"""
Reel 4, "Hi, welcome to Storm Engine!": a voice-over tour of the game on fresh takes (shots_voice.json),
short captions for muted viewers, then the coming-soon end card. Shared by make_voice_audio.py (voice,
music and effects) and make_voice.py (picture).

The lines are spoken by Kokoro (an open text-to-speech model) into out/voice/; the edit follows their
lengths, so make_voice_audio.py runs first.
"""
import json
import os
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 120
VOICE_DIR = os.path.join(HERE, 'out', 'voice')
START = 0.25  # the first line starts
GAP = 0.16    # pause between lines
LEAD = 0.1    # cut to the next shot this much before its line
TAIL = 1.7    # end card after the last line

LINES = [  # recording, take, take_in, spoken line, caption
    ('voice', 'v_hello', 0.3, 'Hi, welcome to Storm Engine!', 'HI, WELCOME TO STORM ENGINE!'),
    ('voice', 'v_build', 0.0, 'Build your own weather machine,', 'BUILD A WEATHER MACHINE'),
    ('voice', 'v_charge', 0.0, 'charge it up,', 'CHARGE IT UP'),
    ('voice', 'v_unleash', 0.0, 'and unleash the storm!', 'UNLEASH THE STORM!'),
    ('voice', 'v_chain', 0.0, 'Chain lightning into massive combos,', 'CHAIN LIGHTNING COMBOS'),
    ('voice', 'v_styles', 0.0, 'unlock wild new worlds and styles,', 'WILD WORLDS AND STYLES'),
    ('voice', 'v_decor', 0.0, 'decorate your floating island,', 'DECORATE YOUR ISLAND'),
    ('reels', 'tournament_hud', 0.3, 'and battle for the top of the weekly leaderboard.', 'TOP THE WEEKLY LEADERBOARD'),
    ('voice', 'v_end', 0.0, 'Storm Engine. Coming soon on Google Play!', None),
]
SHOTS = {'voice': 'shots_voice.json', 'reels': 'shots_reels.json'}


def line_path(i):
    return os.path.join(VOICE_DIR, 'line_%02d.wav' % i)


def spans():
    """[(start, end)] of every spoken line, in reel seconds."""
    t, out = START, []
    for i in range(len(LINES)):
        with wave.open(line_path(i)) as f:
            n = f.getnframes() / f.getframerate()
        out.append((t, t + n))
        t += n + GAP
    return out


def edit():
    """Segments [(start, end, recording, take, take_in)], captions [(start, end, text)], end card, length."""
    sp = spans()
    segments = []
    for i, (rec, take, take_in, _, _) in enumerate(LINES):
        start = 0.0 if i == 0 else sp[i][0] - LEAD
        end = sp[i + 1][0] - LEAD if i + 1 < len(LINES) else sp[-1][1] + TAIL
        segments.append((start, end, rec, take, take_in))
    captions = [(s, e, LINES[i][4]) for i, (s, e, *_) in enumerate(segments) if LINES[i][4]]
    return segments, captions, segments[-1][0], segments[-1][1]


def sound_events(segments):
    """Game actions that land on screen, in reel seconds: [(time, action)]."""
    cues = {}
    for rec, path in SHOTS.items():
        with open(os.path.join(HERE, path), encoding='utf-8') as f:
            for take in json.load(f)['takes']:
                cues[(rec, take['name'])] = [(c['t'], c['a']) for c in take.get('cues', [])]
    events = []
    for start, end, rec, take, take_in in segments:
        for t, action in cues.get((rec, take), []):
            at = start + (t - take_in)
            if start <= at < end:
                events.append((at, action.split(':')[0]))
    return sorted(events)
