"""
Builds the game's translation files from Tools/loc/src/<code>*.txt.

Each source line is "<id>|<translation>" (a tab also works; ids from keys.txt; "\\n" means a line break).
Several files per language are allowed (ja.1.txt, ja.2.txt...). The result goes to
Assets/StormEngine/Resources/StormEngine/Lang/<code>.json as {"English": "translation"}.

Checks: every id exists, placeholders ({0}, {1}...) match the English text, nothing is empty.
Missing ids are listed (the game shows English for them).

Usage: python Tools/loc/build.py [code ...]
"""
import glob
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(ROOT, 'Assets', 'StormEngine', 'Resources', 'StormEngine', 'Lang')
PLACEHOLDER = re.compile(r'\{\d+\}')


def load_sources(code):
    table = {}
    problems = []
    files = sorted(glob.glob(os.path.join(HERE, 'src', code + '.txt')) + glob.glob(os.path.join(HERE, 'src', code + '.*.txt')))
    for path in files:
        for n, line in enumerate(io.open(path, encoding='utf-8').read().split('\n'), 1):
            line = line.rstrip('\r')
            if not line.strip() or line.startswith('#'):
                continue
            m = re.match(r'^(\d+)[\t|](.*)$', line)
            if not m:
                problems.append('%s:%d: expected "<id>|<text>"' % (os.path.basename(path), n))
                continue
            key, text = m.group(1), m.group(2)
            try:
                table[int(key)] = text.replace('\\n', '\n').strip()
            except ValueError:
                problems.append('%s:%d: bad id %r' % (os.path.basename(path), n, key))
    return table, problems, files


def build(code, keys):
    table, problems, files = load_sources(code)
    if not files:
        return None
    out = {}
    missing = []
    for k in sorted(keys):
        english = keys[k]
        text = table.get(k)
        if not text:
            missing.append(k)
            continue
        if sorted(PLACEHOLDER.findall(english)) != sorted(PLACEHOLDER.findall(text)):
            problems.append('id %d: placeholders differ: %r -> %r' % (k, english, text))
            continue
        out[english] = text
    for k in table:
        if k not in keys:
            problems.append('id %d is not a current key' % k)
    os.makedirs(OUT, exist_ok=True)
    with io.open(os.path.join(OUT, code + '.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(out, f, ensure_ascii=False, indent=0, sort_keys=False)
    return len(out), missing, problems


def main():
    keys = {int(k): v for k, v in json.load(io.open(os.path.join(HERE, 'keys.json'), encoding='utf-8'))['keys'].items()}
    codes = sys.argv[1:] or sorted({os.path.basename(p).split('.')[0] for p in glob.glob(os.path.join(HERE, 'src', '*.txt'))})
    ok = True
    for code in codes:
        result = build(code, keys)
        if result is None:
            print('%s: no source files' % code)
            continue
        count, missing, problems = result
        print('%-8s %4d / %d translated%s' % (code, count, len(keys), ('   missing: ' + ','.join(map(str, missing[:30])) + ('...' if len(missing) > 30 else '')) if missing else ''))
        for p in problems:
            print('   ! ' + p)
            ok = False
    sys.exit(0 if ok else 1)


if __name__ == '__main__':
    main()
