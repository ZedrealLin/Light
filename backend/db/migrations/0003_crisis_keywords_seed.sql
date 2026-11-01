-- ============================================================================
-- 0003_crisis_keywords_seed.sql — 危机关键词初始词集（crisis-flow §1）
-- 硬性规则：词集内容需人工确认（docs/TODO.md T007），上线前必须复核误报/漏报。
-- 幂等：INSERT ... ON DUPLICATE KEY UPDATE（键 uk_crisis_keywords_keyword）。
-- 匹配方式：应用层大小写不敏感包含匹配（CrisisDetectionService），后台可增删改与启停。
-- ============================================================================

INSERT INTO crisis_keywords (keyword, is_active) VALUES
('想死', 1),
('不想活', 1),
('活不下去', 1),
('轻生', 1),
('自杀', 1),
('自残', 1),
('自伤', 1),
('割腕', 1),
('跳楼', 1),
('结束自己的生命', 1),
('伤害自己', 1),
('了结自己', 1)
ON DUPLICATE KEY UPDATE keyword = VALUES(keyword);
