import io, sys

P = '规则书/Unity技术方案与开发计划.md'
s = io.open(P, encoding='utf-8').read()
pairs = [
 ('骰子、金币、CD 全是整数', '骰子、碎片、CD 全是整数'),
 ('int CardUid, int GoldCost)', 'int CardUid, int ShardCost)'),
 ('    public int Gold;', '    public int Shard;'),
 ('// Gold 或 WinCount', '// Shard 或 WinCount'),
 ('ApplyBurn, Heal, GainGold,', 'ApplyBurn, Heal, GainShard,'),
 ('PlaceTrap, StealGold, DrawTempCard', 'PlaceTrap, StealShard, DrawTempCard'),
 ('对手金币的20%', '对手碎片的 20%'),
 ('[GainGold 1]', '[GainShard 1]'),
 ('（全局金币计时判定：全体发钱）', '（全局发碎片：每 3 轮全员 +5，见 `大富翁层规则书.md` §2.6）'),
 ('Δ(金币)', 'Δ(碎片)'),
 ('[P1 头像 等级 金币 升级方式]', '[P1 头像 等级 碎片 升级方式]'),
 ('# GoldLedger, ShopService', '# ShardLedger, ShopService'),
 ('- 商店、金币、全局发钱、PVP 抢掠、双轨升级、6 级胜利判定。',
  '- 商店、碎片经济、全局发碎片、PVP 抢夺、双轨升级、6 级胜利判定。'),
 ('金币曲线', '碎片曲线'),
 ('全局金币计时 + 强制 6 级结束', '全局发碎片 + 强制 6 级结束'),
 ('OnActionEnd | GainGold | 1 |', 'OnActionEnd | GainShard | 1 |'),
 ('每次行动后 +1 金币', '每次行动后 +1 碎片'),
 ('他人踩到付 5 金', '他人踩到付 5 碎片'),
 ('金币/胜场双轨', '碎片/胜场双轨'),
]
for old, new in pairs:
    if old not in s:
        print('MISS', old[:40]); sys.exit(1)
    s = s.replace(old, new)
io.open(P, 'w', encoding='utf-8', newline='\n').write(s)
print('ok', len(pairs))
