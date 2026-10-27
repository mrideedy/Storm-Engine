"""
Reel 3, "Me: just 5 minutes": a clock jumps from 9 PM to midnight, then races to 3 AM while the storms
get bigger. Shared by make_clock_audio.py (music and effects) and make_clock.py (picture).

Music: 120 BPM (a beat is 0.5 s, a bar 2 s). Takes come from two recordings, 'reels' (shots_reels.json)
and 'forecast' (shots_forecast.json); a segment plays its take from `take_in` seconds.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 120
BPM = 120
BEAT = 60.0 / BPM
LENGTH = 16.0

SEGMENTS = [  # start, end, recording, take, take_in
    (0.0, 2.0, 'forecast', 'f_calm', 0.3),
    (2.0, 3.0, 'reels', 'build', 0.2),
    (3.0, 4.0, 'reels', 'upgrade', 0.2),
    (4.0, 6.0, 'reels', 'charge', 0.2),
    (6.0, 7.5, 'reels', 'unleash', 0.25),
    (7.5, 8.5, 'reels', 'chain', 0.0),
    (8.5, 9.5, 'reels', 'mega', 0.1),
    (9.5, 12.0, 'forecast', 'f_chaos', 0.5),
    (12.0, 13.0, 'forecast', 'f_yes', 0.05),
    (13.0, 16.0, 'reels', 'endcard', 0.0),
]
SHOTS = {'reels': 'shots_reels.json', 'forecast': 'shots_forecast.json'}

# The clock, in minutes after midnight of the first day: jumps until the drop, then races to 2:59 AM.
CLOCK = [(0.0, 21 * 60), (2.0, 21 * 60 + 5), (3.0, 22 * 60 + 40), (4.0, 23 * 60 + 59)]
RACE = (6.0, 10.0, 24 * 60, 26 * 60 + 59)  # from, to, first minute, last minute
THREE_AM = 12.0
CLOCK_UNTIL = 13.0
JUMPS = [2.0, 3.0, 4.0, 6.0, 12.0]  # the clock pops

CAPTIONS = [  # start, end, text, style
    (0.0, 2.0, 'ME: "JUST 5 MINUTES"', 'punch'),
    (2.05, 3.0, 'ONE MORE UPGRADE...', 'punch'),
    (3.05, 4.0, 'OK, TWO MORE', 'punch'),
    (4.05, 6.0, 'LAST STORM, I PROMISE', 'punch'),
    (10.0, 12.0, 'WHAT IS SLEEP?', 'punch'),
    (12.05, 13.0, 'WORTH IT.', 'hot'),
]
HITS = [(6.0, 0.6), (12.0, 0.55), (13.0, 0.8)]
END_CARD = 13.0


def minutes_at(t):
    if t >= THREE_AM:
        return 27 * 60
    a, b, first, last = RACE
    if t >= a:
        return int(first + (last - first) * min(1.0, (t - a) / (b - a)))
    value = CLOCK[0][1]
    for at, minutes in CLOCK:
        if at <= t:
            value = minutes
    return value


def clock_text(minutes):
    h, m = divmod(minutes % (24 * 60), 60)
    return '%d:%02d %s' % (h % 12 or 12, m, 'AM' if h < 12 else 'PM')


def sound_events():
    """Game actions that land on screen, in reel seconds: [(time, action)]."""
    cues = {}
    for rec, path in SHOTS.items():
        with open(os.path.join(HERE, path), encoding='utf-8') as f:
            for take in json.load(f)['takes']:
                cues[(rec, take['name'])] = [(c['t'], c['a']) for c in take.get('cues', [])]
    events = []
    for start, end, rec, take, take_in in SEGMENTS:
        for t, action in cues.get((rec, take), []):
            at = start + (t - take_in)
            if start <= at < end:
                events.append((at, action.split(':')[0]))
    return sorted(events)
