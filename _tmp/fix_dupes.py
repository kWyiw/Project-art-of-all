import io, sys

edits = {
 '规则书/棋盘层双栏卡槽UI与拖拽交互规格.md': [
   ('**UI-7.4.3** 断线玩家在准备期内不阻塞开战',
    '**UI-7.4.4** 断线玩家在准备期内不阻塞开战'),
   ('| 22 | 抓包请求他人数据 | 返回的 `PublicLoadout.Battle` 在准备期/战斗中为 `null`，且**响应体里不含任何 bag 牌名** |',
    '| 22 | 抓包请求他人数据 | 返回的 `PublicLoadout.Battle` 在准备期为 `null`、战斗中正常下发；且**响应体里不含任何 bag 牌名** |'),
 ],
 '规则书/卡牌构筑规则书.md': [
   ('触发"出售时"类效果（车票、铁丝网、敲诈、淡水）',
    '触发"出售时"类效果（车票、铁丝网、敲诈）'),
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
