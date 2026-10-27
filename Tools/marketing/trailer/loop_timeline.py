"""
Reel 5, "i made a game where you control the weather": one continuous orbit around the island while a
storm builds, breaks and clears, made to loop (the end flows back into the start). Native-looking text,
no end card. Shared by make_loop_audio.py and make_loop.py; `python loop_timeline.py` writes the
recorder's shot list (shots_loop.json).

Music: 120 BPM, one loop is 5 bars (10 s). The take is recorded OVERLAP seconds past the end, and that
tail is blended into the first frames so the cut back to the start is invisible.
"""
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 120
BPM = 120
BEAT = 60.0 / BPM
LENGTH = 10.0
OVERLAP = 0.4
DROP = 4.0
CLEAR = 8.0

STRIKES = [(4.0, True), (4.5, False), (5.0, True), (5.5, False), (6.0, True), (6.5, False), (7.0, True),
           (7.5, False), (7.95, True)]  # time, big
TEXT = [  # start, end, line
    (0.0, DROP, 'i made a game where you control the weather'),
    (DROP, CLEAR, "but i might've made it a little too powerful"),
    (CLEAR, LENGTH, '(coming soon on google play)'),
]


def closeness(t):
    """0 far, 1 closest (at 6 s); eases in and out so the loop has no bump."""
    s = t / 12.0 if t < 6.0 else 0.5 + (t - 6.0) / 8.0
    return math.sin(math.pi * s) ** 2


def camera_key(t):
    c = closeness(t)
    return {'t': round(t, 3), 'anchor': 'dome', 'y': 0.6, 'yaw': round(-40 + 36 * t, 2),
            'pitch': round(24 - 13 * c, 2), 'dist': round(46 - 24 * c, 2), 'fov': round(40 + 2 * c, 2),
            'sy': round(0.05 + 0.15 * c, 3)}


def shots():
    cues = [{'t': 0.6, 'a': 'chargeRamp:0.15:1:3.2'}, {'t': 1.0, 'a': 'skyRamp:0.85:2.8'},
            {'t': DROP, 'a': 'skyRamp:1:0.3'}, {'t': DROP, 'a': 'shake:0.4'}, {'t': 6.0, 'a': 'shake:0.3'},
            {'t': 7.95, 'a': 'shake:0.45'}, {'t': CLEAR + 0.1, 'a': 'skyRamp:0:1.7'},
            {'t': CLEAR + 0.1, 'a': 'chargeRamp:1:0.15:1.7'}]
    cues += [{'t': t, 'a': 'bigStrike' if big else 'strike'} for t, big in STRIKES]
    seconds = LENGTH + OVERLAP + 0.1
    keys = [camera_key(i * 0.25) for i in range(int(round(seconds / 0.25)) + 1)]
    take = {'name': 'loop', 'seconds': seconds, 'preroll': 1.5, 'ui': False, 'ease': 0.0,
            'setup': ['calm', 'home:0', 'equip:world_meadow', 'equip:skin_classic', 'equip:cloud_classic',
                      'equip:bolt_classic', 'sky:0'],
            'cues': sorted(cues, key=lambda c: c['t']), 'camera': keys}
    return {'fps': FPS, 'width': 1080, 'height': 1920, 'takes': [take]}


if __name__ == '__main__':
    with open(os.path.join(HERE, 'shots_loop.json'), 'w', encoding='utf-8') as f:
        json.dump(shots(), f, indent=1)
    print('wrote shots_loop.json')
