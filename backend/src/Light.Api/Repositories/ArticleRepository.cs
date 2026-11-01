using Dapper;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record ArticleRow(
    long Id,
    long? CategoryId,
    string Title,
    string? Slug,
    string? Summary,
    string Content,
    string SourceInfo,
    string Status,
    long? ReviewedBy,
    string? ReviewNote,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ArticleCategoryRow(long Id, string Name, string Slug, int SortOrder, bool IsActive);

/// <summary>科普文章仓储（审核发布流，硬性规则 7：来源/审核人/更新时间）。</summary>
public sealed class ArticleRepository
{
    private readonly IDbConnectionFactory _factory;

    public ArticleRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<ArticleCategoryRow>> ListCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id, name, slug, sort_order, is_active FROM article_categories WHERE is_active = 1 ORDER BY sort_order";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CatRaw>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(r => new ArticleCategoryRow(r.id, r.name, r.slug, r.sort_order, r.is_active == 1)).ToList();
    }

    /// <summary>前台列表：仅 published。</summary>
    public async Task<(IReadOnlyList<ArticleRow> Items, int Total)> ListPublishedAsync(
        long? categoryId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var where = "status = 'published'";
        if (categoryId is not null)
        {
            where += " AND category_id = @categoryId";
        }
        var sql = $"""
            SELECT id, category_id, title, slug, summary, content, source_info, status,
                   reviewed_by, review_note, published_at, created_at, updated_at
            FROM articles
            WHERE {where}
            ORDER BY published_at DESC
            LIMIT @limit OFFSET @offset
            """;
        var countSql = $"SELECT COUNT(*) FROM articles WHERE {where}";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var items = (await connection.QueryAsync<ArticleRaw>(new CommandDefinition(sql, new
        {
            categoryId, limit = pageSize, offset = (page - 1) * pageSize,
        }, cancellationToken: cancellationToken))).ToList();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { categoryId }, cancellationToken: cancellationToken));
        return (items.Select(r => r.ToRecord()).ToList(), total);
    }

    public async Task<ArticleRow?> FindPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, category_id, title, slug, summary, content, source_info, status,
                   reviewed_by, review_note, published_at, created_at, updated_at
            FROM articles
            WHERE slug = @slug AND status = 'published'
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ArticleRaw>(
            new CommandDefinition(sql, new { slug }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    // ---------- 后台 ----------

    public async Task<long> InsertAsync(
        long? categoryId, string title, string? slug, string? summary, string content,
        string sourceInfo, long adminId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO articles (category_id, title, slug, summary, content, source_info, status, submitted_by)
            VALUES (@categoryId, @title, @slug, @summary, @content, @sourceInfo, 'draft', @adminId);
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            categoryId, title, slug, summary, content, sourceInfo, adminId,
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SetStatusAsync(long id, string status, CancellationToken cancellationToken = default)
    {
        var setPublished = status == "published" ? ", published_at = COALESCE(published_at, UTC_TIMESTAMP(3))" : "";
        var sql = $"UPDATE articles SET status = @status{setPublished} WHERE id = @id";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { id, status }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> ReviewAsync(
        long id, long adminId, bool approve, string? note, CancellationToken cancellationToken = default)
    {
        var newStatus = approve ? "published" : "draft";
        const string sql = """
            UPDATE articles
            SET status = @newStatus, reviewed_by = @adminId, reviewed_at = UTC_TIMESTAMP(3),
                review_note = @note, published_at = CASE WHEN @approve THEN COALESCE(published_at, UTC_TIMESTAMP(3)) ELSE published_at END
            WHERE id = @id AND status = 'pending_review'
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            id, adminId, note, approve = approve ? 1 : 0, newStatus,
        }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> SubmitAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE articles SET status = 'pending_review' WHERE id = @id AND status = 'draft'";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<(IReadOnlyList<ArticleRow> Items, int Total)> ListAllAsync(
        string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var where = "1=1";
        if (!string.IsNullOrEmpty(status))
        {
            where += " AND status = @status";
        }
        var sql = $"""
            SELECT id, category_id, title, slug, summary, content, source_info, status,
                   reviewed_by, review_note, published_at, created_at, updated_at
            FROM articles
            WHERE {where}
            ORDER BY updated_at DESC
            LIMIT @limit OFFSET @offset
            """;
        var countSql = $"SELECT COUNT(*) FROM articles WHERE {where}";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var items = (await connection.QueryAsync<ArticleRaw>(new CommandDefinition(sql, new
        {
            status, limit = pageSize, offset = (page - 1) * pageSize,
        }, cancellationToken: cancellationToken))).ToList();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { status }, cancellationToken: cancellationToken));
        return (items.Select(r => r.ToRecord()).ToList(), total);
    }

    public async Task<(string ReviewerName, DateTime PublishedAt, DateTime UpdatedAt)?> GetMetaForPublishedAsync(
        long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COALESCE(ad.display_name, ad.username, '后台') AS reviewer, a.published_at, a.updated_at
            FROM articles a
            LEFT JOIN admins ad ON ad.id = a.reviewed_by
            WHERE a.id = @id
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<MetaRaw>(
            new CommandDefinition(sql, new { id }, cancellationToken: cancellationToken));
        return row is null ? null : (row.reviewer, row.published_at, row.updated_at);
    }

    private sealed class ArticleRaw
    {
        public long id { get; set; }
        public long? category_id { get; set; }
        public string title { get; set; } = string.Empty;
        public string? slug { get; set; }
        public string? summary { get; set; }
        public string content { get; set; } = string.Empty;
        public string source_info { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public long? reviewed_by { get; set; }
        public string? review_note { get; set; }
        public DateTime? published_at { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }

        public ArticleRow ToRecord() => new(id, category_id, title, slug, summary, content, source_info,
            status, reviewed_by, review_note, published_at, created_at, updated_at);
    }

    private sealed class CatRaw
    {
        public long id { get; set; }
        public string name { get; set; } = string.Empty;
        public string slug { get; set; } = string.Empty;
        public int sort_order { get; set; }
        public sbyte is_active { get; set; }
    }

    private sealed class MetaRaw
    {
        public string reviewer { get; set; } = string.Empty;
        public DateTime published_at { get; set; }
        public DateTime updated_at { get; set; }
    }
}
