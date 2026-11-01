# 心灯（Light）开发进度 — PROGRESS.md

> 本文件是项目"记忆文件"之一。每个阶段、每个新会话开始前，必须先重新读取 [CONVENTIONS.md](./CONVENTIONS.md) 与本文件。
> 每完成一步立即更新本文件。

- 最近更新：2026-11-01
- 当前阶段：阶段 0（规划，不写业务代码）——**已全部完成，等待输出阶段总结确认**
- 当前步骤：阶段 1 / 步骤 1.1（待用户回复"继续"后开始）

## 已完成步骤

### [x] 仓库初始化（2026-11-01 完成，应用户要求插入执行）
- `git init`（分支 `main`），远程 `origin = https://github.com/ZedrealLin/Light.git`；仓库级身份 `ZedrealLin <ZedrealLin@users.noreply.github.com>`，推送走 GCM。
- 已建 `.gitignore`（排除 `.env`，保留 `!.env.example`）与 `.gitattributes`（LF）。

### [x] 0.1 创建记忆文件（2026-11-01 完成）
- `/docs/CONVENTIONS.md`、`/docs/PROGRESS.md`。

### [x] 0.2 ER 设计与 SQL 建表脚本（2026-11-01 完成）
- `/docs/er-design.md`（17 表契约）+ `/backend/db/migrations/0001_init.sql`（17 表 / 21 外键，幂等）+ ER 图可视化。
- 验证：临时 5.7 实例执行两遍 exit=0；防超卖冒烟测试通过；sqlfluff 全量解析 0 错误。

### [x] 决策落实：L4 / L5（2026-11-01 完成，用户确认）
- L4：开发环境用本机 MySQL 5.7.44；SQL 以 5.7 为兼容基线（`utf8mb4_unicode_ci`、无 CTE/窗口函数、CHECK 由应用层承担）；部署目标仍 MySQL 8。原始脚本在 5.7 复验通过。
- L5：注销 = 匿名化并**保留密文数据**；"删除我的数据"为独立入口（物理删除、导出先行）。

### [x] 0.3 接口清单、危机干预流程、风险清单（2026-11-01 完成）
- `/docs/api-spec.md`：9 个模块约 50 个接口的契约（统一返回体、匿名标注、错误码、所属阶段），含"契约变更规则"。
- `/docs/crisis-flow.md`：危机干预唯一权威流程——四来源触发矩阵、命中后统一动作（建工单 + 求助页 + 后台提醒 + AI 拒答）、10 分钟去重、工单状态机、热线/安全话术配置项、关键词缓存策略、验收清单。
- `/docs/risk-list.md`：24 项风险（合规 7 / 安全 7 / 技术 7 / 产品 3），8 条"需人工确认"事项汇总。
- CONVENTIONS.md 错误码表追加：2007 / 4002 / 5002 / 8001。
- 危机干预流程图可视化（会话内，mermaid 源码以 crisis-flow.md 第 7 节为准）。
- **TODO.md 说明**：受"每步 ≤3 个新文件"限制，首批"需人工确认"条目暂存于 risk-list.md 第 5 节，**步骤 1.1 开始时正式创建 `/docs/TODO.md`**（内容取自该节，硬性规则 8）。

### [x] 1.1 后端骨架（2026-11-01 完成）
- `docs/TODO.md`（8 条需人工确认事项正式建立）；`backend/Light.slnx` + `Light.Api`（net10.0）。
- 统一返回体 `ApiResponse<T>`、错误码 `ErrorCodes`（CONVENTIONS 全表落位）、全局异常中间件（对外 1003 不泄堆栈）、Serilog（控制台 + 滚动文件）、内置 OpenAPI（`Microsoft.AspNetCore.OpenApi`）+ Scalar UI（仅开发环境，`/scalar`）。
- 依赖（NuGet 最新稳定版，不猜版本）：Serilog.AspNetCore 10.0.0、Scalar.AspNetCore 2.18.7、Microsoft.AspNetCore.OpenApi 10.0.14。
- 环境：本机 SDK 为 11.0.100-rc.3 + 运行时 10.0.14；实测 SDK 11 RC 可构建/运行 net10.0（目标包 NuGet 自动解析）。`dotnet new sln` 生成新格式 `Light.slnx`。
- **实测输出**：`dotnet build` 0 警告 0 错误；`/api/health` 返回统一体 `{code:0,...,"traceId":"0HNPNT...:00000001"}`；`/openapi/v1.json` HTTP 200（OpenAPI 3.1.1）；`/scalar → 302 → /scalar/ → 200`（"Scalar API Reference"）；Serilog 请求日志正常。

### [x] 1.2 迁移工具与连接设施（2026-11-01 完成）
- `Infrastructure/Db/DbConnectionFactory`：连接串仅来自环境变量 `ConnectionStrings__Default`（硬性规则 9），未配置时健康检查如实报 `not_configured`。
- `Infrastructure/Migrations/DbMigrator`：自写迁移工具——按文件名升序执行输出目录 `db/migrations/*.sql`，记录 `schema_migrations`；内置"顶层分号"切分器（引号/注释内分号不切分）；DDL 隐式提交故逐文件执行、逐文件记录；`Migrations__RunOnStartup=false` 可关闭。
- 依赖：MySqlConnector 2.6.2、Dapper 2.1.89。csproj 将 `/backend/db/migrations/*.sql` 复制到输出目录（脚本唯一来源）。
- **实测输出（临时 5.7 实例，独立 datadir + 端口 3307 + 独立库 light_dev）**：
  1. 首启日志：`应用迁移：0001_init.sql` → `迁移完成：新应用 1 个，跳过 0 个（共 1 个）`；
  2. 数据库核验：`schema_migrations` 有 1 行（0001_init.sql），表数量 18（17 业务表 + schema_migrations）；
  3. 重启幂等：`迁移完成：新应用 0 个，跳过 1 个（共 1 个）`；
  4. `/api/health` 显示 `"db":"ok"`；验证后临时实例已关闭、目录已删除。
- **注意**：真实开发库（本机 5.7.44 服务）的账号密码尚未提供——需要时以环境变量 `ConnectionStrings__Default` 注入（建议写 `.env`，已被 .gitignore 排除），不提交仓库。

### [x] 1.3 量表引擎（2026-11-01 完成）
- 种子迁移 `0002_scale_seed.sql`：PHQ-9 / GAD-7 / SDS 全配置（题目/选项/计分/分级/预警规则/来源标注），SDS 反向题 value 预反转、乘 1.25 得标准分；`config.source.verified=false` 对应 TODO.md T006。
- `Models/Scale/ScaleConfig.cs` + `Services/ScaleEngine.cs`：纯函数引擎——答案完整性/选项合法性校验（3002）、求分（乘数+四舍五入远离零）、分级区间命中、预警规则评估（questionAtLeast）。
- xUnit：`backend/tests/Light.Api.Tests`（net10.0），**14 个用例全部通过**（含"第 9 题 ≥1 必触发"与边界/校验/配置健壮性）。
- 接口（api-spec §2 落地）：`GET /api/scales`、`GET /api/scales/{code}`（对外投影**不含**计分/分级/预警规则）、`POST /api/scale-submissions`（匿名、答案 AES-GCM 加密落库、命中预警响应带 `crisis.helpUrl`）、`GET /api/scale-submissions/{publicId}`。
- 加密设施：`Infrastructure/Crypto/AesGcmCipher`（密钥仅走环境变量 `AES_DATA_KEY`，nonce+密文+tag）。
- 依赖新增：FluentValidation 12.2.0 + DependencyExtensions 12.2.0。
- **实测输出（临时 5.7 实例 + light_dev）**：迁移 `新应用 2 个`；量表列表 3 条；详情投影泄漏检查 `False`；全 0 作答 → `totalScore=0/minimal/crisis=null`；**第 9 题=1 → `crisis={"helpUrl":"/crisis-help"}`**；结果取回 code=0；库中 `readable_plaintext_rows=0`（answers_cipher 均为 200 字节密文）；错误码 3002/3001/1000 全部按预期返回；验证后实例与测试密钥已清理。
- 修复记录：①控制器路由改为契约的绝对路径 `/api/scale-submissions`；②MySqlConnector 将 CHAR(36) 映射为 Guid，仓储属性改用 Guid 接。

## 当前步骤

**阶段 1 / 步骤 1.4：危机触发服务与预警工单**（待开始）
- `CrisisDetectionService`（关键词内存缓存 + 30s 刷新）、命中建工单（10 分钟去重）、`GET /api/crisis/help`、种子迁移 0003（crisis_keywords 初始词集，T007 需人工确认）。

## 遗留问题

| 编号 | 事项 | 状态 |
|---|---|---|
| L2 | `/docs/TODO.md` 未创建 | **已解决**（1.1 建立，8 条待确认 + 2 条已归档） |
| L6 | risk-list.md 中 8 条"需人工确认"事项 | 已正式入 TODO.md（T001–T008），按截止时点确认 |
| L7 | 本机 5.7.44 服务实例的账号密码未提供（真实开发库连接用） | 待用户提供（写 `.env`，环境变量名 `ConnectionStrings__Default`） |

## 下一步

1. 继续执行 1.4（无需用户确认）：危机触发服务 + 预警工单 + 求助页数据接口。
2. 1.4 完成 → 1.5 /web 前端骨架 → 阶段 1 验收（web-gui-tester：作答 PHQ-9 第 9 题 ≥1 必须弹求助页并生成预警工单）。

## 阶段计划对照

| 阶段 | 内容 | 状态 |
|---|---|---|
| 0 | 规划：0.1 / 0.2 / 0.3 | **全部完成** |
| 1 | 骨架 + 量表 + 危机提示（开发库：本机 MySQL 5.7.44） | 未开始 |
| 2 | 账号 + 情绪日记 + 后台 | 未开始 |
| 3 | 科普内容 + 社区（Discourse SSO） | 未开始 |
| 4 | 咨询预约 | 未开始 |
| 5 | AI 陪伴 | 未开始 |
| 6 | 部署与合规收尾 | 未开始 |
