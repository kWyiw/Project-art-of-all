# -*- coding: utf-8 -*-
import io

P = "规则书/战斗规则书.md"
lines = io.open(P, encoding="utf-8").read().split("\n")

E = []
E.append((10, 17, ''))
E.append((85, 85, '''> **为什么给 5 秒而不是 0 秒**：PvP 的发起方是主动方，被挑战方是被动方。若准备期为 0，"立刻开打"会让被挑战方永远用着上一次随手摆的阵容——这不是技巧差距，是信息与时间的不对称。5 秒刚好够"我把它挪到隔壁"这一个动作，不够重新构筑，所以不会让战斗变成临时拼装比赛。'''))
E.append((398, 398, '''**BT-7.7.1** 必须做成独立的骰值覆盖，**不能表达成"修正 +N"**：修正会渲染到卡面与 tooltip（`★+N`），玩家读到的是"加了多少点"而不是"必定满骰"——**文案与实际效果的偏差会在联机争议时变成规则漏洞**（"我以为是加 100 所以应该溢出"）。'''))
E.append((584, 584, '''| 自噬之蛇 | 被动 | 右邻所有骰值 **必定取最大值** | **已裁决**，见 BT-7.7 |'''))
E.append((588, 588, ''))

for start, end, text in sorted(E, key=lambda e: -e[0]):
    seg = text.split("\n") if text != "" else []
    lines[start - 1:end] = seg

io.open(P, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
print("ok", len(lines))
