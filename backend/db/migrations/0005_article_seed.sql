-- ============================================================================
-- 0005_article_seed.sql — 文章分类种子 + 示例文章（含硬性规则 7 所需字段）
-- 幂等：INSERT ... ON DUPLICATE KEY UPDATE
-- ============================================================================

INSERT INTO article_categories (name, slug, sort_order, is_active) VALUES
('认识抑郁', 'understanding', 1, 1),
('自我照顾', 'self-care', 2, 1),
('求助指南', 'getting-help', 3, 1)
ON DUPLICATE KEY UPDATE name = VALUES(name), sort_order = VALUES(sort_order);

-- 示例文章：草稿状态进入审核流演示（后台审核通过后前台可见）
INSERT INTO articles (category_id, title, slug, summary, content, source_info, status, published_at)
SELECT c.id, '情绪低落与抑郁症：如何区分', 'mood-vs-depression',
       '情绪低落是每个人都有的正常反应；持续的、影响生活的低落才需要专业关注。本文帮助你理解两者的边界。',
       '# 情绪低落与抑郁症：如何区分\n\n每个人都会经历情绪低落。考试失利、失恋、失业……低落是正常的情绪反应，通常会随时间缓解。\n\n## 需要关注的信号\n\n- 持续两周以上几乎每天都情绪低落\n- 对以往感兴趣的事完全失去兴趣\n- 睡眠、食欲明显改变\n- 感到自责、无价值，甚至出现伤害自己的念头\n\n## 本文想告诉你的\n\n**本文不能诊断你**。如果你在上述信号中看到自己的影子，请考虑咨询专业人员；如果出现伤害自己的念头，请立即使用本站的求助资源。\n\n> 本站不提供诊断与治疗建议。', '世界卫生组织（WHO）抑郁科普资料（来源标注需人工核实，T002/T006）', 'pending_review', NULL
FROM article_categories c WHERE c.slug = 'understanding'
ON DUPLICATE KEY UPDATE summary = VALUES(summary), source_info = VALUES(source_info);
