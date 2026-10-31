import io, sys

edits = {
 '规则书/战斗规则书.md': [
   ('不允许：购买、主动使用卡牌效果、打开商店。这三件事只在棋盘层做——否则 5 秒准备期会变成"临场刷牌"，',
    '不允许：购买、打开商店。这两件事只在棋盘层做——否则 5 秒准备期会变成"临场刷牌"，'),
 ],
 '规则书/大富翁层规则书.md': [
   ('           MoveCommand / EngageCommand / PlayCardCommand / ReorderCommand / SetNormaCommand。',
    '           MoveCommand / EngageCommand / ReorderCommand / SetNormaCommand。'),
 ],
 '规则书/棋盘层双栏卡槽UI与拖拽交互规格.md': [
   ('准备期内**禁止**的是购买、主动开火、打开商店。',
    '准备期内**禁止**的是购买、打开商店。'),
   ('- 准备期内的购买 / 主动开火 / 商店入口（BT-2.3.3；出售本身在准备期**可用**）；',
    '- 准备期内的购买 / 商店入口（BT-2.3.3；出售本身在准备期**可用**）；'),
   ('；同一窗口内点购买 / 主动开火被拒（BT-2.3.3）',
    '；同一窗口内点购买 / 开商店被拒（BT-2.3.3）'),
 ],
}

for P, pairs in edits.items():
    s = io.open(P, encoding='utf-8').read()
    for old, new in pairs:
        if old not in s:
            print('MISS', P, old[:30]); sys.exit(1)
        s = s.replace(old, new, 1)
    io.open(P, 'w', encoding='utf-8', newline='\n').write(s)
    print('ok', P, len(pairs))
