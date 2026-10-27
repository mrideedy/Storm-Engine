"""
The trailer's edit: which recorded take plays when, the captions, and the sound cues. Shared by
make_trailer_audio.py (music and effects) and make_trailer.py (picture), so cuts, hits and captions
stay on the beat.

Music: 120 BPM, so a beat is 0.5 s and a bar 2 s. Takes come from the trailer recorder
(see shots.json); a segment plays its take from `take_in` seconds.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 30
BPM = 120
BEAT = 60.0 / BPM
LENGTH = 46.0

# Caption styles: 'trailer' = calm movie-trailer white text, 'punch' = bold game caption,
# 'hot' = bold yellow-orange caption for the big moments.
SEGMENTS = [
    # start, end, take, take_in, captions [(from, to, text, style, position)]
    (0.0, 4.0, 'calm', 0.3, [(0.25, 1.9, 'IN A WORLD...', 'trailer', 'center'),
                             (2.0, 3.95, '...WHERE THE WEATHER WAS PERFECTLY FINE', 'trailer', 'center')]),
    (4.0, 6.0, 'calm_engine', 0.2, [(4.25, 5.95, 'ONE ENGINE SAID:', 'trailer', 'center')]),
    (6.0, 8.0, 'nope', 0.0, [(6.04, 7.92, '"NOPE."', 'hot', 'center')]),
    (8.0, 12.0, 'logo', 1.65, []),
    (12.0, 14.0, 'build', 0.0, [(12.05, 13.9, 'BUILD WILD MACHINES', 'punch', 'low')]),
    (14.0, 16.0, 'upgrade', 0.0, [(14.05, 15.9, 'UPGRADE EVERYTHING', 'punch', 'low')]),
    (16.0, 18.0, 'decor', 0.0, [(16.05, 17.9, 'DECORATE YOUR SKY ISLAND', 'punch', 'low')]),
    (18.0, 20.0, 'skins', 0.0, [(18.05, 19.9, 'DRESS UP YOUR ENGINE', 'punch', 'low')]),
    (20.0, 22.0, 'worlds', 0.0, [(20.05, 21.9, 'CHANGE THE WHOLE WORLD', 'punch', 'low')]),
    (22.0, 24.0, 'charge', 0.0, [(22.05, 23.9, 'CHARGE THE CORE...', 'punch', 'low')]),
    (24.0, 26.0, 'unleash', 0.0, [(24.05, 25.9, '...AND UNLEASH THE STORM!', 'hot', 'low')]),
    (26.0, 28.0, 'chain', 0.0, [(26.05, 27.9, 'CHAIN LIGHTNING!', 'hot', 'low')]),
    (28.0, 30.0, 'warning', 0.0, [(28.1, 28.95, 'WARNING:', 'hot', 'center'),
                                  (29.0, 29.97, 'MAY CAUSE EXTREME WEATHER', 'punch', 'center')]),
    (30.0, 32.0, 'tournament_hud', 0.0, [(30.05, 31.9, '...AND EXTREME FUN', 'hot', 'center')]),
    (32.0, 33.0, 'mega', 0.0, [(32.03, 32.95, 'MEGA STORMS!', 'hot', 'low')]),
    (33.0, 34.0, 'tournament_hud', 2.0, [(33.03, 33.95, 'WEEKLY TOURNAMENTS', 'punch', 'center')]),
    (34.0, 35.0, 'home_menu', 0.6, [(34.03, 34.95, 'PLAY ONLINE\nOR OFFLINE', 'punch', 'right')]),
    (35.0, 40.0, 'legend', 0.0, [(35.1, 39.85, 'BECOME A STORM LEGEND', 'hot', 'low')]),
    (40.0, 46.0, 'endcard', 0.4, []),
]

# Edit-level hits (picture flash + sound): the logo slam and the end card.
HITS = [(8.0, 0.85), (40.0, 0.7)]
LOGO_SLAM = (8.0, 11.75)
END_CARD = 40.0
# The cold open is letterboxed like a movie trailer; the bars open on the drop.
LETTERBOX_UNTIL = 8.0


def take_cues():
    """Cue lists of every take (from shots.json): {take: [(t, action), ...]}."""
    with open(os.path.join(HERE, 'shots.json'), encoding='utf-8') as f:
        shots = json.load(f)
    cues = {}
    for take in shots['takes']:
        cues[take['name']] = [(c['t'], c['a']) for c in take.get('cues', [])]
    return cues


def sound_events():
    """Game actions that land on screen, mapped to timeline seconds: [(time, action)]."""
    cues = take_cues()
    events = []
    for start, end, take, take_in, _ in SEGMENTS:
        for t, action in cues.get(take, []):
            at = start + (t - take_in)
            if start <= at < end:
                events.append((at, action.split(':')[0]))
        if take == 'logo':
            # The intro's own strikes (seconds into the intro, which starts 0.1 s before the take),
            # the last and biggest one on the drop.
            for t, kind in ((0.6, 'strike'), (1.15, 'strike'), (1.75, 'bigStrike')):
                at = start + (t - 0.1 - take_in)
                if start <= at < end:
                    events.append((at, kind))
    return sorted(events)
