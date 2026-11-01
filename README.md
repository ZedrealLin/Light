# 心灯（Light）

> 在情绪的夜里，留一盏灯。
> 抑郁情绪科普与自助支持网站：科普内容 · 自评量表 · 情绪日记 · 咨询预约 · AI 陪伴 · 危机干预（贯穿全站）。

> ⚠️ 本站不提供医疗诊断或治疗建议。如果你正处于危机中，请立即拨打当地心理援助热线或前往就近医院。

## 技术栈

- 后端：.NET 10（ASP.NET Core, Controller）+ Dapper/MySqlConnector + MySQL（开发基线 5.7，部署目标 8）+ Redis + Serilog + JWT + AES-GCM + FluentValidation + xUnit
- 前端：Vue 3 + TypeScript + Vite + Pinia + Vue Router + Axios + Element Plus + ECharts（/web 前台 与 /admin 后台 两个独立项目）
- 部署：Docker Compose + Nginx；社区使用 Discourse 官方镜像独立部署（见 `deploy/discourse/README.md`）

## 快速开始（开发）

```bash
# 1. 准备环境变量（硬性规则 9：真实值不进仓库）
cp .env.example .env   # 填写 DB / AES_DATA_KEY / JWT_SECRET 等
# 生成 AES 密钥示例：python -c "import base64,os;print(base64.b64encode(os.urandom(32)).decode())"

# 2. 后端（需本机 MySQL；迁移在启动时自动执行 /backend/db/migrations）
cd backend && dotnet run --project src/Light.Api   # http://127.0.0.1:5080  文档 /scalar

# 3. 前端
cd web  && npm install && npm run dev   # http://127.0.0.1:5173
cd admin && npm install && npm run dev  # http://127.0.0.1:5174

# 4. 测试
cd backend && dotnet test               # 14 个单测（量表引擎）
```

## 部署（阶段 6）

```bash
cp .env.example .env && vim .env
docker compose up -d          # MySQL 8 + Redis + API + web + admin + Nginx
# 备份/恢复：deploy/backup.sh、deploy/restore.sh（建议 crontab）
```

## 文档索引

| 文档 | 说明 |
|---|---|
| `docs/CONVENTIONS.md` | 技术栈、目录、命名、统一返回体、错误码（先读这个） |
| `docs/PROGRESS.md` | 阶段进度与待办（接力文件） |
| `docs/er-design.md` | 17 表 ER 设计契约 |
| `docs/api-spec.md` | 接口清单契约 |
| `docs/crisis-flow.md` | 危机干预流程（唯一权威） |
| `docs/risk-list.md` | 风险清单（24 项） |
| `docs/security-review.md` | 全量安全自查报告（阶段 6） |
| `docs/TODO.md` | 待人工确认事项（T001–T012） |
| `docs/legal/*.docx` | 隐私政策/用户协议/免责声明草稿（**需人工法务审阅**） |

## 合规红线（摘要，全文见 AGENTS.md）

不做诊断；危机干预贯穿全站（4 来源 → 求助页 + 预警工单）；敏感内容 AES-GCM 加密落库；热线号码全部可配置（占位符 + 上线前人工核实）；AI 声明"不是医生或心理治疗师"；密钥只走环境变量。

## 上线前必办（节选，全文见 security-review.md §4 / TODO.md）

T009 更换开发兜底管理员口令；T010 实现数据导出与彻底删除接口；T011 禁用用户即时吊销令牌；T012 AI 限流切 Redis；容器链路在有 Docker 的环境实测；Discourse 部署与回调对接人工确认。
