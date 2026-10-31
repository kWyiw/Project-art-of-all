import io, sys

P = '规则书/README.md'
s = io.open(P, encoding='utf-8').read()
pairs = [
 ('1. `大富翁层规则书.md` §2~§3（数值标准：碎片量级、生命、胜场阈值、背包、商店；以及地块补充建议）',
  '1. `大富翁层规则书.md` §1.1~§1.8、§2（流程与数值：碎片量级、生命、胜场阈值、背包、商店、全局发碎片），§3（UI 硬性约定）'),
 ('2. `大富翁层规则书.md` §5（UI 硬性约定）',
  '2. `大富翁层规则书.md` §3（UI 硬性约定）'),
 ('| **v1.7** | `R-BASE/MAP/MOVE/TILE/TURN/HOME/NORMA/CBT/CPU/ECO001-008/WIN` | 7 条（内容侧，不阻塞编码） |',
  '| **v1.7** | `R-BASE/MAP/MOVE/TILE/TURN/HOME/NORMA/CBT/CPU/ECO001-008/WIN` | 0 |'),
]
for old, new in pairs:
    if old in s:
        s = s.replace(old, new, 1)
    else:
        print('SKIP', old[:30])
io.open(P, 'w', encoding='utf-8', newline='\n').write(s)
print('done')
