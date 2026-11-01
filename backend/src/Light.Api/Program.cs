// 心灯（Light）API 入口。
// 骨架接线（步骤 1.1）：Serilog（控制台 + 滚动文件）、全局异常中间件、请求日志、
// 内置 OpenAPI（/openapi/v1.json）+ Scalar UI（/scalar，仅开发环境）。
// 注意：日志不得输出日记正文、量表答案等敏感字段明文（后续各步自觉遵守，自查表核查）。
using Light.Api.Common;
using FluentValidation;
using Light.Api.Infrastructure.Db;
using Light.Api.Infrastructure.Migrations;
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

    // 基础设施：连接工厂 + 迁移工具 + 应用层加密（密钥走环境变量 AES_DATA_KEY）
    builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
    builder.Services.AddSingleton<DbMigrator>();
    builder.Services.AddSingleton<Light.Api.Infrastructure.Crypto.IAesCipher, Light.Api.Infrastructure.Crypto.AesGcmCipher>();

    // 业务：量表
    builder.Services.AddScoped<Light.Api.Repositories.ScaleRepository>();
    builder.Services.AddScoped<Light.Api.Repositories.ScaleSubmissionRepository>();
    builder.Services.AddScoped<Light.Api.Repositories.CrisisRepository>();
    builder.Services.AddScoped<Light.Api.Services.CrisisAlertService>();
    builder.Services.AddScoped<Light.Api.Services.ScaleService>();

    // 危机干预：关键词检测（单例缓存）+ 30 秒刷新后台任务
    builder.Services.AddSingleton<Light.Api.Services.CrisisDetectionService>();
    builder.Services.AddHostedService<Light.Api.Infrastructure.Jobs.CrisisKeywordRefreshJob>();

    builder.Services.AddValidatorsFromAssemblyContaining<Light.Api.Models.Dto.ScaleSubmitRequestValidator>();

    // 内置 OpenAPI 文档生成（.NET 10 自带，无需第三方包）
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // 启动迁移：可用环境变量 Migrations__RunOnStartup=false 关闭
    if (app.Configuration.GetValue<bool>("Migrations:RunOnStartup"))
    {
        await app.Services.GetRequiredService<DbMigrator>().MigrateAsync();
    }

    // 危机关键词首次加载（此后由后台任务每 30 秒刷新）
    await app.Services.GetRequiredService<Light.Api.Services.CrisisDetectionService>().ReloadAsync();

    // 全局异常中间件必须最先注册：任何管道内异常都收敛为统一返回体
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();

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
