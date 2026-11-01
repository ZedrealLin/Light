namespace Light.Api.Common;

/// <summary>
/// 业务错误码（集中定义，禁止在业务代码里散落魔法数字）。
/// 分段规则与完整表见 docs/CONVENTIONS.md 第 6 节；新增码必须先改文档再落位到这里。
/// </summary>
public static class ErrorCodes
{
    // —— 1xxx 通用 ——
    public const int InvalidArgument = 1000;
    public const int NotFound = 1001;
    public const int TooManyRequests = 1002;
    public const int InternalError = 1003;

    // —— 2xxx 认证与账号 ——
    public const int Unauthorized = 2001;
    public const int TokenExpired = 2002;
    public const int Forbidden = 2003;
    public const int AccountExists = 2004;
    public const int InvalidCredentials = 2005;
    public const int AccountUnavailable = 2006;
    public const int InvalidCaptcha = 2007;

    // —— 3xxx 量表与危机 ——
    public const int ScaleNotFound = 3001;
    public const int ScaleAnswerInvalid = 3002;
    public const int CrisisAlertTriggered = 3101;

    // —— 4xxx 情绪日记 ——
    public const int DiaryNotFound = 4001;
    public const int DiaryExistsToday = 4002;

    // —— 5xxx 科普内容 ——
    public const int ArticleNotFound = 5001;
    public const int InvalidStateTransition = 5002;

    // —— 6xxx 咨询预约 ——
    public const int SlotBooked = 6001;
    public const int BookingInvalid = 6002;

    // —— 7xxx AI 陪伴 ——
    public const int AiRateLimited = 7001;
    public const int AiCrisisRefusal = 7101;

    // —— 8xxx 后台管理与审核 ——
    public const int AdminInvalidCredentials = 8001;

    /// <summary>各错误码的默认对外文案（可被业务覆盖；对外不含内部细节）。</summary>
    public static string? MessageOf(int code) => code switch
    {
        InvalidArgument => "请求参数有误",
        NotFound => "资源不存在",
        TooManyRequests => "请求过于频繁，请稍后再试",
        InternalError => "服务暂不可用",
        Unauthorized => "请先登录",
        TokenExpired => "登录已过期，请重新登录",
        Forbidden => "没有权限执行该操作",
        AccountExists => "该账号已存在",
        InvalidCredentials => "账号或密码错误",
        AccountUnavailable => "账号已注销或禁用",
        InvalidCaptcha => "验证码无效或已过期",
        ScaleNotFound => "量表不存在或未启用",
        ScaleAnswerInvalid => "量表答案格式不合法",
        DiaryNotFound => "日记不存在",
        DiaryExistsToday => "当日日记已存在",
        ArticleNotFound => "文章不存在或未发布",
        InvalidStateTransition => "当前状态不允许该操作",
        SlotBooked => "该时段已被预约",
        BookingInvalid => "预约不存在或状态不允许该操作",
        AiRateLimited => "AI 会话超过频率限制，请稍后再试",
        AiCrisisRefusal => "我们注意到你可能正处于困难时刻",
        AdminInvalidCredentials => "管理员账号或密码错误",
        _ => null,
    };
}
