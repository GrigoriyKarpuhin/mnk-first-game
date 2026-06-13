#!/usr/bin/env python3
"""C-075 "girl" — dedicated sprite generation (Block C dossier).

Concept: athletic build, black hair in a messy updo, tied-off short-sleeve
shirt with bare midriff, dog tags, chain hanging off the belt, low-rise worn
cargo pants, tall laced boots. Female silhouette: narrow shoulders/waist,
bust, wide hips, rounded seat. Profile art faces LEFT (game flips it right).

Outputs 9 sprites to Assets/Resources/Sprites:
    girl[_walk_1/2].png        front
    girl_side[_walk_1/2].png   profile
    girl_up[_walk_1/2].png     back

Run from repo root:  python3 tools/generate_girl.py
"""
from sprite_lib import SHIRT, char_back, char_front, char_side, ramp

GIRL = dict(
    shirt=SHIRT,                 # worn grey-olive prison shirt, tied off
    pants=ramp((66, 68, 58)),    # darker low-rise cargo pants
    hair=(26, 24, 28),           # black
    hairstyle="tied",            # messy updo with bun
    female=True,                 # bust, waist dip, hips, seat shading
    sleeves="short",             # bare arms
    midriff=True,                # tied shirt, bare stomach
    dogtag=True,                 # tags on chain
    belt="chain",                # chain loop off the left hip
    boots="tall",                # laced shafts
    number=True,                 # C-075 stencil
)

if __name__ == "__main__":
    for frame, suffix in ((0, ""), (1, "_walk_1"), (2, "_walk_2")):
        char_front(GIRL, frame=frame).save(f"girl{suffix}.png")
        char_side(GIRL, frame=frame).save(f"girl_side{suffix}.png")
        char_back(GIRL, frame=frame).save(f"girl_up{suffix}.png")
    print("girl: done")
