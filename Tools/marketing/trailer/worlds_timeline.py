"""
Reel 8, "which world would you live in?": one smooth orbit around the engine while the whole island
switches world on the beat (world, engine skin, clouds and lightning together), a numbered label in each
world's colours, then all nine worlds snap into a 3x3 grid with "comment your number" and "link in bio".
Shared by make_worlds_audio.py and make_worlds.py; `python worlds_timeline.py` writes shots_worlds.json.

Music: 128 BPM future-bass pop (a bar is 1.875 s). The first bar is the intro; the drop brings world 1,
then a new world every half bar.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 120
BPM = 128
BEAT = 60.0 / BPM
BAR = BEAT * 4
DROP = BAR
STEP = BAR / 2
GRID = DROP + 9 * STEP         # 10.3125 s: the grid finale
FINAL = BAR * 6                # 11.25 s: the last big hit
LENGTH = 13.5

WORLDS = [  # label, equips, label gradient (top, bottom)
    ('SUNNY MEADOW', ['world_meadow', 'skin_classic', 'cloud_classic', 'bolt_classic'], ((200, 255, 150), (60, 185, 90))),
    ('FROST PEAKS', ['world_snow', 'skin_crystal', 'cloud_glacier', 'bolt_electric'], ((235, 250, 255), (110, 185, 255))),
    ('CANYON DESERT', ['world_desert', 'skin_gold', 'cloud_inferno', 'bolt_crimson'], ((255, 228, 150), (240, 128, 58))),
    ('TROPICAL ISLE', ['world_tropic', 'skin_ocean', 'cloud_candy', 'bolt_prism'], ((175, 255, 240), (36, 185, 220))),
    ('SAKURA GARDEN', ['world_sakura', 'skin_blossom', 'cloud_petal', 'bolt_blossom'], ((255, 225, 238), (240, 115, 172))),
    ('STARRY NIGHT', ['world_night', 'skin_midnight', 'cloud_tempest', 'bolt_plasma'], ((210, 210, 255), (112, 100, 232))),
    ('VOLCANO ISLE', ['world_volcano', 'skin_magma', 'cloud_ash', 'bolt_lava'], ((255, 205, 120), (230, 62, 40))),
    ('NEON GRID', ['world_neon', 'skin_cyber', 'cloud_synth', 'bolt_neon'], ((255, 160, 255), (150, 62, 255))),
    ('ALIEN PLANET', ['world_alien', 'skin_rocket', 'cloud_void', 'bolt_emerald'], ((205, 255, 165), (140, 72, 240))),
]
QUESTION = 'which world is yours?'
END_TEXT = [  # start, y, line
    (GRID + 0.75, 0.40, 'comment your number'),
    (GRID + 1.05, 0.52, 'storm engine is free on google play'),
    (GRID + 1.35, 0.635, 'link in bio'),
]


def world_at(i):
    """When world i (0-based) appears."""
    return DROP + i * STEP


def shots():
    cues = [{'t': round(DROP, 4), 'a': 'bigStrike'}, {'t': round(DROP, 4), 'a': 'shake:0.35'}]
    for i, (_, equips, _) in enumerate(WORLDS[1:], 1):
        at = round(world_at(i), 4)
        cues += [{'t': at, 'a': 'equip:' + e} for e in equips]
        cues.append({'t': round(at + 0.02, 4), 'a': 'bigStrike' if i == len(WORLDS) - 1 else 'strike'})
    seconds = round(GRID + 0.6, 3)
    take = {'name': 'worlds9', 'seconds': seconds, 'preroll': 1.5, 'ui': False, 'ease': 0.0,
            'setup': ['calm', 'home:0'] + ['equip:' + e for e in WORLDS[0][1]] + ['sky:0.3'],
            'cues': cues,
            'camera': [{'t': 0, 'anchor': 'dome', 'y': 0.8, 'yaw': -70, 'pitch': 16, 'dist': 30, 'fov': 42, 'sy': 0.12},
                       {'t': seconds, 'anchor': 'dome', 'y': 0.8, 'yaw': 45, 'pitch': 15, 'dist': 24, 'fov': 42, 'sy': 0.12}]}
    return {'fps': FPS, 'width': 1080, 'height': 1920, 'takes': [take]}


if __name__ == '__main__':
    with open(os.path.join(HERE, 'shots_worlds.json'), 'w', encoding='utf-8') as f:
        json.dump(shots(), f, indent=1)
    print('wrote shots_worlds.json')
