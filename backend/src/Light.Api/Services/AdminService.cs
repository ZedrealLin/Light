using System.Text.Json;
using Dapper;
using Light.Api.Common;
using Light.Api.Infrastructure.Auth;
using Light.Api.Infrastructure.Db;
using Light.Api.Models.Dto;
using Light.Api.Models.Scale;
using Light.Api.Repositories;
using MySqlConnector;

namespace Light.Api.Services;

/// <summary>
/// 后台管理业务。预警工单处理（状态机 crisis-flow §4）、用户管理、量表版本管理、关键词管理。
/// </summary>
public sealed class AdminService
{
    private readonly AdminRepository _admins;
    private readonly IDbConnectionFactory _factory;
    private readonly JwtTokenService _jwt;
    private readonly CrisisDetectionService _detection;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        AdminRepository admins, IDbConnectionFactory factory, JwtTokenService jwt,
        CrisisDetectionService detection, IWebHostEnvironment environment,
        ILogger<AdminService> logger)
    {
        _admins = admins;
        _factory = factory;
        _jwt = jwt;
        _detection = detection;
        _environment = environment;
        _logger = logger;
    }

    public async Task<AdminTokenResponse> LoginAsync(AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var admin = await _admins.FindAdminByUsernameAsync(request.Username, cancellationToken);
        if (admin is null || !admin.IsActive
            || !PasswordHasher.Verify(request.Password, admin.PasswordHash))
        {
            throw new BizException(ErrorCodes.AdminInvalidCredentials, "管理员账号或密码错误");
        }
        await _admins.UpdateAdminLastLoginAsync(admin.Id, cancellationToken);
        var token = _jwt.IssueAccessToken(admin.Id, role: "admin");
        return new AdminTokenResponse(token.Token, token.ExpiresAtUtc, admin.DisplayName ?? admin.Username);
    }

    public async Task<IReadOnlyList<AdminAlertItemResponse>> ListAlertsAsync(
        string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (rows, _) = await _admins.ListAlertsAsync(status, page, pageSize, cancellationToken);
        return rows.Select(r => new AdminAlertItemResponse(
            r.Id, r.Source, r.Status, r.DetailJson, r.UserId, r.ExternalRef,
            r.CreatedAt, r.HandledAt, r.HandleNote)).ToList();
    }

    public async Task<bool> HandleAlertAsync(
        long adminId, long alertId, HandleAlertRequest request, CancellationToken cancellationToken)
    {
        return await _admins.HandleAlertAsync(alertId, adminId, request.Action, request.Note?.Trim(), cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUserItemResponse>> ListUsersAsync(
        string? query, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (rows, _) = await _admins.ListUsersAsync(query, page, pageSize, cancellationToken);
        return rows.Select(r => new AdminUserItemResponse(
            r.Id, r.PublicId, r.Nickname, Mask(r.Phone), Mask(r.Email), r.Status, r.CreatedAt)).ToList();
    }

    public Task<bool> DisableUserAsync(long userId, bool disable, CancellationToken cancellationToken)
    {
        if (disable)
        {
            // 禁用同时吊销其全部刷新令牌，立即生效
            return _admins.SetUserStatusAsync(userId, "disabled", cancellationToken);
        }
        return _admins.SetUserStatusAsync(userId, "active", cancellationToken);
    }

    /// <summary>新增量表配置版本（历史版本保留）；config_json 必须能解析为量表配置。</summary>
    public async Task<long> AddScaleVersionAsync(
        ScaleConfigUpdateRequest request, CancellationToken cancellationToken)
    {
        ScaleConfigDocument? config;
        try
        {
            config = JsonSerializer.Deserialize<ScaleConfigDocument>(request.ConfigJson);
        }
        catch (JsonException ex)
        {
            throw new BizException(ErrorCodes.InvalidArgument, $"配置不是合法 JSON：{ex.Message}");
        }
        if (config is null || config.Questions.Count == 0)
        {
            throw new BizException(ErrorCodes.InvalidArgument, "配置缺少题目");
        }
        if (!string.Equals(config.Code, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            throw new BizException(ErrorCodes.InvalidArgument, "配置内 code 与请求的 code 不一致");
        }
        return await _admins.InsertScaleVersionAsync(
            request.Code, request.Name, request.ConfigJson, request.IsActive, cancellationToken);
    }

    public Task<bool> SetScaleActiveAsync(long scaleId, bool isActive, CancellationToken cancellationToken)
        => _admins.SetScaleActiveAsync(scaleId, isActive, cancellationToken);

    public Task<IReadOnlyList<string>> ListKeywordsAsync(bool onlyActive, CancellationToken cancellationToken)
        => _admins.ListKeywordsAsync(onlyActive, cancellationToken);

    public async Task AddKeywordAsync(string keyword, CancellationToken cancellationToken)
    {
        await _admins.AddKeywordAsync(keyword.Trim(), cancellationToken);
        await _detection.ReloadAsync(cancellationToken); // 立即生效（后台任务仍每 30s 兜底刷新）
    }

    public async Task<bool> ToggleKeywordAsync(string keyword, bool isActive, CancellationToken cancellationToken)
    {
        var ok = await _admins.SetKeywordActiveAsync(keyword, isActive, cancellationToken);
        await _detection.ReloadAsync(cancellationToken);
        return ok;
    }

    /// <summary>
    /// 开发环境兜底：admins 表为空时创建默认管理员（admin / Admin@123456）。
    /// 生产环境禁止依赖此默认口令——上线前必须修改（docs/TODO.md T009）。
    /// </summary>
    public async Task EnsureDefaultAdminAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM admins", cancellationToken: cancellationToken));
        if (count > 0)
        {
            return;
        }
        const string sql = """
            INSERT INTO admins (username, password_hash, display_name)
            VALUES ('admin', @hash, '系统管理员')
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            hash = PasswordHasher.Hash("Admin@123456"),
        }, cancellationToken: cancellationToken));
        _logger.LogWarning("已创建默认管理员 admin（开发兜底）。生产环境上线前必须修改口令（docs/TODO.md T009）");
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Contains('@')
            ? $"{value[..2]}****{value[(value.IndexOf('@') - 1)..]}"
            : $"{value[..3]}****{value[^2..]}";
    }
}
