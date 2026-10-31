import io

P = '规则书/Unity技术方案与开发计划.md'
s = io.open(P, encoding='utf-8').read()
old = '## 附录 A：卡牌数据表模板（CSV，策划可直接填）\n\n| id | name_zh |'
new = ('## 附录 A：卡牌数据表模板（CSV）\n\n'
       '> 下面几行只演示**字段怎么填**，不是真实卡表。真实 32 张的数值以 '
       '`战斗与卡牌系统规格书.md` §9 与 `卡牌构筑规则书.md` 第 11 章为准。\n\n'
       '| id | name_zh |')
assert old in s
s = s.replace(old, new, 1)
io.open(P, 'w', encoding='utf-8', newline='\n').write(s)

# 草场 example cost 2 -> 1 (matches the real card)
s = io.open(P, encoding='utf-8').read()
old2 = '| pasture | 草场 | 2 | 0 | Finance |'
new2 = '| pasture | 草场 | 1 | 0 | Finance |'
if old2 in s:
    s = s.replace(old2, new2, 1)
    io.open(P, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
