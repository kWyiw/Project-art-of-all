# -*- coding: utf-8 -*-
import io, sys

def patch(path, subs):
    s = io.open(path, encoding="utf-8").read()
    for old, new in subs:
        if old not in s:
            print("MISS", path, repr(old[:40]))
            continue
        s = s.replace(old, new)
    io.open(path, "w", encoding="utf-8", newline="\n").write(s)

patch("规则书/战斗规则书.md", [
    ("`大富翁层规则书` §4.3", "`大富翁层规则书` §2.3"),
    ("`大富翁层规则书.md` §4.2", "`大富翁层规则书.md` §2.2"),
])

patch("规则书/卡牌构筑规则书.md", [
    ("（例如 `大富翁层规则书.md` §4.2.1 的「蓄势格」提案），不能默认允许",
     "，不能默认允许"),
])

P = "规则书/战斗与卡牌系统规格书.md"
L = io.open(P, encoding="utf-8").read().split("\n")
L[318] = "## 9. 逐卡实现表"
L[320] = "| 牌 | 槽/CD/词条 | 关键实现 | 实现备注 |"
L[325] = "| 撬棍 | 1c/3s/武器 | 1d6 | — |"
L[327] = "| 汽车门板 | 2c/9s/废料 | 2D20 护盾 | — |"
L[328] = "| 核废水 | 1c/4s/废料 | 1d4 中毒 | 层数只增不减，见 §4.1 |"
L[338] = "| 铁丝网 | 2c/6s/陷阱 | 主动 缓速 2 张 1s；出售时在当前格放一次性陷阱（禁移动 2 回合） | — |"
L[341] = "| 雪水净化器 | 2c/8s/地产·废料 | 1d20 治疗；**每回合结束**生成 1 张淡水 | \"回合\"=棋盘回合（CJ-1.5） |"
L[345] = "| 压缩饼干 | 1c/3s/食物 | 1d4 再生 | 层数只增不减，见 §4.1 |"
io.open(P, "w", encoding="utf-8", newline="\n").write("\n".join(L))
print("ok")
