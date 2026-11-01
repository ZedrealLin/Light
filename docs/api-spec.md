# 心灯（Light）接口清单 — 阶段 0.3 契约

- 约定依据：[CONVENTIONS.md](./CONVENTIONS.md) 第 4.3 / 5 / 6 节（路由、统一返回体、错误码）
- 状态：**契约文档**。实现前以本文件为准；开发中如需调整路径 / 字段，**先改这里再写代码**，并同步错误码表。
- 本清单覆盖阶段 1–5 的规划接口；进入各阶段实现前，按该阶段需求细化字段（路径与语义尽量不改）。

## 0. 通用约定速览

- 统一返回体：`{ code, message, data, traceId }`，`code=0` 成功；`traceId` 同时写入响应头 `X-Trace-Id`。
- 默认**需要鉴权**（JWT Bearer）；下表"匿名"= 显式 `[AllowAnonymous]`。
- 分页参数：`page`（≥1）、`pageSize`（默认 20，最大 100）；分页响应 `data = { items, total, page, pageSize }`。
- 时间：请求与响应一律 UTC ISO 8601，前端本地化。
- **危机命中不作为错误**：量表 / 日记 / AI 相关接口命中危机时仍返回 `code=0`，通过 `data.crisis` 非空触发前端求助页；错误码 `3101` / `7101` 分别用于审计语义与 AI 拒答响应。
- 请求体验证：FluentValidation；5.7 不强制 CHECK 约束，**一切业务校验以应用层为准**。

## 1. 系统与健康（阶段 1）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| GET `/api/health` | 匿名 | — → `{ status: "ok", db, redis }` | — | 1 |

## 2. 量表与作答（阶段 1）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| GET `/api/scales` | 匿名 | — → `items[{ code, name, description, questionCount, estimatedMinutes }]`（仅启用量表，不含题目明细） | — | 1 |
| GET `/api/scales/{code}` | 匿名 | — → 量表对外投影：`{ code, name, questions: [{ no, title, options: [{ value, label }] }] }`。**不返回计分 / 分级 / 预警规则**（防作弊且属配置机密） | 3001 | 1 |
| POST `/api/scale-submissions` | 匿名（可带 token） | `{ scaleCode, answers: [{ no, value }] }` → `{ submissionPublicId, totalScore, levelCode, levelLabel, disclaimer, crisis?: { helpUrl, hotlines } }`。命中预警规则时 `crisis` 非空 + 服务端已建工单；答案密文落库 | 3001 3002 | 1 |
| GET `/api/scale-submissions/{publicId}` | 匿名 | — → 同提交响应的结果部分（结果非敏感明文，可凭 publicId 取回） | 1001 | 1 |
| GET `/api/me/scale-submissions` | 鉴权 | 分页 → 我的作答历史（`totalScore/levelCode/createdAt`，不含答案明细） | 2001 | 2 |

## 3. 危机求助（阶段 1，流程见 [crisis-flow.md](./crisis-flow.md)）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| GET `/api/crisis/help` | 匿名 | — → `{ disclaimer, hotlines: [{ name, number, note }], advice }`（热线读配置 `CRISIS_HOTLINES`，代码仅占位符，上线前人工核实） | — | 1 |
| POST `/api/crisis/community-callback` | 匿名 + HMAC 签名头 | Discourse 发帖回调：`{ topicId, postId, username, externalUserId, content }`；命中关键词建工单（`external_ref` 存 Discourse 引用） | 1000 | 3 |

## 4. 认证与账号（阶段 2）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| POST `/api/auth/register` | 匿名 | `{ account, password, nickname?, privacyConsent: true }` → `{ accessToken, refreshToken, user }`；`privacyConsent` 必须为 true（硬性规则 4） | 2004 2007 | 2 |
| POST `/api/auth/login` | 匿名 | `{ account, password }` → 令牌对 | 2005 2006 | 2 |
| POST `/api/auth/sms-code` | 匿名 | `{ phone }` → 发送验证码（Redis 存储 + 限流；阶段 2 为 mock） | 1002 | 2 |
| POST `/api/auth/login/sms` | 匿名 | `{ phone, code }` → 令牌对（无账号则自动注册并要求同意隐私政策） | 2007 2004 | 2 |
| POST `/api/auth/refresh` | 匿名 | `{ refreshToken }` → 新令牌对（轮换，旧 token 撤销） | 2001 2002 | 2 |
| POST `/api/auth/logout` | 鉴权 | `{ refreshToken }` → 撤销 | 2001 | 2 |
| GET `/api/me` | 鉴权 | — → `{ publicId, nickname, avatarUrl, phoneMasked, emailMasked, privacyConsentAt }`（敏感字段脱敏） | 2001 | 2 |
| PUT `/api/me` | 鉴权 | `{ nickname?, avatarUrl? }` → 更新 | 2001 | 2 |
| POST `/api/me/privacy-consent` | 鉴权 | — → 记录同意（`privacy_consent_at/version`） | 2001 | 2 |
| GET `/api/me/export` | 鉴权 | — → 个人数据导出（JSON：资料 / 作答结果 / 日记明文仅本人口令触发后生成；硬性规则 4） | 2001 | 2 |
| POST `/api/me/deactivate` | 鉴权 | `{ passwordOrCode }`（二次确认）→ **注销**：匿名化、保留密文（2026-11-01 已确认） | 2001 2005 | 2 |
| POST `/api/me/delete` | 鉴权 | `{ passwordOrCode }`（二次确认，导出先行提示）→ **删除我的数据**：物理删除，独立入口 | 2001 2005 | 2 |
| GET `/api/me/notifications` | 鉴权 | 分页 → 站内通知 | 2001 | 4 |
| POST `/api/me/notifications/{id}/read` | 鉴权 | — → 标记已读 | 1001 | 4 |

## 5. 情绪日记（阶段 2）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| GET `/api/diaries?from&to&page` | 鉴权 | 分页 → `items[{ id, entryDate, moodScore, tags, hasCrisis }]`（**列表不含正文**） | 2001 | 2 |
| POST `/api/diaries` | 鉴权 | `{ entryDate, moodScore(1-5), content, tags[] }` → `{ id, crisis? }`；当日已存在返回 4002；正文密文落库 | 4002 | 2 |
| GET `/api/diaries/{id}` | 鉴权 | — → 详情含正文（服务端解密，仅本人） | 1001 2003 | 2 |
| PUT `/api/diaries/{id}` | 鉴权 | 同 POST → 覆盖更新（软删除后同日期重建走恢复 / 覆盖逻辑） | 1001 2003 | 2 |
| DELETE `/api/diaries/{id}` | 鉴权 | — → 软删除（`deleted_at`） | 1001 | 2 |
| GET `/api/diaries/trend?days=30` | 鉴权 | — → `[{ date, moodScore }]`（趋势图数据，明文最小字段） | 2001 | 2 |
| GET `/api/diaries/reminder` | 鉴权 | — → `{ enabled, remindTime, daysOfWeek }` | 2001 | 2 |
| PUT `/api/diaries/reminder` | 鉴权 | 同上结构 → 更新提醒设置 | 1000 | 2 |

## 6. 科普内容（阶段 3）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| GET `/api/article-categories` | 匿名 | — → `items[{ id, name, slug }]`（仅启用） | — | 3 |
| GET `/api/articles?categoryId&page` | 匿名 | 分页 → 仅 `published`：`items[{ slug, title, summary, categoryName, publishedAt }]` | — | 3 |
| GET `/api/articles/{slug}` | 匿名 | — → `{ title, content, sourceInfo, reviewerName, publishedAt, updatedAt, disclaimer }`（信息来源 / 审核人 / 更新时间——硬性规则 7） | 5001 | 3 |

## 7. 咨询预约（阶段 4）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| GET `/api/counselors` | 匿名 | — → 上架且审核通过的咨询师：`items[{ id, name, title, specialties, avatarUrl }]` | — | 4 |
| GET `/api/counselors/{id}` | 匿名 | — → 档案（资质展示：`title` + `licenseNo` 掩码，如 `12****89`） | 1001 | 4 |
| GET `/api/counselors/{id}/slots?from&to` | 匿名 | — → `items[{ id, startAt, endAt }]`（仅 `available`） | 1001 | 4 |
| POST `/api/bookings` | 鉴权 | `{ slotId }` → `{ bookingId, startAt }`；时段原子扣减 + `uk_bookings_active_slot` 兜底 | 6001 2001 | 4 |
| GET `/api/bookings/my` | 鉴权 | 分页 → 我的预约（含状态） | 2001 | 4 |
| POST `/api/bookings/{id}/cancel` | 鉴权 | `{ reason? }` → 取消并释放时段 → 站内通知 | 6002 2003 | 4 |

## 8. AI 陪伴（阶段 5）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| POST `/api/ai/sessions` | 鉴权 | — → `{ sessionId, disclaimer }`（`disclaimer` 固定含"我不是医生或心理治疗师"） | 2001 | 5 |
| POST `/api/ai/sessions/{id}/messages` | 鉴权 | `{ content }` → **前置拦截**：命中危机关键词 → `code=7101` + `data = { safeReply, helpUrl, hotlines }`（**不调用模型**）；未命中 → `{ reply }`；限流 7001；消息密文落库 | 7101 7001 1001 | 5 |
| GET `/api/ai/sessions/{id}/messages` | 鉴权 | 分页 → 历史（服务端解密，仅本人） | 1001 2003 | 5 |
| GET `/api/ai/sessions` | 鉴权 | 分页 → 会话列表 | 2001 | 5 |

## 9. 后台管理 `/api/admin/*`（阶段 2–4，全部管理员鉴权）

| 方法 路径 | 鉴权 | 请求 → 响应 | 错误码 | 阶段 |
|---|---|---|---|---|
| POST `/api/admin/auth/login` | 匿名 | `{ username, password }` → 管理员令牌（独立凭据体系） | 8001 | 2 |
| GET `/api/admin/crisis-alerts?status&page` | 管理员 | 分页 → 预警工单列表（来源筛选） | 2003 | 1 |
| POST `/api/admin/crisis-alerts/{id}/handle` | 管理员 | `{ action: processing/resolved/dismissed, note }` → 状态流转（见 crisis-flow） | 1000 | 1 |
| GET `/api/admin/scales` / POST `/api/admin/scales` | 管理员 | 列表 / 新增（code + name + config_json） | 1000 | 1 |
| PUT `/api/admin/scales/{id}/config` | 管理员 | `{ configJson, activate? }` → 新版本配置（版本号递增，不改历史） | 3001 1000 | 1 |
| GET / POST / DELETE `/api/admin/crisis-keywords` | 管理员 | 关键词管理（词表更新即时生效，见 crisis-flow 缓存策略） | 1000 | 1 |
| GET `/api/admin/users?query&page` / POST `/api/admin/users/{id}/disable` | 管理员 | 用户查询 / 禁用 | 2003 | 2 |
| POST `/api/admin/articles` / PUT `/{id}` / POST `/{id}/submit` / POST `/{id}/review` / POST `/{id}/offline` | 管理员 | 文章草稿 → 提交 → 审核（通过 / 驳回 + note）→ 发布 / 下线；审核人自动记录 | 5002 | 3 |
| POST /api/admin/article-categories | 管理员 | 分类管理（CRUD，路径同风格） | 1000 | 3 |
| POST `/api/admin/counselors` / PUT `/{id}` / POST `/{id}/review` | 管理员 | 咨询师档案与资质审核 | 5002 同语义复用为状态错误 | 4 |
| POST `/api/admin/counselors/{id}/slots` / DELETE `/api/admin/slots/{id}` | 管理员 | 批量排班 / 关闭时段（已有有效预约的时段不可删，返回 6002） | 6002 | 4 |
| GET `/api/admin/bookings?counselorId&page` / POST `/{id}/complete` / POST `/{id}/no-show` | 管理员 | 预约管理 | 6002 | 4 |

## 10. 契约细化与变更规则

1. 每个阶段开工前：以本清单为基线，在 PR / 步骤总结中列出"本阶段细化的字段差异"，**不改路径语义**。
2. 新增错误码必须先追加 [CONVENTIONS.md](./CONVENTIONS.md) 第 6 节，再在代码 `ErrorCodes.cs` 落位。
3. 匿名接口仅限本清单标注"匿名"者；新接口默认鉴权。
