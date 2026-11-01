using Dapper;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record CounselorRow(
    long Id, string Name, string? Title, string? LicenseNo, string? Intro,
    string? Specialties, string? AvatarUrl, string ReviewStatus, bool IsActive);

public sealed record SlotRow(long Id, long CounselorId, DateTime StartAt, DateTime EndAt, string Status);

public sealed record BookingRow(
    long Id, long UserId, long CounselorId, string CounselorName,
    DateTime StartAt, DateTime EndAt, string Status, string PaymentStatus,
    DateTime BookedAt, DateTime? CancelledAt);

/// <summary>
/// 咨询师 / 时段 / 预约仓储。
/// 防超卖双保险：①事务内 SELECT ... FOR UPDATE 行锁 + 原子状态更新；
/// ②bookings.active_slot_key 生成列唯一索引（DB 层兜底，见 0001）。
/// </summary>
public sealed class BookingRepository
{
    private readonly IDbConnectionFactory _factory;

    public BookingRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>上架且审核通过的咨询师列表。</summary>
    public async Task<IReadOnlyList<CounselorRow>> ListActiveCounselorsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, title, license_no, intro, specialties, avatar_url, review_status, is_active
            FROM counselors
            WHERE is_active = 1 AND review_status = 'approved'
            ORDER BY id
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CounselorRaw>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToRecord()).ToList();
    }

    public async Task<CounselorRow?> FindActiveCounselorAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, title, license_no, intro, specialties, avatar_url, review_status, is_active
            FROM counselors
            WHERE id = @id AND is_active = 1 AND review_status = 'approved'
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<CounselorRaw>(
            new CommandDefinition(sql, new { id }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    /// <summary>可约时段（available 且在未来）。</summary>
    public async Task<IReadOnlyList<SlotRow>> ListAvailableSlotsAsync(
        long counselorId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT id, counselor_id, start_at, end_at, status
            FROM counselor_slots
            WHERE counselor_id = @counselorId AND status = 'available' AND start_at >= @fromUtc
            """;
        if (to is not null)
        {
            sql += " AND start_at <= @toUtc";
        }
        sql += " ORDER BY start_at LIMIT 200";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<SlotRaw>(new CommandDefinition(sql, new
        {
            counselorId,
            fromUtc = from ?? DateTime.UtcNow,
            toUtc = to,
        }, cancellationToken: cancellationToken));
        return rows.Select(r => new SlotRow(r.id, r.counselor_id, r.start_at, r.end_at, r.status)).ToList();
    }

    /// <summary>
    /// 创建预约（事务 + 行锁防超卖）。返回 (bookingId, 错误码)——0 表示成功，6001 时段不可约。
    /// 同时写预约通知。
    /// </summary>
    public async Task<(long BookingId, int ErrorCode)> CreateBookingAsync(
        long userId, long slotId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string lockSql = """
                SELECT s.id, s.counselor_id, s.status, s.start_at, s.end_at, c.name AS counselor_name
                FROM counselor_slots s
                JOIN counselors c ON c.id = s.counselor_id
                WHERE s.id = @slotId
                FOR UPDATE
                """;
            var slot = await connection.QuerySingleOrDefaultAsync<SlotLockRaw>(new CommandDefinition(
                lockSql, new { slotId }, transaction, cancellationToken: cancellationToken));
            if (slot is null || slot.status != "available")
            {
                await transaction.RollbackAsync(cancellationToken);
                return (0, ErrorCodesSlotBooked);
            }

            const string updateSlot = "UPDATE counselor_slots SET status = 'booked' WHERE id = @slotId";
            await connection.ExecuteAsync(new CommandDefinition(
                updateSlot, new { slotId }, transaction, cancellationToken: cancellationToken));

            const string insertBooking = """
                INSERT INTO bookings (user_id, counselor_id, slot_id, status, payment_status)
                VALUES (@userId, @counselorId, @slotId, 'booked', 'none');
                SELECT LAST_INSERT_ID();
                """;
            var bookingId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                insertBooking, new { userId, counselorId = slot.counselor_id, slotId }, transaction, cancellationToken: cancellationToken));

            const string insertNotification = """
                INSERT INTO notifications (user_id, type, title, body)
                VALUES (@userId, 'booking_confirmed', '预约成功', @body)
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertNotification, new
            {
                userId,
                body = $"您已成功预约 {slot.counselor_name}：{slot.start_at:yyyy-MM-dd HH:mm} - {slot.end_at:HH:mm}（UTC）。",
            }, transaction, cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return (bookingId, 0);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry) // uk_bookings_active_slot 兜底
        {
            await transaction.RollbackAsync(cancellationToken);
            return (0, ErrorCodesSlotBooked);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private const int ErrorCodesSlotBooked = 6001;

    /// <summary>我的预约列表。</summary>
    public async Task<IReadOnlyList<BookingRow>> ListByUserAsync(
        long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT b.id, b.user_id, b.counselor_id, c.name AS counselor_name,
                   s.start_at, s.end_at, b.status, b.payment_status, b.booked_at, b.cancelled_at
            FROM bookings b
            JOIN counselors c ON c.id = b.counselor_id
            JOIN counselor_slots s ON s.id = b.slot_id
            WHERE b.user_id = @userId
            ORDER BY b.booked_at DESC
            LIMIT 200
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<BookingRaw>(
            new CommandDefinition(sql, new { userId }, cancellationToken: cancellationToken));
        return rows.Select(r => new BookingRow(r.id, r.user_id, r.counselor_id, r.counselor_name,
            r.start_at, r.end_at, r.status, r.payment_status, r.booked_at, r.cancelled_at)).ToList();
    }

    /// <summary>取消预约（事务）：booking.booked → cancelled；slot 释放回 available；通知。</summary>
    public async Task<bool> CancelAsync(
        long userId, long bookingId, string? reason, CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string lockSql = """
                SELECT b.id, b.user_id, b.status, b.slot_id, s.start_at, s.end_at, c.name AS counselor_name
                FROM bookings b
                JOIN counselor_slots s ON s.id = b.slot_id
                JOIN counselors c ON c.id = b.counselor_id
                WHERE b.id = @bookingId
                FOR UPDATE
                """;
            var row = await connection.QuerySingleOrDefaultAsync<CancelLockRaw>(new CommandDefinition(
                lockSql, new { bookingId }, transaction, cancellationToken: cancellationToken));
            if (row is null || row.user_id != userId || row.status != "booked")
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            const string cancelBooking = """
                UPDATE bookings SET status = 'cancelled', cancelled_at = UTC_TIMESTAMP(3), cancel_reason = @reason
                WHERE id = @bookingId
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                cancelBooking, new { bookingId, reason }, transaction, cancellationToken: cancellationToken));

            const string freeSlot = "UPDATE counselor_slots SET status = 'available' WHERE id = @slotId";
            await connection.ExecuteAsync(new CommandDefinition(
                freeSlot, new { slotId = row.slot_id }, transaction, cancellationToken: cancellationToken));

            const string insertNotification = """
                INSERT INTO notifications (user_id, type, title, body)
                VALUES (@userId, 'booking_cancelled', '预约已取消', @body)
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertNotification, new
            {
                userId,
                body = $"您对 {row.counselor_name}（{row.start_at:yyyy-MM-dd HH:mm} UTC）的预约已取消。",
            }, transaction, cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ---------- 后台 ----------

    public async Task<long> InsertCounselorAsync(
        string name, string? gender, string? title, string? licenseNo,
        string? intro, string? specialties, long adminId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO counselors (name, gender, title, license_no, intro, specialties,
                                    review_status, reviewed_by, reviewed_at, is_active)
            VALUES (@name, @gender, @title, @licenseNo, @intro, @specialties,
                    'approved', @adminId, UTC_TIMESTAMP(3), 1);
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            name, gender = gender ?? "unknown", title, licenseNo, intro, specialties, adminId,
        }, cancellationToken: cancellationToken));
    }

    /// <summary>批量排班：同一时刻可能因唯一键冲突失败（重复排班），由调用方提示。</summary>
    public async Task<int> InsertSlotsAsync(
        long counselorId, DateTime startAt, DateTime endAt, int count, int intervalMinutes,
        CancellationToken cancellationToken = default)
    {
        var inserted = 0;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        for (var i = 0; i < count; i++)
        {
            var s = startAt.AddMinutes(intervalMinutes * i);
            var e = endAt.AddMinutes(intervalMinutes * i);
            const string sql = """
                INSERT INTO counselor_slots (counselor_id, start_at, end_at, status)
                VALUES (@counselorId, @s, @e, 'available')
                """;
            try
            {
                inserted += await connection.ExecuteAsync(new CommandDefinition(
                    sql, new { counselorId, s, e }, cancellationToken: cancellationToken));
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            {
                // 与既有时段冲突：跳过该时段（uk_counselor_slots_counselor_start）
            }
        }
        return inserted;
    }

    public async Task<IReadOnlyList<BookingRow>> ListAllBookingsAsync(
        long? counselorId, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT b.id, b.user_id, b.counselor_id, c.name AS counselor_name,
                   s.start_at, s.end_at, b.status, b.payment_status, b.booked_at, b.cancelled_at
            FROM bookings b
            JOIN counselors c ON c.id = b.counselor_id
            JOIN counselor_slots s ON s.id = b.slot_id
            """;
        if (counselorId is not null)
        {
            sql += " WHERE b.counselor_id = @counselorId";
        }
        sql += " ORDER BY b.booked_at DESC LIMIT 200";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<BookingRaw>(new CommandDefinition(
            sql, new { counselorId }, cancellationToken: cancellationToken));
        return rows.Select(r => new BookingRow(r.id, r.user_id, r.counselor_id, r.counselor_name,
            r.start_at, r.end_at, r.status, r.payment_status, r.booked_at, r.cancelled_at)).ToList();
    }

    /// <summary>后台完结预约（completed / no_show）：终态后 active_slot_key 变 NULL。</summary>
    public async Task<bool> CompleteBookingAsync(
        long bookingId, string status, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE bookings SET status = @status WHERE id = @id AND status = 'booked'";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            sql, new { bookingId, status }, cancellationToken: cancellationToken)) > 0;
    }

    private sealed class CounselorRaw
    {
        public long id { get; set; }
        public string name { get; set; } = string.Empty;
        public string? title { get; set; }
        public string? license_no { get; set; }
        public string? intro { get; set; }
        public string? specialties { get; set; }
        public string? avatar_url { get; set; }
        public string review_status { get; set; } = string.Empty;
        public sbyte is_active { get; set; }

        public CounselorRow ToRecord() => new(id, name, title, license_no, intro, specialties, avatar_url, review_status, is_active == 1);
    }

    private sealed class SlotRaw
    {
        public long id { get; set; }
        public long counselor_id { get; set; }
        public DateTime start_at { get; set; }
        public DateTime end_at { get; set; }
        public string status { get; set; } = string.Empty;
    }

    private sealed class SlotLockRaw
    {
        public long id { get; set; }
        public long counselor_id { get; set; }
        public string status { get; set; } = string.Empty;
        public DateTime start_at { get; set; }
        public DateTime end_at { get; set; }
        public string counselor_name { get; set; } = string.Empty;
    }

    private sealed class BookingRaw
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public long counselor_id { get; set; }
        public string counselor_name { get; set; } = string.Empty;
        public DateTime start_at { get; set; }
        public DateTime end_at { get; set; }
        public string status { get; set; } = string.Empty;
        public string payment_status { get; set; } = string.Empty;
        public DateTime booked_at { get; set; }
        public DateTime? cancelled_at { get; set; }
    }

    private sealed class CancelLockRaw
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public string status { get; set; } = string.Empty;
        public long slot_id { get; set; }
        public DateTime start_at { get; set; }
        public DateTime end_at { get; set; }
        public string counselor_name { get; set; } = string.Empty;
    }
}
