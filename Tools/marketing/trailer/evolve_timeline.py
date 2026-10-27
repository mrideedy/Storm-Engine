"""
Reel 6, "day 1 vs day 30 in my game": one continuous shot of the island growing from a bare day-1 engine
to a fully decorated day-30 engine in a giant storm, with a DAY counter and native-looking text, ending on
"free on google play". Shared by make_evolve_audio.py and make_evolve.py; `python evolve_timeline.py`
writes the recorder's shot list (shots_evolve.json).

Music: 120 BPM (a bar is 2 s). Record on a copy of the test save with its decorations removed.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 120
BPM = 120
BEAT = 60.0 / BPM
LENGTH = 13.0
GROOVE = (4.0, 12.0)   # drums and bass
LEAD_FROM = 8.0
FINAL = 12.0           # the last hit

STAGES = [(0.0, 'DAY 1'), (2.0, 'DAY 3'), (4.0, 'DAY 7'), (7.0, 'DAY 30')]
TEXT = [  # start, end, line (native-looking boxes)
    (0.0, 10.0, 'day 1 vs day 30 in my game'),
    (10.0, LENGTH, 'storm engine is free on google play'),
]

CUES = [
    (0.9, 'buy:pump'),
    (2.1, 'buy:condenser'), (2.4, 'buy:tesla'), (2.7, 'buy:furnace'), (3.0, 'buy:hydro'), (3.3, 'buy:turbine'),
    (3.55, 'decor:dec_flowerbed'), (3.75, 'decor:dec_lamppost'),
    (4.1, 'level:pump:12'), (4.35, 'level:condenser:12'), (4.6, 'level:tesla:12'),
    (5.0, 'decor:dec_windmill'), (5.25, 'decor:dec_fountain'), (5.5, 'decor:dec_pond'), (5.75, 'decor:dec_ducks'),
    (6.0, 'decor:dec_kite'), (6.25, 'decor:dec_balloon'), (6.5, 'decor:dec_arch'),
    (7.05, 'equip:skin_gold'), (7.05, 'equip:cloud_tempest'), (7.05, 'equip:bolt_prism'),
    (7.3, 'decor:dec_statue'), (7.5, 'decor:dec_blimp'), (7.7, 'decor:dec_lanterns'), (7.9, 'decor:dec_crystal'),
    (8.1, 'decor:dec_whale'), (8.3, 'decor:dec_sailboat'), (8.5, 'decor:dec_mushroom'),
    (8.4, 'skyRamp:0.9:1.0'),
    (9.0, 'strike'), (9.5, 'bigStrike'), (10.0, 'bigStrike'), (10.0, 'shake:0.35'), (10.5, 'strike'),
    (11.0, 'bigStrike'), (11.5, 'strike'), (12.0, 'bigStrike'), (12.0, 'shake:0.45'),
]


def camera_key(t):
    p = t / LENGTH
    return {'t': round(t, 3), 'anchor': 'island', 'y': 1.2, 'yaw': round(-60 + 48 * p, 2),
            'pitch': round(31 - 4 * p, 2), 'dist': round(50 - 8 * p, 2), 'fov': 40, 'sy': 0.04}


def shots():
    seconds = LENGTH + 0.2
    take = {'name': 'evolve', 'seconds': seconds, 'preroll': 1.5, 'ui': False, 'ease': 0.0,
            'setup': ['calm', 'home:0', 'equip:world_meadow', 'equip:skin_classic', 'equip:cloud_classic',
                      'equip:bolt_classic', 'hide:pump,hydro,condenser,tesla,furnace,turbine', 'sky:0'],
            'cues': [{'t': t, 'a': a} for t, a in sorted(CUES)],
            'camera': [camera_key(0.0), camera_key(seconds)]}
    return {'fps': FPS, 'width': 1080, 'height': 1920, 'takes': [take]}


def stage_at(t):
    label = STAGES[0][1]
    for at, name in STAGES:
        if at <= t:
            label = name
    return label


if __name__ == '__main__':
    with open(os.path.join(HERE, 'shots_evolve.json'), 'w', encoding='utf-8') as f:
        json.dump(shots(), f, indent=1)
    print('wrote shots_evolve.json')
