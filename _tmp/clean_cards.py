# -*- coding: utf-8 -*-
import io

P = "规则书/卡牌构筑规则书.md"
lines = io.open(P, encoding="utf-8").read().split("\n")

E = []
E.append((7, 7, '''> **数据源**：`第一套卡牌.txt` 是**唯一权威**（32 张）；`卡牌示例.txt` 只作记法说明（见规格书 §13）。'''))
E.append((10, 15, ''))
E.append((294, 299, '''**CJ-6.9　淡水是 1c / 0g / 无冷却的资源牌。**

它是**燃料、相邻素材、触发源**，靠被消耗和被利用产生价值，自身不成长，出售也不增值。理由：CJ-6.5 规定牌卖掉即永久消失，任何"出售时变强"的效果提升的都是一个永远用不上的数字。

> 这同时关掉了一个隐患：任何"卖得越多越值钱"的牌，在"卖掉即消失"下都只能改成"造得越多越值钱"，否则就会出现"这张牌存在的意义就是被卖"，而卖掉之后它又不存在了——**逻辑上自相矛盾**。'''))
E.append((350, 350, ''))
E.append((404, 404, '## 第 11 章 现有 32 张牌的构筑定位'))
E.append((471, 471, ''))

for start, end, text in sorted(E, key=lambda e: -e[0]):
    seg = text.split("\n") if text != "" else []
    lines[start - 1:end] = seg

io.open(P, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
print("ok", len(lines))
