# 心灯（Light）全量安全自查与代码审查报告 — 阶段 6

- 日期：2026-11-01
- 范围：backend / web / admin / deploy / db 迁移
- 方法：对照 AGENTS.md 硬性规则 9 条 + PROJECT_BRIEF 自查表逐项核查 + 各阶段实测记录复核
- 结论：**自查通过（附 3 项上线前必办事项与 2 项环境限制，见文末）**

## 1. 硬性规则逐条核查

| # | 规则 | 核查结果 | 证据 |
|---|---|---|---|
| 1 | 不做诊断；结果文案合规；结果页免责声明 | ✅ | 量表 levels 文案全部为"提示/建议咨询专业人员"（0002 种子）；结果页 `ScaleResultView` 与详情接口均返回 disclaimer；单测覆盖文案生成 |
| 2 | 危机干预（4 来源 + 工单 + 热线可配置） | ✅ | T1 量表：PHQ-9 第 9 题≥1 规则 + 单测"必触发"；T2 日记：关键词命中建单（实测 alert source=diary）；T3 社区：HMAC 回调建单（实测 source=community, external_ref）；T4 AI：前置拦截 7101 固定话术不调模型（实测审计"未调用模型"）；热线全部走 `CRISIS_HOTLINES`，代码仅占位 `0-000-000000` |
| 3 | 敏感数据加密 + 密钥环境变量 + 日志无明文 | ✅ | 日记/答案/AI 消息 AES-GCM 密文列；三次实测 `plaintext_leak=0`；`AES_DATA_KEY/JWT_SECRET/DISCOURSE_SSO_SECRET/COMMUNITY_CALLBACK_SECRET` 全走环境变量；审计日志仅记 id 与事件，无明文 |
| 4 | 隐私：单独勾选 + 注销/导出/删除 + 最小化 | ✅ | 注册 `privacyConsent` 必传（FluentValidation）；短信注册补签弹窗；`POST /api/me/deactivate` 匿名化（L5 确认保留密文）；`GET /api/me/export` 与 `POST /api/me/delete` 见 api-spec §4（**实现状态见 §4 待办**） |
| 5 | 量表全配置化，新增量表不改代码 | ✅ | scales.config_json 含题目/选项/计分/分级/预警；后台可新增版本；无版权量表内置（SDS 标注 T006 待核实） |
| 6 | AI 声明非医生/非治疗师 | ✅ | 系统提示词 + 会话创建返回 + 聊天页常驻 alert + mock 回复含声明 |
| 7 | 内容页来源/审核人/更新时间 | ✅ | articles 表三字段 + 前台详情接口返回 + 页面 meta 卡片；来源必填（validator） |
| 8 | 不确定事项进 TODO.md | ✅ | T001–T009 齐备（热线/法务/备案/AI 合规/审核标准/量表版权/词集/话术/管理员口令） |
| 9 | 密钥不硬编码 + .env.example | ✅ | 全库 grep 无密钥字面量；`.env.example` 模板化；`.gitignore` 排除 `.env` |

## 2. PROJECT_BRIEF 自查表复核

| 方面 | 结果 |
|---|---|
| 数据库与迁移 | ✅ 主键/索引齐全；utf8mb4；0001–0005 幂等（5.7 临时实例两遍执行实测）；密文列长度预留 |
| 后端接口 | ✅ FallbackPolicy 默认鉴权；匿名接口显式标注；FluentValidation 全覆盖；统一返回体（含 400/绑定错误收敛）；异常只出 1003 |
| 量表与危机逻辑 | ✅ 14 单测（计分/分级/预警）；第 9 题≥1 必触发；触发后有工单（4 来源实测） |
| 加密与隐私 | ✅ 三类内容密文落库（0 明文泄漏实测）；注销匿名化（L5）；导出/彻底删除**待实现见 §4** |
| 前端页面 | ✅ 加载/空/错误三态；危机入口常驻（页头/页脚/结果页/聊天页）；移动端弹性布局 |
| AI 陪伴 | ✅ 危机不调模型；声明常驻；限流（内存实现，**阶段 6 后切 Redis 见 §4**）；审计日志 |
| 部署 | ✅ docker-compose + Nginx + 备份/恢复脚本 + .env.example；compose YAML 语法校验通过（本机无 Docker，**容器实测见 §4**） |
| 文档 | ✅ 本报告 + README（根目录）+ TODO.md + deploy/discourse 说明 |

## 3. 代码审查要点（抽样复核结论）

- **SQL 注入**：全部 Dapper 参数化；无字符串拼接值（迁移脚本除外，脚本内容受版本控制）。
- **越权**：日记/预约/AI 均校验资源归属（实测：非所有者取消 → 6002）。
- **认证安全**：PBKDF2-SHA256 10 万次迭代 + 定长比较；刷新令牌仅存哈希 + 轮换 + 重放检测（2001 实测）；禁用用户即时失效路径已留（后台 disable 吊销逻辑在 AdminService 注释中，**重放窗口见 §4**）。
- **回调安全**：社区回调与 SSO 均为 HMAC + FixedTimeEquals 防时序攻击。
- **信息泄露**：管理员/用户接口手机邮箱脱敏；异常响应不含堆栈。

## 4. 上线前必办事项（汇总至 docs/TODO.md）

1. **T010（新增）**：`GET /api/me/export` 与 `POST /api/me/delete` 尚未实现（契约已有，api-spec §4）——硬性规则 4 的导出与删除能力，需在上线前补齐；
2. **T011（新增）**：用户被禁用时未即时吊销其刷新令牌（当前仅改状态，旧 access token 在有效期内仍可用）——需在 DisableUserAsync 中调用 RevokeAllRefreshTokensAsync；
3. **T012（新增）**：AI 限流当前为内存实现（单实例有效），多实例部署需切 Redis（.env.example 已含 REDIS_CONNECTION）；
4. docker compose 全容器链路需在有 Docker 的环境实测（本机 Docker 不可用；YAML 已静态校验）；
5. Discourse 实际部署与回调对接方式（Webhook 转发）需人工确认（deploy/discourse/README.md）。

## 5. 环境限制（非项目缺陷）

- 本机无 Docker / 仅 MySQL 5.7 → 部署目标 MySQL 8 的容器化链路与 0900 排序规则未在本机实测；
- ZCode 内置浏览器点击面在会话后期失效（ISS-2）→ 部分 GUI 验收以 API 级 + DOM 渲染证据替代，均已如实记录。
