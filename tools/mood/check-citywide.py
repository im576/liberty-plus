"""Validate the actual generator against local FusionFix files; never modifies game files."""
import json
import math
from pathlib import Path
import re
import subprocess
import sys
import tempfile

repo = Path(__file__).resolve().parents[2]
game_data = Path(sys.argv[1]) / 'update/pc/data'
exe = repo / 'tools/mood/bin/MoodTimecycle.exe'
config = repo / 'config/mood.json'

def rows(path):
    result = {}
    weather = time = None
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        if line.startswith('//////////'):
            weather = line.strip('/ ')
        elif line.startswith('//') and line[2:].strip() in times:
            time = line[2:].strip()
        elif time and re.match(r'^\s*\d', line):
            key = weather, time
            assert key not in result, f'duplicate row {key}'
            result[key] = [float(x) for x in re.findall(r'-?\d+(?:\.\d+)?', line)]
            time = None
    return result

mood = json.loads(config.read_text(encoding='utf-8-sig'))
times = {t for group in mood['timeClasses'].values() for t in group}
weathers = {w for group in mood['weatherClasses'].values() for w in group}
assert len(times) == 11 and len(weathers) == 8
expected = {(w, t) for w in weathers for t in times}
with tempfile.TemporaryDirectory(prefix='liberty-mood-check-') as folder:
    out = Path(folder)
    sources = [game_data / (name + '.fusionfix') for name in ('timecyc.dat', 'timecycext.dat')]
    outputs = [out / name for name in ('timecyc.dat', 'timecycext.dat')]
    args = [str(exe), str(config), *(str(p) for p in sources), *(str(p) for p in outputs)]
    run = subprocess.run(args, capture_output=True, text=True)
    assert run.returncode == 0, run.stdout + run.stderr
    before, after = rows(sources[0]), rows(outputs[0])
    assert expected <= before.keys() and expected <= after.keys()
    permitted = set(range(0, 9)) | set(range(12,15)) | set(range(25,31)) | set(range(38,42)) | set(range(64,76)) | set(range(81,84)) | set(range(99,102)) | {24,45,46,48,49,56,57,58,59,60,61}
    changed = 0
    for key in expected:
        a, b = before[key], after[key]
        assert len(a) == len(b), f'column count changed {key}'
        assert all(math.isfinite(v) for v in b)
        assert all(a[i] == b[i] for i in range(len(a)) if i not in permitted), f'unrelated column changed {key}'
        assert 0 <= b[45] <= .85 and 0 <= b[48] <= .85
        assert .5 <= b[46] <= 1.25 and .5 <= b[49] <= 1.25
        assert all(0 <= b[i] <= 255 for i in list(range(0,9)) + list(range(12,15)) + list(range(25,31)) + list(range(39,42)) + [56])
        assert a[9:12] == b[9:12], f'legacy unused/grain/fog-alpha fields changed {key}'
        assert all(0 <= b[i] <= 1 for i in list(range(64,76)) + list(range(81,84)) + list(range(99,102)))
        assert a != b, f'untuned row {key}'
        changed += 1
    before_ext, after_ext = rows(sources[1]), rows(outputs[1])
    assert expected <= after_ext.keys()
    for key in expected:
        a, b = before_ext[key], after_ext[key]
        assert len(a) == len(b) and a[1:] == b[1:], f'unrelated extended field changed {key}'
        assert math.isfinite(b[0]) and b[0] >= 0
    hashes = [p.read_bytes() for p in outputs]
    assert subprocess.run(args, capture_output=True).returncode == 0
    assert hashes == [p.read_bytes() for p in outputs], 'generation is not deterministic'
    invalid = dict(mood)
    invalid['rules'] = [dict(mood['rules'][0], skyTop=[0, 300, 0])]
    bad = out / 'invalid.json'
    bad.write_text(json.dumps(invalid))
    args[1] = str(bad)
    assert subprocess.run(args, capture_output=True).returncode != 0, 'invalid sky RGB accepted'
    invalid['rules'] = [dict(mood['rules'][0], cloudAlpha=256)]
    bad.write_text(json.dumps(invalid))
    assert subprocess.run(args, capture_output=True).returncode != 0, 'invalid cloud alpha accepted'
    print(f'PASS: {changed}/88 weather-time rows; grain/fog-alpha and unrelated fields preserved; normalized sky RGB; deterministic generation; invalid RGB/cloud alpha rejected')
