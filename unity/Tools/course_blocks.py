#!/usr/bin/env python3
"""Lil Foots Course Block Library — Region 1: Pacific Northwest.

Blocks are geometry templates in canvas px (ppu 100). Ground top = y 620.
Plats are [x, y(top), w, h]. Every block STARTS and ENDS grounded at 620.

Rules baked into every block (the auditor double-checks them):
  - base gaps <= 330px so the hard dial (1.15x) never exceeds 360px
  - rises <= 150px (under the 185px jump ceiling)
  - a terminal plat [x, 620, 0] is auto-width: extends to the next block origin

The auditor (course_auditor.py) is the gate — a block that fails reachability
can never reach a level, so the library can never hold a broken pattern.
"""

# Difficulty dial widens the gap before each gap-edge plat:
# dial 0 = 0.85x gaps (easy), 1 = 1.0x, 2 = 1.15x (hard, still <= 360)

BLOCKS = {
    # ---------------- TEACH ----------------
    "mossy_stretch": {
        "phase": "TEACH", "length": 1200,
        "plats": [[0, 620, 1200, 400]],
        "tokens": "ground_trail", "tokens_n": 8,
    },
    # ---------------- PLAY ----------------
    "fern_hop": {
        "phase": "PLAY", "length": 1800,
        "plats": [[0, 620, 500, 400],
                  [800, 470, 200, 90],
                  [1330, 470, 200, 90],
                  [1800, 620, 0, 400]],
        "gap_edges": [1, 2],
        "tokens": "hop_arc",
    },
    "cedar_steps": {
        "phase": "PLAY", "length": 1500,
        "plats": [[0, 620, 400, 400],
                  [520, 470, 320, 90],
                  [960, 320, 320, 90],
                  [1400, 320, 100, 90],
                  [1500, 620, 0, 400]],
        "gap_edges": [],
        "tokens": "stair_arc",
    },
    # ---------------- DEVELOP ----------------
    "stream_logs": {
        "phase": "DEVELOP", "length": 1700,
        "plats": [[0, 620, 450, 400],
                  [770, 560, 160, 80],
                  [1280, 560, 160, 80],
                  [1700, 620, 0, 400]],
        "gap_edges": [1, 2],
        "water": True,
        "tokens": "log_arc",
    },
    "fog_bank": {
        "phase": "DEVELOP", "length": 2600,
        "plats": [[0, 620, 350, 400],
                  [660, 480, 200, 70],
                  [1170, 480, 200, 70],
                  [1680, 480, 200, 70],
                  [2190, 620, 0, 400]],
        "gap_edges": [1, 2, 3],
        "water": True,
        "tokens": "fog_arc",
    },
    "snag_run": {
        "phase": "DEVELOP", "length": 1500,
        "plats": [[0, 620, 1500, 400]],
        "snags": [[400, 560, 120, 60], [900, 560, 120, 60], [1200, 560, 120, 60]],
        "tokens": "ground_trail", "tokens_n": 6,
    },
    "canopy_climb": {
        "phase": "DEVELOP", "length": 1400,
        "plats": [[0, 620, 500, 400],
                  [780, 480, 180, 70],
                  [1080, 340, 180, 70],
                  [1400, 200, 320, 70],
                  [1500, 620, 0, 400]],
        "gap_edges": [1, 2],
        "tokens": "climb_arc",
        "checkpoint": True, "checkpoint_x": 1480,
    },
    # ---------------- CHALLENGE ----------------
    "bramble_gauntlet": {
        "phase": "CHALLENGE", "length": 2200,
        "plats": [[0, 620, 350, 400],
                  [640, 500, 120, 70],
                  [1000, 470, 120, 70],
                  [1360, 500, 120, 70],
                  [1700, 620, 0, 400]],
        "gap_edges": [1, 2, 3],
        "water": True,
        "tokens": "gauntlet_arc",
    },
    "ranger_tower": {
        "phase": "CHALLENGE", "length": 1300,
        "plats": [[0, 620, 400, 400],
                  [620, 480, 130, 70],
                  [880, 340, 130, 70],
                  [1140, 200, 260, 70],
                  [1300, 620, 0, 400]],
        "gap_edges": [1, 2],
        "tokens": "tower_cache",
        "checkpoint": True, "checkpoint_x": 1270,
    },
    # ---------------- FINALE ----------------
    "wendigo_approach": {
        "phase": "FINALE", "length": 1600,
        "plats": [[0, 620, 350, 400],
                  [650, 470, 950, 90],
                  [1600, 620, 0, 400]],
        "tokens": "runway_arc",
    },
    # ---------------- REWARD ----------------
    "flag_clearing": {
        "phase": "REWARD", "length": 1000,
        "plats": [[0, 620, 1000, 400]],
        "tokens": "ground_trail", "tokens_n": 6,
        "gate": True,
    },
    # ---------------- SECRETS (insertable) ----------------
    "gully_secret": {
        "phase": "SECRET", "length": 900,
        "plats": [[0, 620, 250, 400],
                  [250, 770, 400, 200],
                  [650, 620, 250, 400]],
        "tokens": "gully_cache",
        "heart": {"dx": 450, "y": 720},
    },
    "moss_cave": {
        "phase": "SECRET", "length": 800,
        "plats": [[0, 620, 350, 400],
                  [350, 470, 300, 70],
                  [650, 620, 150, 400]],
        "tokens": "cave_cache",
    },
}

PHASE_ORDER = ["TEACH", "PLAY", "DEVELOP", "CHALLENGE", "FINALE", "REWARD"]
