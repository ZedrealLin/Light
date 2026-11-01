# Discourse（社区）部署配置说明 — deploy/discourse/

> Discourse 官方镜像**不使用 docker-compose**，采用其自带的 `discourse_docker` 启动器
> （git clone https://github.com/discourse/discourse_docker && ./discourse-setup）。
> 本目录提供独立部署的参数样例与心灯侧对接说明。**部署前需人工确认服务器规格**（Discourse 官方建议 ≥2GB 内存，见 risk-list T3）。

## 1. 部署步骤概览（需人工确认后执行）

1. 在独立主机（或与主站分离的容器/端口）克隆 discourse_docker；
2. `cp samples/standalone.yml containers/light-community.yml`，按下文修改；
3. `./launcher start light-community`（首次 `bootstrap`，需 10~30 分钟）；
4. 进入 Discourse 管理后台 → 设置 → 启用 `enable_discourse_connect`（DiscourseConnect / 原 SSO）；
5. 填写 `discourse_connect_url` = `https://<主站域名>/api/community/sso`，`discourse_connect_secret` = 与主站一致的密钥。

## 2. light-community.yml 关键参数（样例见 containers.yml.sample）

| 参数 | 说明 |
|---|---|
| `DISCOURSE_HOSTNAME` | 社区域名，如 community.example.com |
| `DISCOURSE_DEVELOPER_EMAILS` | 管理员邮箱（上线前人工确认） |
| `DISCOURSE_SMTP_ADDRESS/...` | 必须配置真实 SMTP，否则无法创建管理员（**需人工确认 SMTP 供应商**） |
| 端口 | 建议仅暴露 80/443（或反代），与主站 Nginx 隔离 |

## 3. 心灯侧环境变量（与 .env.example 对应）

| 变量 | 说明 |
|---|---|
| `DISCOURSE_SSO_ENABLED` | `true` 时开放 `GET /api/community/sso`（未启用返回引导错误） |
| `DISCOURSE_SSO_SECRET` | 与 Discourse `discourse_connect_secret` **完全一致**的随机串（≥32 字符） |
| `COMMUNITY_CALLBACK_SECRET` | Discourse → 心灯发帖预警回调的 HMAC 共享密钥（在 Discourse 侧以 Webhook/自定义插件方式携带 `X-Light-Signature` 头；回调实现对接方式需与 Discourse 侧配置**人工确认**） |

## 4. 预警回调（社区发帖 → 心灯工单）

- 心灯接口：`POST /api/community/crisis-callback`（匿名 + `X-Light-Signature: HMAC-SHA256(rawBody, COMMUNITY_CALLBACK_SECRET)` hex）
- 请求体：`{"topicId":42,"postId":7,"username":"...","externalUserId":"...","content":"帖子正文"}`
- Discourse 侧实现方式（Webhook 转发头不原生支持自定义 HMAC 头 → 需小型转发服务或插件）**待人工确认**（risk-list T3 关联），阶段 6 部署时落实。

## 5. 验收对应

- 未登录社区跳转 → Discourse → `GET /api/community/sso` → 302 → `/community-sso` → 前端持令牌 `POST /api/community/sso-url` → 带 HMAC 回跳 Discourse 完成登录（已用模拟请求实测签名自洽）。
- 社区发帖命中危机词 → 回调 → `crisis_alerts(source=community, external_ref=discourse://topic/{id}/post/{id})`（已实测）。
