# -*- coding: utf-8 -*-
import io
p = "规则书/大富翁层规则书.md"
ls = io.open(p, encoding="utf-8").read().split("\n")
q = {0x22: 'ASCII"', 0x201c: 'LDQ', 0x201d: 'RDQ', 0x300c: 'LJB', 0x300d: 'RJB', 0x300a: 'LDB', 0x300b: 'RDB', 0x2018: 'LSQ', 0x2019: 'RSQ'}
for n in (214, 215, 302, 312, 342, 424, 526, 620, 632):
    line = ls[n-1]
    tags = sorted({q[ord(c)] for c in line if ord(c) in q})
    print(n, tags, len(line))
