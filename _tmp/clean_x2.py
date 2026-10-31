# -*- coding: utf-8 -*-
import io

P = "规则书/大富翁层规则书.md"
s = io.open(P, encoding="utf-8").read()

subs = [
    ("| 奖励（碎片版）Bonus | — | `碎片 += min(等级,3) × 本次骰点` | ×2 版：掷 2 颗骰再算 |",
     "| 奖励（碎片版）Bonus | — | `碎片 += min(等级,3) × 本次骰点` | |"),
    ("| 怪物 Encounter | — | 与 1 只杂鱼应战 | ×2 版胜场翻倍 |",
     "| 怪物 Encounter | — | 与 1 只杂鱼应战 | |"),
    ("            判定：走到该格时用 R-MOVE-001 掷出的那个值。\n            ×2 版：掷 2 颗骰子求和后计算（测试里固定下来）。\n",
     "            判定：走到该格时用 R-MOVE-001 掷出的那个值。\n"),
    ("R-MOVE-008  额外移动阶段（Move 格）上限：**每回合最多追加 1 次**，防止连锁。",
     "R-MOVE-008  若启用 Move 格（§3 建议 3）：额外移动阶段上限**每回合最多追加 1 次**，防止连锁。"),
    ("五件套取 ×2/Move/Drop/Boss",
     "五件套里 Drop / Boss 已入 §1.5 的地块表，×2 / Move 暂缓（见 §3）"),
    ("| B4 | 地块效果分发（全部 **11 种**，含 ×2 / Move / Drop / Boss 演化） |",
     "| B4 | 地块效果分发（全部 **11 种**，含 Boss 演化） |"),
    ("| Bonus ×2 | 掷 2 骰求和 × min(等级,3) |\n", ""),
]

for old, new in subs:
    n = s.count(old)
    if n != 1:
        print("COUNT", n, repr(old[:50]))
    s = s.replace(old, new)

io.open(P, "w", encoding="utf-8", newline="\n").write(s)
print("done")
