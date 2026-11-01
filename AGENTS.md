# 技术栈（固定，不得擅自更换；确需更换必须先说明理由并等我同意）
后端：
- .NET 10，ASP.NET Core Web API（Controller 风格）
- 数据访问：Dapper + MySqlConnector，MySQL 8，字符集 utf8mb4
- 数据库迁移：纯 SQL 脚本，按序号命名放在 /backend/db/migrations，启动时由迁移工具（自写或 DbUp）顺序执行
- Redis（StackExchange.Redis）用于缓存与验证码；接口限流用 ASP.NET Core 内置 RateLimiter
- 鉴权：JWT Bearer（访问令牌 + 刷新令牌）；密码哈希用 BCrypt 或 PBKDF2
- 加密：System.Security.Cryptography.AesGcm
- 日志：Serilog；接口文档：内置 OpenAPI + Scalar UI
- 参数校验：FluentValidation
- 后台任务：BackgroundService
- 测试：xUnit

前端（前台 /web 与后台 /admin，两个独立项目）：
- Vue 3（Composition API，`<script setup>`）+ TypeScript + Vite
- Pinia + Vue Router + Axios
- UI：Element Plus；图表：ECharts

部署：Docker Compose + Nginx；社区使用 Discourse 官方镜像方案。

说明：具体版本以 NuGet / npm 实际可用的最新稳定版为准。不确定某个包是否支持 .NET 10 时，先查证，不要猜。

# 硬性规则（任何阶段都不得违反）
1. 不做诊断。结果文案只能用"提示 / 建议就医 / 建议咨询专业人员"，禁止出现"你患有抑郁症"之类表述；结果页必须有免责声明。
2. 危机干预必须实现：
   - 触发条件：PHQ-9 第 9 题得分 ≥1；日记、社区发帖、AI 对话命中自伤自杀关键词。
   - 动作：立即展示求助页，写入预警工单并通知后台；AI 对话改用固定安全话术并转介，不再调用模型。
   - 求助热线号码一律做成可配置项，不得凭记忆写任何具体号码，用占位符并标注"上线前人工核实"。
3. 敏感数据：日记正文、量表答案在应用层 AES-GCM 加密后入库；密钥走环境变量，不得进入代码仓库；日志不得输出这些字段明文。
4. 隐私：首次使用需单独勾选同意隐私政策；提供账号注销、个人数据导出与删除；数据收集最小化。
5. 不内置有版权限制的量表（如 BDI）。量表题目、选项、计分规则、分级规则、预警规则全部放 JSON 配置，新增量表不改代码。
6. AI 陪伴在界面和系统提示词中必须声明"我不是医生或心理治疗师"，不诊断、不建议用药。
7. 内容类页面必须有"信息来源 / 审核人 / 更新时间"。
8. 不确定的事项（法规、热线、资质要求）写入 /docs/TODO.md 并标注"需人工确认"，不得编造。
9. 密钥、密码、API Key 一律不硬编码；提供 .env.example 模板，真实 .env 加入 .gitignore。

# 统一约定
- 后端分层：Controllers / Services / Repositories / Models（Entity、DTO） / Infrastructure（加密、Redis、Jwt） / Jobs。
- 统一返回体：{ code, message, data, traceId }；业务错误码集中定义。
- 所有接口默认需鉴权，匿名接口（如量表作答、科普浏览）显式标注。
- 目录：/backend /web /admin /deploy /docs /scripts，README.md 放根目录。
- 前端请求统一封装，错误统一拦截；路由按模块拆分；组件命名 PascalCase。
- 视觉风格：柔和、低刺激、浅暖色系，字号偏大，避免高饱和红色警示；危机入口在主要页面显眼可见，但不制造惊吓感。

# 工作方式（非常重要，严格遵守）
A. 先建立两个"记忆文件"，之后每个阶段、每个新会话开始前都必须先重新读取它们：
   - /docs/CONVENTIONS.md：技术栈、目录结构、命名规范、统一返回体、错误码规则、硬性规则摘要。
   - /docs/PROGRESS.md：已完成步骤、当前步骤、遗留问题、下一步。每完成一步立即更新。
B. 严格按"阶段 → 步骤"执行。每个步骤最多生成 3 个文件，生成后立即执行编译 / 构建 / 测试，并贴出真实输出。命令失败必须先修复；失败状态下不得进入下一步，也不得在未运行的情况下声称"已通过"。
C. 禁止出现"此处省略""同上""其余类似"等写法；每个文件必须完整。文件过长时可分多次写入同一文件，但最终必须完整。
D. 先定契约再写实现：先写表结构（SQL）与接口清单（路径、请求、响应），再写代码。
E. 每个阶段结束输出"阶段总结"（已完成、如何运行验证、遗留问题）后停止，等我回复"继续"。
F. 需求不明确时，列出你的假设并继续；只有重大分歧才提问，且一次最多问一个问题。
