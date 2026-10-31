import io

P = '规则书/大富翁层规则书.md'
s = io.open(P, encoding='utf-8').read()

block = '''**对地图设计的三条硬约束**（由"任意 home 可升级 + 仅自家可主动停留"直接决定）：

1. 4 个 home 应把环**四等分**（N=32 → 节点 0/8/16/24）。因为 home 现在是可以互相替代的升级点，均匀分布能把"离最近 home 的最远距离"压到最小，避免出现"半圈没有升级点"的死区。
2. home 位置**决定玩家节奏**：出生在 home 上 ⇒ 每圈一次的稳定兑现节奏是相同的；不要出现"某个 home 前面是连续 3 个 Warp 格"之类导致兑现率异常的布局。
3. **这条规则的隐含好处（不需要补偿数值的原因之一）**：落后玩家不会因为离家太远而卡在"攒够了却兑不了"，追赶更顺。这是"任意 home 可升级"这条规则的真正价值。

'''

# 1. cut the orphaned block from under §3 UI 硬性约定
old_tail = '\n\n\n' + block
if old_tail not in s:
    print('MISS block'); raise SystemExit(1)
s = s.replace(old_tail, '\n', 1)

# 2. insert it at the end of §1.2 地图 (after the R-MAP code fence)
anchor = '''R-MAP-006  地块 = 基础地块（恒为 Base）+ 0..n 个地块效果（TileEffect）。
           "特殊地块"一律由效果叠加实现，不新增基础类型。
```
'''
if anchor not in s:
    print('MISS anchor'); raise SystemExit(1)
s = s.replace(anchor, anchor + '\n' + block, 1)

io.open(P, 'w', encoding='utf-8', newline='\n').write(s)
print('moved')
