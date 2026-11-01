-- ============================================================================
-- 心灯（Light）数据库初始化脚本 — 0001_init.sql
-- 目标数据库：MySQL 5.7.44（开发基线，2026-11-01 用户确认）～ MySQL 8.0+（部署目标）
-- 排序规则：utf8mb4_unicode_ci（5.7 / 8.0 双兼容，不使用 8 专属的 0900 排序规则）
-- 注意：CHECK 约束在 5.7 中被解析但不生效，相关校验由应用层（FluentValidation）承担，升到 8 后自动生效
-- 约定：
--   1. 所有时间列 DATETIME(3)，统一存 UTC（部署配置保证会话时区为 UTC）
--   2. 幂等：全部 CREATE TABLE IF NOT EXISTS，可重复执行
--   3. 敏感内容（日记正文/量表答案/AI 消息）为应用层 AES-GCM 密文：
--      nonce(12B) + ciphertext + tag(16B) 拼接存二进制列
--   4. 外键默认 ON DELETE RESTRICT，删除走应用层显式流程（合规可审计）
-- 依据：/docs/CONVENTIONS.md 第 4.2 节、/docs/er-design.md
-- ============================================================================

SET NAMES utf8mb4;

-- ----------------------------------------------------------------------------
-- 1. users 前台用户
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS users (
  id                      BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
  public_id               CHAR(36)          NOT NULL COMMENT '对外标识，应用层生成 GUIDv7，防枚举',
  phone                   VARCHAR(20)       NULL COMMENT '手机号（验证码登录）；注销时清空',
  email                   VARCHAR(255)      NULL COMMENT '邮箱（邮件登录）；注销时清空',
  password_hash           VARCHAR(255)      NULL COMMENT 'BCrypt/PBKDF2 哈希；验证码登录账户可为空',
  nickname                VARCHAR(64)       NULL COMMENT '昵称；注销时清空',
  avatar_url              VARCHAR(512)      NULL COMMENT '头像；注销时清空',
  status                  ENUM('active','disabled','anonymized') NOT NULL DEFAULT 'active' COMMENT 'active=正常 disabled=禁用 anonymized=已注销匿名化',
  privacy_consent_at      DATETIME(3)       NULL COMMENT '首次单独勾选同意隐私政策的时间',
  privacy_consent_version VARCHAR(32)       NULL COMMENT '同意的隐私政策版本号',
  created_at              DATETIME(3)       NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at              DATETIME(3)       NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at              DATETIME(3)       NULL COMMENT '注销/删除时间',
  PRIMARY KEY (id),
  UNIQUE KEY uk_users_public_id (public_id),
  UNIQUE KEY uk_users_phone (phone),
  UNIQUE KEY uk_users_email (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='前台用户';

-- ----------------------------------------------------------------------------
-- 2. admins 后台管理员（独立凭据，与前台用户分离）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS admins (
  id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  username      VARCHAR(64)     NOT NULL COMMENT '登录名',
  password_hash VARCHAR(255)    NOT NULL COMMENT 'BCrypt/PBKDF2 哈希',
  display_name  VARCHAR(64)     NULL COMMENT '显示名',
  is_active     TINYINT(1)      NOT NULL DEFAULT 1 COMMENT '1=启用 0=停用',
  last_login_at DATETIME(3)     NULL,
  created_at    DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at    DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uk_admins_username (username)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='后台管理员';

-- ----------------------------------------------------------------------------
-- 3. refresh_tokens 刷新令牌（只存哈希）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS refresh_tokens (
  id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id    BIGINT UNSIGNED NOT NULL,
  token_hash CHAR(64)        NOT NULL COMMENT '令牌的 SHA-256 十六进制哈希，不存原文',
  expires_at DATETIME(3)     NOT NULL COMMENT '过期时间',
  revoked_at DATETIME(3)     NULL COMMENT '撤销时间（轮换/登出时写入）',
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uk_refresh_tokens_hash (token_hash),
  KEY idx_refresh_tokens_user (user_id),
  CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (user_id)
    REFERENCES users (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='刷新令牌';

-- ----------------------------------------------------------------------------
-- 4. diary_reminders 日记提醒设置（每用户一条）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS diary_reminders (
  id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id      BIGINT UNSIGNED NOT NULL,
  is_enabled   TINYINT(1)      NOT NULL DEFAULT 0 COMMENT '1=开启提醒',
  remind_time  TIME            NOT NULL DEFAULT '20:00:00' COMMENT '每天提醒时刻（UTC）',
  days_of_week VARCHAR(16)     NOT NULL DEFAULT '1,2,3,4,5,6,7' COMMENT '周几提醒，逗号分隔，1=周一',
  created_at   DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at   DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uk_diary_reminders_user (user_id),
  CONSTRAINT fk_diary_reminders_user FOREIGN KEY (user_id)
    REFERENCES users (id) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='日记提醒设置';

-- ----------------------------------------------------------------------------
-- 5. scales 量表定义（题目/选项/计分/分级/预警规则全部在 config_json，
--    新增量表不改代码 — 硬性规则 5）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS scales (
  id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  code        VARCHAR(32)     NOT NULL COMMENT '量表代码，如 phq9 / gad7 / sds',
  name        VARCHAR(128)    NOT NULL COMMENT '量表名称',
  version     INT UNSIGNED    NOT NULL DEFAULT 1 COMMENT '配置版本号，同 code 升版不改历史',
  config_json LONGTEXT        NOT NULL COMMENT 'JSON：题目、选项、计分规则、分级规则、预警规则',
  is_active   TINYINT(1)      NOT NULL DEFAULT 1 COMMENT '1=启用（可作答）',
  created_at  DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at  DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uk_scales_code_version (code, version),
  CONSTRAINT chk_scales_config_json CHECK (JSON_VALID(config_json))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='量表定义（配置化引擎）';

-- ----------------------------------------------------------------------------
-- 6. scale_submissions 量表作答记录（答案密文；支持匿名作答）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS scale_submissions (
  id             BIGINT UNSIGNED  NOT NULL AUTO_INCREMENT,
  public_id      CHAR(36)         NOT NULL COMMENT '对外标识（匿名取回结果用），应用层生成 GUIDv7',
  user_id        BIGINT UNSIGNED  NULL COMMENT '作答用户；NULL=匿名作答',
  scale_id       BIGINT UNSIGNED  NOT NULL,
  scale_code     VARCHAR(32)      NOT NULL COMMENT '冗余量表代码，便于统计',
  scale_version  INT UNSIGNED     NOT NULL COMMENT '作答时的量表配置版本',
  answers_cipher VARBINARY(4096)  NOT NULL COMMENT 'AES-GCM 密文：答案 JSON（题目号+选项值）',
  total_score    INT UNSIGNED     NOT NULL DEFAULT 0 COMMENT '总分（计分规则算出，明文仅此最小字段）',
  level_code     VARCHAR(32)      NOT NULL COMMENT '分级代码（来自分级规则，如 moderate）',
  level_label    VARCHAR(64)      NOT NULL COMMENT '分级展示文案（不含诊断表述）',
  is_crisis      TINYINT(1)       NOT NULL DEFAULT 0 COMMENT '命中预警规则（如 PHQ-9 第9题≥1分）',
  created_at     DATETIME(3)      NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '作答时间',
  PRIMARY KEY (id),
  UNIQUE KEY uk_scale_submissions_public_id (public_id),
  KEY idx_scale_submissions_user (user_id, created_at),
  KEY idx_scale_submissions_scale (scale_id, created_at),
  CONSTRAINT fk_scale_submissions_user FOREIGN KEY (user_id)
    REFERENCES users (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_scale_submissions_scale FOREIGN KEY (scale_id)
    REFERENCES scales (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='量表作答记录（答案密文）';

-- ----------------------------------------------------------------------------
-- 7. diaries 情绪日记（正文密文；每用户每天一条；软删除）
--    注意：软删除不释放 (user_id, entry_date) 唯一键，
--    同日期重新记录由应用层走"恢复/覆盖"逻辑
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS diaries (
  id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id        BIGINT UNSIGNED NOT NULL,
  entry_date     DATE            NOT NULL COMMENT '日记归属日期（用户本地时区日期）',
  mood_score     TINYINT UNSIGNED NOT NULL COMMENT '当日心情分 1~5，5 为最好',
  content_cipher MEDIUMBLOB      NOT NULL COMMENT 'AES-GCM 密文日记正文',
  tags           JSON            NULL COMMENT '标签名数组（可选集合由前端限定）',
  is_crisis      TINYINT(1)      NOT NULL DEFAULT 0 COMMENT '该篇命中危机关键词',
  created_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at     DATETIME(3)     NULL COMMENT '软删除时间',
  PRIMARY KEY (id),
  UNIQUE KEY uk_diaries_user_date (user_id, entry_date),
  KEY idx_diaries_user_created (user_id, created_at),
  CONSTRAINT fk_diaries_user FOREIGN KEY (user_id)
    REFERENCES users (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT chk_diaries_mood CHECK (mood_score BETWEEN 1 AND 5)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='情绪日记（正文密文）';

-- ----------------------------------------------------------------------------
-- 8. crisis_keywords 危机关键词配置（日记/社区发帖/AI 对话命中即触发预警）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS crisis_keywords (
  id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  keyword    VARCHAR(64)     NOT NULL COMMENT '关键词（应用层按词匹配）',
  is_active  TINYINT(1)      NOT NULL DEFAULT 1,
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uk_crisis_keywords_keyword (keyword)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='危机关键词配置';

-- ----------------------------------------------------------------------------
-- 9. ai_sessions AI 陪伴会话
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ai_sessions (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id         BIGINT UNSIGNED NOT NULL,
  title           VARCHAR(128)    NULL COMMENT '会话标题（可由首条消息摘要生成）',
  status          ENUM('active','closed') NOT NULL DEFAULT 'active',
  started_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  last_message_at DATETIME(3)     NULL,
  created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY idx_ai_sessions_user (user_id, started_at),
  CONSTRAINT fk_ai_sessions_user FOREIGN KEY (user_id)
    REFERENCES users (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI 陪伴会话';

-- ----------------------------------------------------------------------------
-- 10. ai_messages AI 陪伴消息（内容密文；危机命中留痕）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ai_messages (
  id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  session_id     BIGINT UNSIGNED NOT NULL,
  role           ENUM('user','assistant','system') NOT NULL COMMENT '消息角色',
  content_cipher MEDIUMBLOB      NOT NULL COMMENT 'AES-GCM 密文消息内容（用户与助手均加密）',
  is_crisis      TINYINT(1)      NOT NULL DEFAULT 0 COMMENT '该消息命中危机关键词',
  created_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY idx_ai_messages_session (session_id, id),
  CONSTRAINT fk_ai_messages_session FOREIGN KEY (session_id)
    REFERENCES ai_sessions (id) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI 陪伴消息（密文）';

-- ----------------------------------------------------------------------------
-- 11. crisis_alerts 预警工单（危机干预核心表）
--     来源四种：量表作答 / 日记 / 社区发帖(Discourse) / AI 对话
--     detail_json 只存触发要素（题目号/选项分/命中词），不复制正文全文
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS crisis_alerts (
  id                  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  source              ENUM('scale','diary','community','ai_chat') NOT NULL COMMENT '预警来源',
  user_id             BIGINT UNSIGNED NULL COMMENT '关联用户；社区匿名场景可为空',
  scale_submission_id BIGINT UNSIGNED NULL COMMENT '来源=scale 时的作答记录',
  diary_id            BIGINT UNSIGNED NULL COMMENT '来源=diary 时的日记',
  ai_session_id       BIGINT UNSIGNED NULL COMMENT '来源=ai_chat 时的会话',
  external_ref        VARCHAR(512)    NULL COMMENT '来源=community 时的外部引用（如 Discourse 帖子链接/ID）',
  detail_json         JSON            NULL COMMENT '触发快照：命中关键词/题目得分等（最小化）',
  status              ENUM('open','processing','resolved','dismissed') NOT NULL DEFAULT 'open' COMMENT 'open=待处理 processing=处理中 resolved=已解决 dismissed=已忽略',
  handled_by          BIGINT UNSIGNED NULL COMMENT '处理管理员',
  handled_at          DATETIME(3)     NULL,
  handle_note         VARCHAR(1024)   NULL COMMENT '处理记录',
  created_at          DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at          DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY idx_crisis_alerts_status (status, created_at),
  KEY idx_crisis_alerts_user (user_id, created_at),
  CONSTRAINT fk_crisis_alerts_user FOREIGN KEY (user_id)
    REFERENCES users (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_crisis_alerts_submission FOREIGN KEY (scale_submission_id)
    REFERENCES scale_submissions (id) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT fk_crisis_alerts_diary FOREIGN KEY (diary_id)
    REFERENCES diaries (id) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT fk_crisis_alerts_session FOREIGN KEY (ai_session_id)
    REFERENCES ai_sessions (id) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT fk_crisis_alerts_admin FOREIGN KEY (handled_by)
    REFERENCES admins (id) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='预警工单（危机干预）';

-- ----------------------------------------------------------------------------
-- 12. counselors 咨询师档案（含资质字段与审核状态）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS counselors (
  id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  name              VARCHAR(64)     NOT NULL COMMENT '姓名（对外展示）',
  gender            ENUM('unknown','female','male') NOT NULL DEFAULT 'unknown' COMMENT '展示用，自愿提供',
  title             VARCHAR(64)     NULL COMMENT '头衔，如 国家二级心理咨询师',
  license_no        VARCHAR(128)    NULL COMMENT '资质证书编号',
  license_image_url VARCHAR(512)    NULL COMMENT '资质证书图片地址',
  intro             TEXT            NULL COMMENT '个人简介',
  specialties       VARCHAR(512)    NULL COMMENT '擅长方向，逗号分隔',
  avatar_url        VARCHAR(512)    NULL,
  review_status     ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending' COMMENT '资质审核状态',
  reviewed_by       BIGINT UNSIGNED NULL COMMENT '审核管理员',
  reviewed_at       DATETIME(3)     NULL,
  review_note       VARCHAR(512)    NULL COMMENT '审核意见',
  is_active         TINYINT(1)      NOT NULL DEFAULT 0 COMMENT '1=上架可预约（须审核通过）',
  created_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY idx_counselors_review (review_status),
  KEY idx_counselors_active (is_active),
  CONSTRAINT fk_counselors_admin FOREIGN KEY (reviewed_by)
    REFERENCES admins (id) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='咨询师档案（资质+审核）';

-- ----------------------------------------------------------------------------
-- 13. counselor_slots 可预约时段
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS counselor_slots (
  id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  counselor_id BIGINT UNSIGNED NOT NULL,
  start_at     DATETIME(3)     NOT NULL COMMENT '开始时间（UTC）',
  end_at       DATETIME(3)     NOT NULL COMMENT '结束时间（UTC）',
  status       ENUM('available','booked','closed') NOT NULL DEFAULT 'available' COMMENT 'available=可约 booked=已约 closed=关闭',
  created_at   DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at   DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uk_counselor_slots_counselor_start (counselor_id, start_at),
  KEY idx_counselor_slots_query (counselor_id, status, start_at),
  CONSTRAINT fk_counselor_slots_counselor FOREIGN KEY (counselor_id)
    REFERENCES counselors (id) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT chk_counselor_slots_time CHECK (end_at > start_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='可预约时段';

-- ----------------------------------------------------------------------------
-- 14. bookings 咨询预约
--     防超卖：active_slot_key 生成列——status='booked' 时=slot_id，否则 NULL；
--     唯一索引 uk_bookings_active_slot 保证"同一时段最多一条有效预约"
--     （MySQL 唯一索引允许多个 NULL）。支付仅预留字段。
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS bookings (
  id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id        BIGINT UNSIGNED NOT NULL,
  counselor_id   BIGINT UNSIGNED NOT NULL,
  slot_id        BIGINT UNSIGNED NOT NULL,
  status         ENUM('booked','cancelled','completed','no_show') NOT NULL DEFAULT 'booked' COMMENT 'booked=有效 cancelled=已取消 completed=已完成 no_show=未到',
  payment_status ENUM('none','pending','paid','refunded') NOT NULL DEFAULT 'none' COMMENT '支付仅预留，阶段 4 不实现',
  cancel_reason  VARCHAR(512)    NULL COMMENT '取消原因',
  booked_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '预约时间',
  cancelled_at   DATETIME(3)     NULL COMMENT '取消时间',
  active_slot_key BIGINT UNSIGNED GENERATED ALWAYS AS (
    CASE WHEN status = 'booked' THEN slot_id END
  ) STORED COMMENT '有效预约的时段键；取消/完成后为 NULL，用于防超卖唯一索引',
  created_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uk_bookings_active_slot (active_slot_key),
  KEY idx_bookings_user (user_id, booked_at),
  KEY idx_bookings_counselor (counselor_id, booked_at),
  CONSTRAINT fk_bookings_user FOREIGN KEY (user_id)
    REFERENCES users (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_bookings_counselor FOREIGN KEY (counselor_id)
    REFERENCES counselors (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_bookings_slot FOREIGN KEY (slot_id)
    REFERENCES counselor_slots (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='咨询预约（DB 层防超卖）';

-- ----------------------------------------------------------------------------
-- 15. article_categories 科普文章分类
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS article_categories (
  id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  name       VARCHAR(64)     NOT NULL COMMENT '分类名',
  slug       VARCHAR(64)     NOT NULL COMMENT '分类标识（URL 用）',
  sort_order INT             NOT NULL DEFAULT 0 COMMENT '排序，小在前',
  is_active  TINYINT(1)      NOT NULL DEFAULT 1,
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  UNIQUE KEY uk_article_categories_slug (slug),
  UNIQUE KEY uk_article_categories_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='科普文章分类';

-- ----------------------------------------------------------------------------
-- 16. articles 科普文章（审核发布；含"信息来源/审核人/更新时间"—硬性规则 7）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS articles (
  id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  category_id  BIGINT UNSIGNED NULL,
  title        VARCHAR(255)    NOT NULL,
  slug         VARCHAR(255)    NULL COMMENT 'URL 标识；发布时可生成',
  summary      VARCHAR(512)    NULL COMMENT '摘要',
  content      MEDIUMTEXT      NOT NULL COMMENT '正文（Markdown）',
  source_info  VARCHAR(512)    NOT NULL COMMENT '信息来源（硬性规则 7）',
  status       ENUM('draft','pending_review','published','offline') NOT NULL DEFAULT 'draft' COMMENT 'draft=草稿 pending_review=待审核 published=已发布 offline=已下线',
  submitted_by BIGINT UNSIGNED NULL COMMENT '提交管理员',
  reviewed_by  BIGINT UNSIGNED NULL COMMENT '审核人（硬性规则 7）',
  reviewed_at  DATETIME(3)     NULL,
  review_note  VARCHAR(512)    NULL COMMENT '审核意见',
  published_at DATETIME(3)     NULL COMMENT '发布时间',
  created_at   DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at   DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（硬性规则 7）',
  PRIMARY KEY (id),
  UNIQUE KEY uk_articles_slug (slug),
  KEY idx_articles_status_published (status, published_at),
  KEY idx_articles_category (category_id),
  CONSTRAINT fk_articles_category FOREIGN KEY (category_id)
    REFERENCES article_categories (id) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT fk_articles_submitter FOREIGN KEY (submitted_by)
    REFERENCES admins (id) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT fk_articles_reviewer FOREIGN KEY (reviewed_by)
    REFERENCES admins (id) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='科普文章（审核发布）';

-- ----------------------------------------------------------------------------
-- 17. notifications 站内通知
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS notifications (
  id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id    BIGINT UNSIGNED NOT NULL,
  type       VARCHAR(32)     NOT NULL COMMENT '类型，如 booking_confirmed / booking_cancelled / crisis_followup',
  title      VARCHAR(128)    NOT NULL,
  body       VARCHAR(1024)   NULL COMMENT '内容（不包含敏感明文）',
  is_read    TINYINT(1)      NOT NULL DEFAULT 0,
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY idx_notifications_user (user_id, is_read, id),
  CONSTRAINT fk_notifications_user FOREIGN KEY (user_id)
    REFERENCES users (id) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='站内通知';

-- ============================================================================
-- 结束：17 张表。种子数据（量表 JSON 配置、初始管理员、危机关键词初始集）
-- 在阶段 1.2 / 1.3 以独立迁移或配置装载方式提供，不混入本脚本。
-- ============================================================================
