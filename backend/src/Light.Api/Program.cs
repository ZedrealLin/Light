// 心灯（Light）API 入口。
// 骨架接线（步骤 1.1）：Serilog（控制台 + 滚动文件）、全局异常中间件、请求日志、
// 内置 OpenAPI（/openapi/v1.json）+ Scalar UI（/scalar，仅开发环境）。
// 注意：日志不得输出日记正文、量表答案等敏感字段明文（后续各步自觉遵守，自查表核查）。
using Light.Api.Common;
using FluentValidation;
using Light.Api.Infrastructure.Auth;
using Light.Api.Infrastructure.Db;
using Light.Api.Infrastructure.Migrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();

    // [ApiController] 绑定失败的 400 也收敛为统一返回体（code=1000），不泄露内部细节
    builder.Services.Configure<ApiBehaviorOptions>(options =>
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "请求格式不正确";
            var payload = ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, message, context.HttpContext.TraceIdentifier);
            return new BadRequestObjectResult(payload);
        });

    // 基础设施：连接工厂 + 迁移工具 + 应用层加密（密钥走环境变量 AES_DATA_KEY）
    builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
    builder.Services.AddSingleton<DbMigrator>();
    builder.Services.AddSingleton<Light.Api.Infrastructure.Crypto.IAesCipher, Light.Api.Infrastructure.Crypto.AesGcmCipher>();

    // 业务：量表 + 日记
    builder.Services.AddScoped<Light.Api.Repositories.ScaleRepository>();
    builder.Services.AddScoped<Light.Api.Repositories.ScaleSubmissionRepository>();
    builder.Services.AddScoped<Light.Api.Repositories.CrisisRepository>();
    builder.Services.AddScoped<Light.Api.Services.CrisisAlertService>();
    builder.Services.AddScoped<Light.Api.Services.ScaleService>();
    builder.Services.AddScoped<Light.Api.Repositories.DiaryRepository>();
    builder.Services.AddScoped<Light.Api.Services.DiaryService>();
    builder.Services.AddScoped<Light.Api.Repositories.AdminRepository>();
    builder.Services.AddScoped<Light.Api.Services.AdminService>();

    // 危机干预：关键词检测（单例缓存）+ 30 秒刷新后台任务
    builder.Services.AddSingleton<Light.Api.Services.CrisisDetectionService>();
    builder.Services.AddHostedService<Light.Api.Infrastructure.Jobs.CrisisKeywordRefreshJob>();

    builder.Services.AddValidatorsFromAssemblyContaining<Light.Api.Models.Dto.ScaleSubmitRequestValidator>();

    // 认证与授权（阶段 2）：JWT Bearer；默认所有接口需鉴权（FallbackPolicy），匿名接口显式 [AllowAnonymous]
    var jwtTokenService = new JwtTokenService(builder.Configuration);
    builder.Services.AddSingleton(jwtTokenService);
    builder.Services.AddScoped<Light.Api.Repositories.UserRepository>();
    builder.Services.AddScoped<Light.Api.Services.AuthService>();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // 关闭入站声明映射：sub 保持 "sub"，便于 User.FindFirst("sub") 读取
            options.MapInboundClaims = false;
            options.TokenValidationParameters = jwtTokenService.ValidationParameters;
        });
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    // 内置 OpenAPI 文档生成（.NET 10 自带，无需第三方包）
    builder.Services.AddOpenApi();

    // CORS：来源走配置（Cors:AllowedOrigins），未配置时默认放行本地前端开发服务器
    const string corsPolicy = "web";
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
    builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();

    // 启动迁移：可用环境变量 Migrations__RunOnStartup=false 关闭
    if (app.Configuration.GetValue<bool>("Migrations:RunOnStartup"))
    {
        await app.Services.GetRequiredService<DbMigrator>().MigrateAsync();
    }

    // 危机关键词首次加载（此后由后台任务每 30 秒刷新）
    var detection = app.Services.GetRequiredService<Light.Api.Services.CrisisDetectionService>();
    await detection.ReloadAsync();

    // 开发兜底：admins 表为空时创建默认管理员（生产上线前必须改口令，TODO T009）
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider
            .GetRequiredService<Light.Api.Services.AdminService>()
            .EnsureDefaultAdminAsync(CancellationToken.None);
    }

    // 全局异常中间件必须最先注册：任何管道内异常都收敛为统一返回体
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseCors(corsPolicy);
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapOpenApi();
    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference(); // /scalar — Scalar UI
    }

    app.MapControllers();

    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
