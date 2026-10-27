"""
Collects every translatable text of STORM ENGINE into Tools/loc/keys.json ({"id": "English"}).

Sources:
  - L.T("..."), L.F("...") and L.N("...") literals in Assets/StormEngine (C# code),
  - content names and descriptions (balance-sim "lockeys" mode runs ContentText.Collect),
  - music track names, moods and descriptions (Runtime/Audio/MusicLibrary.cs).

Ids are stable: existing texts keep their id, new texts get the next free id, texts that are no
longer used move to "obsolete" in keys.json. keys.txt (id<TAB>English) is written for translators.

Usage: python Tools/loc/extract.py
"""
import glob
import io
import json
import os
import re
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(ROOT, 'Assets', 'StormEngine')
KEYS = os.path.join(HERE, 'keys.json')

CALL = re.compile(r'\bL\.[TFN]\(\s*"((?:[^"\\]|\\.)*)"')
TRACK = re.compile(r'\b(Name|Mood|Desc)\s*=\s*"((?:[^"\\]|\\.)*)"')


def unescape(s):
    out = []
    i = 0
    while i < len(s):
        c = s[i]
        if c == '\\' and i + 1 < len(s):
            n = s[i + 1]
            if n == 'n':
                out.append('\n')
            elif n == 't':
                out.append('\t')
            elif n == 'u' and i + 5 < len(s):
                out.append(chr(int(s[i + 2:i + 6], 16)))
                i += 6
                continue
            else:
                out.append(n)
            i += 2
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def code_texts():
    texts = []
    for path in sorted(glob.glob(os.path.join(ASSETS, '**', '*.cs'), recursive=True)):
        if os.sep + 'Tests' + os.sep in path or os.sep + 'Editor' + os.sep in path:
            continue
        src = io.open(path, encoding='utf-8').read()
        for m in CALL.finditer(src):
            texts.append(unescape(m.group(1)))
        if path.endswith('MusicLibrary.cs'):
            for line in src.split('\n'):
                if 'new MusicTrack' in line:
                    for m in TRACK.finditer(line):
                        texts.append(unescape(m.group(2)))
    return texts


def content_texts():
    out = subprocess.run(['dotnet', 'run', '-c', 'Release', '--project', os.path.join(ROOT, 'Tools', 'balance-sim'), '--', 'lockeys'],
                         capture_output=True, check=True)
    text = out.stdout.decode('utf-8')
    start = text.index('[')
    return json.loads(text[start:])


def main():
    wanted = []
    seen = set()
    for t in code_texts() + content_texts():
        if t and t not in seen:
            seen.add(t)
            wanted.append(t)

    data = {'keys': {}, 'obsolete': {}}
    if os.path.exists(KEYS):
        data = json.load(io.open(KEYS, encoding='utf-8'))
    keys = {int(k): v for k, v in data.get('keys', {}).items()}
    obsolete = {int(k): v for k, v in data.get('obsolete', {}).items()}
    by_text = {v: k for k, v in keys.items()}
    by_text.update({v: k for k, v in obsolete.items() if v not in by_text})

    next_id = max(list(keys) + list(obsolete) + [0]) + 1
    new_keys = {}
    added = 0
    for t in wanted:
        if t in by_text:
            new_keys[by_text[t]] = t
        else:
            new_keys[next_id] = t
            next_id += 1
            added += 1
    new_obsolete = {k: v for k, v in list(keys.items()) + list(obsolete.items()) if k not in new_keys}

    json.dump({'keys': {str(k): new_keys[k] for k in sorted(new_keys)},
               'obsolete': {str(k): new_obsolete[k] for k in sorted(new_obsolete)}},
              io.open(KEYS, 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=1)
    with io.open(os.path.join(HERE, 'keys.txt'), 'w', encoding='utf-8', newline='\n') as f:
        for k in sorted(new_keys):
            f.write('%d\t%s\n' % (k, new_keys[k].replace('\n', '\\n')))
    print('%d texts (%d new, %d obsolete)' % (len(new_keys), added, len(new_obsolete)))


if __name__ == '__main__':
    main()
