"""
Reel 7, "pov: it's raining and you found the coziest game": three slow shots of the island on a rainy
night (Starry Night world), joined by soft dissolves, native-looking text and a "link in bio" call to
action, over lofi music. Shared by make_cozy_audio.py and make_cozy.py; `python cozy_timeline.py` writes
the recorder's shot list (shots_cozy.json).

Music: 80 BPM lofi, a bar is 3 s; the reel is 4 bars.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 120
BPM = 80
BEAT = 60.0 / BPM
BAR = BEAT * 4
LENGTH = 12.0
FADE = 0.6

SHOTS = [  # start, end (including the dissolve into the next), take
    (0.0, 4.8, 'cz_wide'),
    (4.2, 8.8, 'cz_dome'),
    (8.2, LENGTH, 'cz_end'),
]
STRIKE = 4.2 + 1.6  # the soft lightning in the dome shot, in reel seconds
TEXT = [  # start, end, line
    (0.0, 4.3, "pov: it's raining and you found the coziest game"),
    (4.6, 8.3, 'build a little storm machine and just… relax'),
    (8.6, LENGTH, 'storm engine is free on google play'),
]
LINK = (9.0, LENGTH, 'link in bio')

NIGHT = ['calm', 'home:0', 'equip:world_night', 'equip:skin_classic', 'equip:cloud_classic', 'equip:bolt_classic']
TAKES = [
    {'name': 'cz_wide', 'seconds': 5.2, 'preroll': 2.0, 'ui': False, 'ease': 0.0, 'setup': NIGHT + ['sky:0.65'],
     'camera': [{'t': 0, 'anchor': 'island', 'y': 1.0, 'yaw': 20, 'pitch': 26, 'dist': 46, 'fov': 40, 'sy': 0.04},
                {'t': 5.2, 'anchor': 'island', 'y': 1.0, 'yaw': 32, 'pitch': 25, 'dist': 42, 'fov': 40, 'sy': 0.04}]},
    {'name': 'cz_dome', 'seconds': 5.0, 'preroll': 0.6, 'ui': False, 'ease': 0.0, 'setup': ['sky:0.65'],
     'cues': [{'t': 0.0, 'a': 'chargeRamp:0.25:0.9:4.8'}, {'t': 1.6, 'a': 'strike'}],
     'camera': [{'t': 0, 'anchor': 'dome', 'y': 0.2, 'yaw': 140, 'pitch': 10, 'dist': 13, 'fov': 40, 'sy': 0.16},
                {'t': 5.0, 'anchor': 'dome', 'y': 0.2, 'yaw': 151, 'pitch': 10, 'dist': 11.5, 'fov': 40, 'sy': 0.16}]},
    {'name': 'cz_end', 'seconds': 4.4, 'preroll': 0.6, 'ui': False, 'ease': 0.0, 'setup': ['sky:0.55'],
     'camera': [{'t': 0, 'anchor': 'island', 'y': 1.2, 'yaw': -150, 'pitch': 34, 'dist': 44, 'fov': 40, 'sy': 0.0},
                {'t': 4.4, 'anchor': 'island', 'y': 1.2, 'yaw': -141, 'pitch': 36, 'dist': 51, 'fov': 40, 'sy': 0.0}]},
]

if __name__ == '__main__':
    with open(os.path.join(HERE, 'shots_cozy.json'), 'w', encoding='utf-8') as f:
        json.dump({'fps': FPS, 'width': 1080, 'height': 1920, 'takes': TAKES}, f, indent=1)
    print('wrote shots_cozy.json')
