# 心灯（Light）开发进度 — PROGRESS.md

> 本文件是项目"记忆文件"之一。每个阶段、每个新会话开始前，必须先重新读取 [CONVENTIONS.md](./CONVENTIONS.md) 与本文件。
> 每完成一步立即更新本文件。

- 最近更新：2026-11-01
- 当前阶段：阶段 0（规划，不写业务代码）——**已全部完成，等待输出阶段总结确认**
- 当前步骤：**全部六阶段完成 + 全面梳理优化一轮**（2026-11-01 梳理修复：
  ①日记分页 total 真 bug（翻页错值，临时实例实测 3 篇/页1=1/total=3）；②短信验证码 mock 并发竞态（改为方法返回值）；
  ③已删除账号访问 /api/me → 401 而非 500；④compose 构建上下文修正（Dockerfile 移入项目内）+ 三个 .dockerignore；
  ⑤nginx web/admin 增加 /api 反代（匹配 VITE_API_BASE=/api 相对路径）；⑥前端 request.ts 与 AiController 死代码清理。
  上线前必办清单见 docs/security-review.md §4 与 docs/TODO.md）

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

### [x] 1.4 危机触发服务与预警工单（2026-11-01 完成）
- `Repositories/CrisisRepository`：关键词读取、工单写入、按 (source+关联对象) 的 10 分钟窗口去重查询。
- `Services/CrisisDetectionService`：启用词集内存缓存 + `Match()` 大小写不敏感包含匹配；`Infrastructure/Jobs/CrisisKeywordRefreshJob`（BackgroundService）每 30 秒刷新，启动时首载。
- `Services/CrisisAlertService`：统一建单动作——去重命中跳过落单（求助展示仍执行）、`detail_json` 最小化快照（只含规则/命中词，**不含答案与正文**）、建单失败不阻断用户、Warning 日志供监控接入。量表命中在 `ScaleService.SubmitAsync` 内联动建单。
- `0003_crisis_keywords_seed.sql`：12 个初始关键词（T007 需人工确认）；`GET /api/crisis/help`：免责声明 + 热线（`CRISIS_HOTLINES` 配置，默认占位 `0-000-000000` + "上线前人工核实"）+ 建议文案。
- **实测输出（临时 5.7 实例）**：迁移 `新应用 3 个`；关键词加载 `12 个`；两次第 9 题=1 提交 → 响应 `crisis` 非空 + `crisis_alerts` 两条（source=scale, status=open, detail 只含 triggeredRules）；`/api/crisis/help` 返回占位热线与 T001 标注。
- 过程修复：①`Hotline` record 补 `JsonPropertyName`（System.Text.Json 默认大小写敏感导致字段全 null）；②`AesGcmCipher` 对 `AES_DATA_KEY` 做 Trim 容错；③定位测试环境陷阱——**Git Bash(MSYS) 会把以 `/` 开头的环境变量值当路径转换**，测试脚本改用不以 `/` 开头的一次性密钥（产品代码无此问题）。

### [x] 1.5 /web 前端骨架（2026-11-01 完成）
- Vite + Vue3 + TS + Pinia + Vue Router + Element Plus；`api/request.ts` 统一封装（统一返回体解包、非 0 统一提示、ApiError、401 预留）。
- 五个页面：首页（危机常驻入口）、量表列表（来源 + 核实标注）、作答页（进度 + 缺答校验）、结果页（免责声明 + 危机弹层）、`/crisis-help` 求助页（热线占位 + T001 标注）。
- 路由按模块拆分（`router/modules/scales.ts`）；全局浅暖色样式（低饱和主色、避免高饱和红色、字号偏大）。
- 构建：`npm run build` 通过（Element Plus 全量引入约 1MB 包为已知优化项）。

### [x] 阶段 1 验收（2026-11-01 完成，web-gui-tester 黑盒 + control-browser 截图）
- **T1 首页** ✅：DOM + 截图；危机入口显眼不惊吓、免责声明齐全。
- **T2 量表列表** ✅：3 个量表、来源 + "待核实"标签；截图有平铺伪影（环境级捕获缺陷）但内容可辨。
- **T3 核心验收（PROJECT_BRIEF 指定）** ✅：GUI 黑盒作答 PHQ-9，第 1–8 题"完全不会"、**第 9 题"好几天"（1 分）**→ 提交 → 结果页总分 1 + 免责声明 + **危机弹层自动弹出**（截图确认）→ 点击"查看求助资源"→ 求助页显示热线占位 `0-000-000000` + "上线前人工核实（T001）"（截图确认）。
- **T4 工单落库** ✅：本次 GUI 提交（publicId `01a1962f...`）→ `scale_submissions.is_crisis=1` → `crisis_alerts` 一条 `source=scale, status=open`，detail_json 含触发规则且与作答关联。
- **发现并修复 ISS-1（阻塞级）**：后端缺 CORS 配置 → 前端联调全挂；已加 `AddCors/UseCors` + `Cors:AllowedOrigins` 配置化（提交 `32199ad`）。
- **ISS-2（环境级，非项目缺陷）**：ZCode 内置浏览器截图捕获面多次使用后超时/平铺伪影；DOM 快照始终干净，不影响页面质量结论。
- 环境清理：临时 MySQL（独立 datadir + 3307）、后端、Vite 进程与一次性 AES 密钥全部关闭删除，端口 5080/5173/3307 释放。

### [x] 阶段 2 账号 + 情绪日记 + 后台（2026-11-01 完成）
- **2.1 认证**（`3174575`）：PBKDF2 密码哈希、JWT 访问令牌 + 刷新令牌轮换（仅存 SHA-256 哈希）、FallbackPolicy 默认鉴权（匿名显式 [AllowAnonymous]）、BizException → 业务码统一返回体、[ApiController] 绑定错误收敛、短信验证码 mock（内存、Development 才回传、日志不打码）。实测：注册/登录/脱敏 me/刷新/重放 2001/错密码 2005/重复 2004/401。
- **2.2 情绪日记**（`4d9c3c3`）：AES-GCM 密文 CRUD、软删除 + 同日期恢复/覆盖、趋势、提醒设置；危机词命中自动建工单（detail 只含命中词）。实测：plaintext_leak=0、4002、危机日记 hasCrisis=true → 工单。
- **2.3 后台**（`fa01ac4`）：/api/admin/*（admin 角色）、工单状态机（open→processing→resolved/dismissed，终态 5002 保护）、用户禁用/启用、量表版本管理、关键词管理即时生效；开发兜底默认管理员（T009 生产必须改）。
- **2.4 /web 前端**（`91c99a6`）：登录/注册（隐私单独勾选）、auth store + 401 自动刷新、路由守卫、日记列表/编辑/详情/趋势（ECharts）/提醒；危机词保存后陪伴弹层。
- **2.5 /admin 前端**（`bb61ea9`）：登录、预警工单（筛选 + 受理/解决/误报留痕）、用户管理、量表与关键词管理。
- **验收（PROJECT_BRIEF）**：①数据库中日记正文为密文——`plaintext_leak=0` 实测 ✅；②后台可处理预警工单——API 级状态机全流程（open→processing→resolved，终态 5002）+ 管理端 UI 渲染验证（DOM）✅。
- **ISS-2 恶化记录**：本轮 ZCode 内置浏览器点击面完全失效（阶段 1 尚可用），GUI 点击类验收被迫降级为 API 级 + DOM 渲染验证；环境级问题，非项目缺陷。dom_cua/Playwright 混合排障过程详见会话记录。

### [~] 阶段 3 科普内容 + 社区（进行中：后端已完成并实测，前端页与 Discourse 部署配置待做）

- **3.1 文章后端**（`aafcca0`，实测通过）：0005 种子（分类 + 示例待审核文章）；前台仅 published 可见（审核前 0 篇 → 审核后 1 篇）；详情含 信息来源/审核人/更新时间/免责声明（硬性规则 7）；后台接口 /api/admin/articles（提交/审核/下线，驳回必填意见）。
- **3.2 SSO 与社区回调**（`aafcca0`，实测通过）：
  - DiscourseConnectService：HMAC-SHA256 验签（FixedTimeEquals 防时序），密钥 env `DISCOURSE_SSO_SECRET`，开关 `DISCOURSE_SSO_ENABLED`；
  - 设计修正：真实 Discourse 只传 nonce，本站 JWT 在 localStorage 不随 302 传递 → GET /api/community/sso 302 到前端处理页 `/community-sso`，前端持令牌调 **POST /api/community/sso-url** 换取回跳地址（实测回跳签名自洽、含 external_id/email/nonce，坏签名 1000）；
  - POST /api/community/crisis-callback：`X-Light-Signature`（HMAC over raw body）验签 + 关键词命中建工单（external_ref=`discourse://topic/{id}/post/{id}`）。实测：有效签名+危机词→工单(source=community)；坏签名 1000；无危机词 alerted=false。
- **待做（3.3）**：/web 文章列表+详情页、/community-sso 处理页、/admin 文章审核页；`deploy/discourse/`（官方镜像独立部署配置 + SSO 参数说明，本机无法起 Discourse → 部署项记录 TODO）；阶段 3 验收（GUI：未审核文章不可见已由 API 实测覆盖；回调建单已实测）。

下一步（回复"继续"后）：**阶段 3 科普内容 + 社区**
- 文章模块（分类、来源、审核流、发布）+ 后台审核接口
- Discourse Docker 配置；DiscourseConnect SSO（HMAC-SHA256 签名校验）；社区发帖关键词预警回调
- 验收：未审核文章前台不可见；社区回调命中关键词生成预警工单

## 遗留问题

| 编号 | 事项 | 状态 |
|---|---|---|
| L2 | `/docs/TODO.md` 未创建 | **已解决**（1.1 建立，8 条待确认 + 2 条已归档） |
| L6 | risk-list.md 中 8 条"需人工确认"事项 | 已正式入 TODO.md（T001–T008），按截止时点确认 |
| L7 | 本机 5.7.44 服务实例的账号密码未提供（真实开发库连接用） | 待用户提供（写 `.env`，环境变量名 `ConnectionStrings__Default`） |

## 下一步

1. 阶段 1 已全部完成并通过验收（含 PROJECT_BRIEF 指定的 web-gui-tester 危机用例）。
2. 用户确认阶段总结后进入阶段 2：账号 + 情绪日记 + 后台（JWT / 密文日记 / admin 骨架）。

## 阶段计划对照

| 阶段 | 内容 | 状态 |
|---|---|---|
| 0 | 规划：0.1 / 0.2 / 0.3 | **完成** |
| 1 | 骨架 + 量表 + 危机提示（1.1–1.5 + 验收） | **完成** |
| 2 | 账号 + 情绪日记 + 后台（2.1–2.5 + 验收） | **完成** |
| 3 | 科普内容 + 社区 | **完成**（提交 5e4575b：后端实测 + 3.3 前端页 + deploy/discourse 配置 + .env.example） |
| 4 | 咨询预约 | **完成**（后端 855a118 并发实测 + /web 预约页） |
| 5 | AI 陪伴 | **完成**（后端 b70094f + 前端 51308fa：7101 拒答/限流/审计实测） |
| 6 | 部署与合规收尾 | **完成**（2292239 compose/Nginx/备份；0dcc4d9 合规 docx 草稿；ee65aae 安全自查+README；mock 演示已上线 https://light-mock-demo-xdr1.zai.site） |
