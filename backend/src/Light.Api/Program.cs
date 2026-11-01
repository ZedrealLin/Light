// 心灯（Light）API 入口。
// 骨架接线（步骤 1.1）：Serilog（控制台 + 滚动文件）、全局异常中间件、请求日志、
// 内置 OpenAPI（/openapi/v1.json）+ Scalar UI（/scalar，仅开发环境）。
// 注意：日志不得输出日记正文、量表答案等敏感字段明文（后续各步自觉遵守，自查表核查）。
using Light.Api.Common;
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

    // 基础设施：连接工厂 + 迁移工具
    builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
    builder.Services.AddSingleton<DbMigrator>();

    // 内置 OpenAPI 文档生成（.NET 10 自带，无需第三方包）
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // 启动迁移：可用环境变量 Migrations__RunOnStartup=false 关闭
    if (app.Configuration.GetValue<bool>("Migrations:RunOnStartup"))
    {
        await app.Services.GetRequiredService<DbMigrator>().MigrateAsync();
    }

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
