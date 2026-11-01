# 心灯（Light）项目约定 — CONVENTIONS.md

> 本文件是项目"记忆文件"之一。每个阶段、每个新会话开始前，必须先重新读取本文件与 [PROGRESS.md](./PROGRESS.md)。
> 修改本文件中的任何约定，必须先说明理由并征得同意后才能改。

## 1. 项目概况

- 项目名：心灯（Light）。Slogan：在情绪的夜里，留一盏灯。
- 定位：抑郁情绪科普与自助支持网站，可落地部署。
- 产品边界：科普 + 自助工具 + 转介支持。**不做医疗诊断，不替代专业治疗。**
- 完整需求与阶段计划见 [PROJECT_BRIEF.md](./PROJECT_BRIEF.md)，硬性规则原文见根目录 [AGENTS.md](../AGENTS.md)。

## 2. 技术栈（固定，不得擅自更换）

确需更换必须先说明理由并征得同意。具体版本以 NuGet / npm 实际可用的最新稳定版为准；不确定某个包是否支持 .NET 10 时，先查证，不要猜。

### 后端
- .NET 10，ASP.NET Core Web API（Controller 风格）
- 数据访问：Dapper + MySqlConnector，MySQL，字符集 utf8mb4。**环境决定（2026-11-01，用户确认）：开发环境使用本机 MySQL 5.7.44（Windows 服务），SQL 以 5.7 为最低兼容基线——不使用 CTE、窗口函数等 8 专属语法，排序规则统一 `utf8mb4_unicode_ci`（5.7/8 双兼容）；部署目标仍为 MySQL 8（Docker Compose，阶段 6）。CHECK 约束在 5.7 中被解析但不生效，相关校验由应用层（FluentValidation）承担**
- 数据库迁移：纯 SQL 脚本，按序号命名放在 `/backend/db/migrations`，启动时由迁移工具（自写或 DbUp）顺序执行
- Redis（StackExchange.Redis）：缓存与验证码；接口限流用 ASP.NET Core 内置 RateLimiter
- 鉴权：JWT Bearer（访问令牌 + 刷新令牌）；密码哈希用 BCrypt 或 PBKDF2
- 加密：System.Security.Cryptography.AesGcm
- 日志：Serilog；接口文档：内置 OpenAPI + Scalar UI
- 参数校验：FluentValidation
- 后台任务：BackgroundService
- 测试：xUnit

### 前端（/web 与 /admin 两个独立项目）
- Vue 3（Composition API，`<script setup>`）+ TypeScript + Vite
- Pinia + Vue Router + Axios
- UI：Element Plus；图表：ECharts

### 部署
- Docker Compose + Nginx；社区使用 Discourse 官方镜像方案。

## 3. 目录结构

```
/
├── AGENTS.md                  # 硬性规则与工作方式（最高优先级）
├── README.md                  # 如何运行（阶段 6 收尾时必须完整）
├── docs/
│   ├── PROJECT_BRIEF.md       # 需求与阶段计划
│   ├── CONVENTIONS.md         # 本文件
│   ├── PROGRESS.md            # 进度记忆文件
│   ├── TODO.md                # 待人工确认事项（首次出现待确认事项时创建）
│   ├── er-design.md           # 阶段 0.2：ER 设计
│   ├── api-spec.md            # 阶段 0.3：接口清单
│   ├── crisis-flow.md         # 阶段 0.3：危机干预流程图
│   ├── risk-list.md           # 阶段 0.3：风险清单
│   └── legal/                 # 阶段 6：隐私政策 / 用户协议 / 免责声明草稿
├── backend/
│   ├── Light.sln
│   ├── src/
│   │   └── Light.Api/         # ASP.NET Core Web API 主项目
│   │       ├── Controllers/
│   │       ├── Services/
│   │       ├── Repositories/
│   │       ├── Models/        # Entity（内部实体）与 Dto（对外契约）分开放
│   │       ├── Infrastructure/ # 加密、Redis、Jwt 等横切设施
│   │       ├── Jobs/          # BackgroundService 后台任务
│   │       └── Common/        # 统一返回体、错误码、验证器等
│   ├── tests/
│   │   └── Light.Api.Tests/   # xUnit
│   └── db/
│       └── migrations/        # NNNN_描述.sql，按序号执行
├── web/                       # 前台（Vue 3）
├── admin/                     # 后台管理（Vue 3）
├── deploy/                    # docker-compose.yml、Nginx 配置、备份恢复脚本
└── scripts/                   # 开发辅助脚本
```

## 4. 命名规范

### 4.1 后端 C#
- 类型、方法、属性：PascalCase；局部变量：camelCase；私有字段：`_camelCase`；接口：`I` 前缀。
- 异步方法加 `Async` 后缀；校验器类名 `XxxValidator`；服务接口 `IXxxService` + 实现 `XxxService`。
- 控制器 `XxxController`，一个资源一个控制器；DTO 按用途命名：`XxxRequest` / `XxxResponse`。

### 4.2 数据库（开发基线 MySQL 5.7 / 部署目标 MySQL 8，utf8mb4）
- 库名、表名、字段名：小写 snake_case；表名用复数（users、scales、scale_submissions、diaries、articles…）。
- 排序规则统一 `utf8mb4_unicode_ci`（5.7 / 8.0 双兼容）；CHECK 约束 5.7 不生效，应用层必须校验。
- 主键：`id BIGINT UNSIGNED AUTO_INCREMENT`；需对外暴露且防枚举的实体，另加公开标识列（`public_id`，UUID），阶段 0.2 定稿。
- 审计字段：每表必有 `created_at`、`updated_at`（DATETIME(3)，存 UTC）；支持删除的业务表加 `deleted_at` 软删除。
- 布尔字段 `is_*` / `has_*`；时间字段以 `_at` 结尾。
- 索引：`idx_表名_列名`；唯一索引：`uk_表名_列名`；外键：`fk_表名_引用表`。
- 敏感字段（日记正文、量表答案等）：以 VARBINARY/BLOB 存 AES-GCM 密文，字段长度按密文长度（nonce + 密文 + tag）预留。
- 迁移脚本：`/backend/db/migrations/NNNN_简短描述.sql`（如 `0001_init.sql`），按序号顺序执行，必须幂等可重复执行（`CREATE TABLE IF NOT EXISTS` 等）。

### 4.3 API 接口
- 路由：`/api/{模块}/{资源}`，资源名复数、kebab-case，例如 `/api/scales`、`/api/scale-submissions`。
- **默认需要鉴权**；匿名接口必须显式 `[AllowAnonymous]`，并在接口清单中标注"匿名"。
- 分页参数：`page`（从 1 起）、`pageSize`（默认 20，最大 100）。
- 时间统一传 UTC（ISO 8601），由前端负责本地化显示。

### 4.4 前端
- 组件：PascalCase 文件名与组件名（`CrisisHelpPage.vue`）；composable：`useXxx.ts`。
- 路由：按模块拆分到 `router/modules/*.ts`；`path` 用 kebab-case，`name` 用 PascalCase。
- Pinia store：`defineStore('模块名', ...)`，每模块一个文件放 `stores/`。
- 请求统一封装（`request.ts`）：统一注入 token、统一拦截错误——非 0 业务码弹统一提示，401 跳登录，403 提示无权限；页面必须处理加载、空、错误三种状态。
- 图表统一用 ECharts，封装公共组件。

### 4.5 配置与环境变量
- 环境变量：UPPER_SNAKE_CASE（如 `DB_PASSWORD`、`JWT_SECRET`、`AES_DATA_KEY`、`REDIS_PASSWORD`、`AI_API_KEY`、`CRISIS_HOTLINES`）。
- **密钥、密码、API Key 一律不硬编码**；维护 `.env.example` 模板，真实 `.env` 加入 `.gitignore`。
- 危机求助热线：一律做成可配置项（`CRISIS_HOTLINES`，JSON 数组：名称 + 号码 + 说明），代码与配置中只允许占位符，并标注"上线前人工核实"，**不得凭记忆写任何具体号码**。

## 5. 统一返回体

所有接口统一返回：

```json
{
  "code": 0,
  "message": "ok",
  "data": {},
  "traceId": "自动生成的链路 ID"
}
```

- `code = 0` 表示成功；非 0 为业务错误码（见下节）。
- `message` 面向用户可读；**异常响应不得泄露堆栈与内部细节**，堆栈只进 Serilog 日志。
- `traceId` 同时写入响应头 `X-Trace-Id`，贯穿 Serilog 日志，便于排查。
- 分页数据放 `data`：`{ "items": [], "total": 0, "page": 1, "pageSize": 20 }`。

## 6. 业务错误码规则

错误码集中定义在 `Common/ErrorCodes.cs`（骨架搭建时落位），禁止在业务代码里散落魔法数字。

分段规则（千位段表示模块）：

| 分段 | 模块 |
|---|---|
| 1xxx | 通用（参数错误、资源不存在、限流等） |
| 2xxx | 认证与账号 |
| 3xxx | 量表与危机 |
| 4xxx | 情绪日记 |
| 5xxx | 科普内容 |
| 6xxx | 咨询预约 |
| 7xxx | AI 陪伴 |
| 8xxx | 后台管理与审核 |
| 9xxx | 系统与任务 |

首批基线码（后续随模块扩展，新增必须追加到 ErrorCodes.cs）：

| code | 含义 |
|---|---|
| 0 | 成功 |
| 1000 | 参数校验失败 |
| 1001 | 资源不存在 |
| 1002 | 请求过于频繁（限流） |
| 1003 | 服务器内部错误（message 对外统一为"服务暂不可用"） |
| 2001 | 未登录或令牌无效 |
| 2002 | 令牌已过期 |
| 2003 | 权限不足 |
| 2004 | 账号已存在 |
| 2005 | 账号或密码错误 |
| 2006 | 账号已注销或禁用 |
| 2007 | 验证码无效或已过期 |
| 3001 | 量表不存在或未启用 |
| 3002 | 量表答案格式不合法 |
| 3101 | 答案命中危机预警（触发求助页 + 预警工单） |
| 4001 | 日记不存在 |
| 4002 | 当日日记已存在（请走更新） |
| 5001 | 文章不存在或未发布 |
| 5002 | 当前状态不允许该操作（审核流等） |
| 6001 | 可约时段已被预约 |
| 6002 | 预约不存在或状态不允许该操作 |
| 7001 | AI 会话超过频率限制 |
| 7101 | AI 拒答：命中危机，返回固定安全话术（不调用模型） |
| 8001 | 管理员账号或密码错误 |

HTTP 状态码照常正确使用（401/403/404/429/500），body 中 `code` 表达业务语义，两者不混用。

## 7. 硬性规则摘要（合规红线，任何阶段不得违反）

> 原文见 AGENTS.md，此处为执行摘要；摘要与原文冲突时以原文为准。

1. **不做诊断**：结果文案只能用"提示 / 建议就医 / 建议咨询专业人员"，禁止"你患有抑郁症"类表述；结果页必须有免责声明。
2. **危机干预**：触发条件——PHQ-9 第 9 题 ≥1 分；日记 / 社区发帖 / AI 对话命中自伤自杀关键词。触发动作——立即展示求助页、写入预警工单并通知后台；AI 对话改用固定安全话术并转介，不再调用模型。热线号码全部可配置、用占位符并标注"上线前人工核实"。
3. **敏感数据加密**：日记正文、量表答案在应用层 AES-GCM 加密后入库；密钥走环境变量，不进代码仓库；日志不得输出这些字段明文。
4. **隐私**：首次使用单独勾选同意隐私政策；提供账号注销、个人数据导出与删除；数据收集最小化。
5. **量表配置化**：不内置有版权限制的量表（如 BDI）；题目、选项、计分、分级、预警规则全部放 JSON 配置，新增量表不改代码。
6. **AI 陪伴声明**：界面与系统提示词必须声明"我不是医生或心理治疗师"，不诊断、不建议用药。
7. **内容标注**：内容类页面必须有"信息来源 / 审核人 / 更新时间"。
8. **不确定事项**（法规、热线、资质）：写入 `/docs/TODO.md` 并标注"需人工确认"，不得编造。
9. **密钥管理**：不硬编码；提供 `.env.example`；真实 `.env` 加入 `.gitignore`。

## 8. 视觉与文案约定

- 柔和、低刺激、浅暖色系；字号偏大；避免高饱和红色警示。
- 危机入口在主要页面显眼可见，但不制造惊吓感。
- 结果类文案用"提示 / 建议就医 / 建议咨询专业人员"；AI 界面常驻"非医生 / 非治疗师"声明。

## 9. 工作方式（摘要，原文见 AGENTS.md）

- 每个阶段、每个新会话开始前，先读 CONVENTIONS.md 与 PROGRESS.md。
- 严格按"阶段 → 步骤"执行；每个步骤最多生成 3 个文件，生成后立即编译 / 构建 / 测试并贴真实输出；失败必须先修复，失败状态下不得进入下一步。
- 禁止"此处省略 / 同上 / 其余类似"；每个文件必须完整。
- 先定契约再写实现：先写表结构（SQL）与接口清单（路径、请求、响应），再写代码。
- 每个阶段结束输出"阶段总结"后停止，等用户回复"继续"。
- 需求不明确时列出假设继续；重大分歧才提问，一次最多一个。
- 每步结束按 PROJECT_BRIEF 的自查表逐项自查，结果写进步骤总结。
