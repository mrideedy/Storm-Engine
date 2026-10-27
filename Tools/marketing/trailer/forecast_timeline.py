"""
Reel 2, "Today's forecast: 9000% chance of lightning": a fake TV weather report that goes off the
rails. Shared by make_forecast_audio.py (music and effects) and make_forecast.py (picture).

Music: 120 BPM (a beat is 0.5 s). Takes come from the trailer recorder playing shots_forecast.json;
a segment plays its take from `take_in` seconds.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 120
BPM = 120
BEAT = 60.0 / BPM
LENGTH = 24.0

SEGMENTS = [  # start, end, take, take_in
    (0.0, 4.0, 'f_calm', 0.3),
    (4.0, 6.0, 'f_cloudy', 0.15),
    (6.0, 8.0, 'f_rain', 0.15),
    (8.0, 10.0, 'f_strike', 0.0),
    (10.0, 14.0, 'f_chaos', 0.0),
    (14.0, 18.0, 'f_yes', 0.0),
    (18.0, 20.0, 'f_anyway', 0.1),
    (20.0, 24.0, 'f_end', 0.0),
]

# The forecast card: from when, weather icon, the number, and whether it is an alarm.
# 'count' rolls the number from 100% to 9000% until the next stage.
FORECAST = [
    (0.0, 'sun', '0%', False),
    (4.0, 'cloud', '10%', False),
    (6.0, 'rain', '60%', False),
    (8.0, 'bolt', '100%', True),
    (10.0, 'bolt', 'count', True),
    (14.0, 'storm', 'YES!', True),
    (18.0, 'sun', '0%', False),
]
UPDATES = [4.0, 6.0, 8.0, 14.0]  # an "UPDATE!" stamp and a news chime
CARD_IN = 0.25
SHOW_UNTIL = 20.0  # card and news banner leave for the end card

# News banner: from when, 'live' or 'breaking', and the ticker line.
NEWS = [
    (0.0, 'live', 'SUNNY SKIES ALL WEEK  •  PERFECT PICNIC WEATHER  •  NOTHING TO WORRY ABOUT  •  '),
    (4.0, 'live', 'SLIGHT BREEZE EXPECTED  •  NOTHING TO WORRY ABOUT  •  PROBABLY  •  '),
    (6.0, 'live', 'BRING AN UMBRELLA  •  MAYBE TWO  •  THE ENGINE IS HUMMING  •  '),
    (8.0, 'breaking', 'LIGHTNING DETECTED  •  STAY INDOORS  •  THE ENGINE IS FINE  •  '),
    (10.0, 'breaking', 'THE ENGINE IS FINE  •  (IT IS NOT FINE)  •  ABANDON PICNIC  •  '),
    (14.0, 'breaking', 'EXPERTS CONFUSED  •  WEATHER: YES  •  RUN  •  '),
    (18.0, 'live', 'BACK TO YOU IN THE STUDIO  •  LOVELY DAY  •  '),
]

CAPTIONS = [(18.12, 19.92, '...ANYWAY.', 'punch', 'center')]
HITS = [(8.0, 0.5), (14.0, 0.55), (20.0, 0.8)]
END_CARD = 20.0


def stage_at(table, t):
    current = table[0]
    for row in table:
        if row[0] <= t:
            current = row
    return current


def sound_events():
    """Game actions that land on screen, in reel seconds: [(time, action)]."""
    with open(os.path.join(HERE, 'shots_forecast.json'), encoding='utf-8') as f:
        shots = json.load(f)
    cues = {take['name']: [(c['t'], c['a']) for c in take.get('cues', [])] for take in shots['takes']}
    events = []
    for start, end, take, take_in in SEGMENTS:
        for t, action in cues.get(take, []):
            at = start + (t - take_in)
            if start <= at < end:
                events.append((at, action.split(':')[0]))
    return sorted(events)
