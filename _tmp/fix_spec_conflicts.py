import io, sys

edits = {
 '规则书/战斗与卡牌系统规格书.md': [
   ('R-TIME-006  充能 N 秒 = CD 剩余直接减去 N×60 tick，下限 0。',
    'R-TIME-006  充能 N 秒 = CD 剩余直接减去 N×60 tick，下限 0；\n'
    '            超出剩余冷却的部分存进该牌的 `chargeCarry`（tick），\n'
    '            下一次进入冷却时从新 CD 里扣除、扣到 0 为止，战斗结束清零（`战斗规则书.md` BT-3.6）。'),
   ('R-TIME-004  "可用"= CD 剩余 ≤ 0。触发后 CD 重置为 card.cooldown。',
    'R-TIME-004  "可用"= CD 剩余 ≤ 0。触发后 CD 重置为 `max(0, card.cooldown − chargeCarry)`，\n'
    '            随后 `chargeCarry = 0`（BT-3.6）。'),
   ('| 生成牌（淡水） | 棋盘内部 | 回合结束进背包，背包满则按 R-ECO-006 折现 |',
    '| 生成牌（淡水） | 棋盘内部 | 回合结束进背包；背包满按 R-ECO-006 **本次获得失败**（不折现、不进场） |'),
   ('`Preparing`/`Fighting` 期间双方战斗栏互不可见（BT-2.3.4、UI 规格 §7.3）',
    '`Preparing` 期间互不可见，`Fighting` 期间只读可见（BT-2.3.4、`大富翁层规则书.md` R-CBT-010、UI 规格 §7.4）'),
   ('R-ECO-014  "出售时"效果每回合最多触发 1 次（铁丝网的陷阱、永久修正），\n            防止一回合连卖 3 张把地块变成雷区。',
    'R-ECO-014  "出售时"效果每回合最多触发 1 次（铁丝网的陷阱、永久修正），\n'
    '            防止一回合连卖 3 张把地块变成雷区。\n'
    '            **准备期内的出售计入发起方当前回合**，与棋盘层的出售共用一个计数。'),
 ],
}

for P, pairs in edits.items():
    s = io.open(P, encoding='utf-8').read()
    for old, new in pairs:
        if old not in s:
            print('MISS'); sys.exit(1)
        s = s.replace(old, new, 1)
    io.open(P, 'w', encoding='utf-8', newline='\n').write(s)
    print('ok', len(pairs))
