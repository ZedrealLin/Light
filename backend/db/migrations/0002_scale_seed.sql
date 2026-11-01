-- ============================================================================
-- 0002_scale_seed.sql — 内置量表种子配置（PHQ-9 / GAD-7 / SDS）
-- 硬性规则 5：题目、选项、计分、分级、预警规则全部在 config_json，新增量表不改代码。
-- 幂等：INSERT ... ON DUPLICATE KEY UPDATE（键 uk_scales_code_version）。
-- 注意：各量表来源与许可需人工核实（docs/TODO.md T006），config.source.verified=false 表示未核实。
-- 计分约定：选项 value 直接参与求和（反向题已把 value 反转写入选项），SDS 乘 1.25 得标准分。
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. PHQ-9 患者健康问卷-9（抑郁筛查，9 题，0~27 分）
-- ----------------------------------------------------------------------------
INSERT INTO scales (code, name, version, config_json, is_active) VALUES
('phq9', '患者健康问卷-9（PHQ-9）', 1, '{
  "code": "phq9",
  "name": "患者健康问卷-9（PHQ-9）",
  "description": "广泛使用的抑郁情绪自助筛查问卷，共 9 题，约 2 分钟完成。",
  "instructions": "在过去两周内，您被以下问题困扰的频率是？请选择最接近的选项。",
  "source": {
    "name": "PHQ-9（Pfizer 公开发布的抑郁筛查工具）",
    "url": "",
    "verified": false,
    "note": "来源与使用许可需人工核实（docs/TODO.md T006），核实后将本字段 verified 置为 true"
  },
  "questions": [
    { "no": 1, "title": "做事时提不起劲或没有兴趣", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 2, "title": "感到心情低落、沮丧或绝望", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 3, "title": "入睡困难、睡不安稳或睡眠过多", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 4, "title": "感觉疲倦或没有活力", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 5, "title": "食欲不振或吃太多", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 6, "title": "觉得自己很糟，或觉得自己很失败，或让自己、家人失望", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 7, "title": "对事物专注有困难，例如阅读或看电视时", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 8, "title": "动作或说话速度缓慢到别人已经察觉？或正好相反——烦躁或坐立不安、动来动去的情况更胜于平常", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 9, "title": "有不如死掉或用某种方式伤害自己的念头", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] }
  ],
  "scoring": { "type": "sum", "multiplier": 1, "decimals": 0 },
  "levels": [
    { "code": "minimal",           "label": "目前未见明显情绪困扰的提示", "min": 0,  "max": 4 },
    { "code": "mild",              "label": "存在轻度情绪困扰的提示，可关注自身状态", "min": 5,  "max": 9 },
    { "code": "moderate",          "label": "存在中度情绪困扰的提示，建议咨询专业人员", "min": 10, "max": 14 },
    { "code": "moderately_severe", "label": "存在中重度情绪困扰的提示，建议尽快咨询专业人员", "min": 15, "max": 19 },
    { "code": "severe",            "label": "存在重度情绪困扰的提示，建议就医或尽快咨询专业人员", "min": 20, "max": 27 }
  ],
  "crisisRules": [
    { "type": "questionAtLeast", "questionNo": 9, "minValue": 1, "description": "PHQ-9 第 9 题得分 ≥1（硬性规则 2 危机触发）" }
  ],
  "disclaimer": "本量表结果仅为情绪自助提示，不构成任何医学诊断。结果解读请参考页免责声明；如有需要，请咨询专业人员或就医。"
}', 1)
ON DUPLICATE KEY UPDATE name = VALUES(name), config_json = VALUES(config_json), is_active = VALUES(is_active);

-- ----------------------------------------------------------------------------
-- 2. GAD-7 广泛性焦虑量表-7（焦虑筛查，7 题，0~21 分）
-- ----------------------------------------------------------------------------
INSERT INTO scales (code, name, version, config_json, is_active) VALUES
('gad7', '广泛性焦虑量表-7（GAD-7）', 1, '{
  "code": "gad7",
  "name": "广泛性焦虑量表-7（GAD-7）",
  "description": "常用的焦虑情绪自助筛查问卷，共 7 题，约 1 分钟完成。",
  "instructions": "在过去两周内，您被以下问题困扰的频率是？请选择最接近的选项。",
  "source": {
    "name": "GAD-7（Spitzer 等公开发布的焦虑筛查工具）",
    "url": "",
    "verified": false,
    "note": "来源与使用许可需人工核实（docs/TODO.md T006）"
  },
  "questions": [
    { "no": 1, "title": "感觉紧张、焦虑或急切", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 2, "title": "不能停止或控制担忧", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 3, "title": "对各种各样的事情担忧过多", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 4, "title": "很难放松下来", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 5, "title": "由于不安而无法静坐", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 6, "title": "变得容易烦恼或急躁", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] },
    { "no": 7, "title": "感到似乎将有可怕的事情发生而害怕", "options": [
      { "value": 0, "label": "完全不会" }, { "value": 1, "label": "好几天" },
      { "value": 2, "label": "一半以上的天数" }, { "value": 3, "label": "几乎每天" } ] }
  ],
  "scoring": { "type": "sum", "multiplier": 1, "decimals": 0 },
  "levels": [
    { "code": "minimal",  "label": "目前未见明显情绪困扰的提示", "min": 0,  "max": 4 },
    { "code": "mild",     "label": "存在轻度情绪困扰的提示，可关注自身状态", "min": 5,  "max": 9 },
    { "code": "moderate", "label": "存在中度情绪困扰的提示，建议咨询专业人员", "min": 10, "max": 14 },
    { "code": "severe",   "label": "存在重度情绪困扰的提示，建议就医或尽快咨询专业人员", "min": 15, "max": 21 }
  ],
  "crisisRules": [],
  "disclaimer": "本量表结果仅为情绪自助提示，不构成任何医学诊断。结果解读请参考页免责声明；如有需要，请咨询专业人员或就医。"
}', 1)
ON DUPLICATE KEY UPDATE name = VALUES(name), config_json = VALUES(config_json), is_active = VALUES(is_active);

-- ----------------------------------------------------------------------------
-- 3. SDS 抑郁自评量表（Zung，20 题，反向题 value 已反转，标准分 = 原始分 × 1.25）
-- ----------------------------------------------------------------------------
INSERT INTO scales (code, name, version, config_json, is_active) VALUES
('sds', '抑郁自评量表（SDS）', 1, '{
  "code": "sds",
  "name": "抑郁自评量表（SDS）",
  "description": "经典的自评抑郁情绪问卷，共 20 题，约 5 分钟完成。",
  "instructions": "请根据您最近一周的实际感觉作答，每题选择最符合的一项。",
  "source": {
    "name": "SDS（Zung 自评抑郁量表，中文修订版广泛流传）",
    "url": "",
    "verified": false,
    "note": "版权状态与中文译文准确性需人工核实（docs/TODO.md T006），如有版权限制则下架或替换"
  },
  "questions": [
    { "no": 1,  "title": "我觉得闷闷不乐，情绪低沉", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 2,  "title": "我觉得一天之中早晨最好", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 3,  "title": "我一阵阵哭出来或想哭", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 4,  "title": "我晚上睡眠不好", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 5,  "title": "我吃得跟平时一样多", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 6,  "title": "我与异性密切接触时和以往一样感到愉快", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 7,  "title": "我发觉我的体重在下降", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 8,  "title": "我有便秘的烦恼", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 9,  "title": "我心跳比平时快", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 10, "title": "我无缘无故地感到疲乏", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 11, "title": "我的头脑跟平常一样清楚", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 12, "title": "我觉得经常做的事情并没有困难", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 13, "title": "我觉得不安而平静不下来", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 14, "title": "我对将来抱有希望", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 15, "title": "我比平时容易生气激动", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 16, "title": "我觉得作出决定是容易的", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 17, "title": "我觉得自己是个有用的人，有人需要我", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 18, "title": "我的生活过得很有意思", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] },
    { "no": 19, "title": "我认为如果我死了别人会生活得好些", "options": [
      { "value": 1, "label": "没有或很少时间" }, { "value": 2, "label": "少部分时间" },
      { "value": 3, "label": "相当多时间" }, { "value": 4, "label": "绝大部分或全部时间" } ] },
    { "no": 20, "title": "平常感兴趣的事我仍然照样感兴趣", "options": [
      { "value": 4, "label": "没有或很少时间" }, { "value": 3, "label": "少部分时间" },
      { "value": 2, "label": "相当多时间" }, { "value": 1, "label": "绝大部分或全部时间" } ] }
  ],
  "scoring": { "type": "sum", "multiplier": 1.25, "decimals": 0 },
  "levels": [
    { "code": "normal",   "label": "目前未见明显情绪困扰的提示", "min": 0,  "max": 49 },
    { "code": "mild",     "label": "存在轻度情绪困扰的提示，可关注自身状态", "min": 50, "max": 59 },
    { "code": "moderate", "label": "存在中度情绪困扰的提示，建议咨询专业人员", "min": 60, "max": 69 },
    { "code": "severe",   "label": "存在重度情绪困扰的提示，建议就医或尽快咨询专业人员", "min": 70, "max": 100 }
  ],
  "crisisRules": [],
  "disclaimer": "本量表结果仅为情绪自助提示，不构成任何医学诊断。结果解读请参考页免责声明；如有需要，请咨询专业人员或就医。"
}', 1)
ON DUPLICATE KEY UPDATE name = VALUES(name), config_json = VALUES(config_json), is_active = VALUES(is_active);
