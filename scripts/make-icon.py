#!/usr/bin/env python3
"""Generate the Thunderstore icon (256x256 PNG).

A dark tile, a mint focus ring, and a prohibition slash: focus, and no.
Run it after changing the colours if you want a different look.

    python3 scripts/make-icon.py
"""

from __future__ import annotations

import os

from PIL import Image, ImageDraw

SIZE = 256
BACKGROUND = (11, 16, 32, 255)      # deep navy
RING = (94, 234, 212, 255)          # mint
SLASH = (251, 113, 133, 255)        # soft red

OUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "packaging", "thunderstore", "icon.png",
)


def main() -> int:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    draw.rounded_rectangle([0, 0, SIZE - 1, SIZE - 1], radius=48, fill=BACKGROUND)

    centre = SIZE // 2
    radius = 84
    draw.ellipse(
        [centre - radius, centre - radius, centre + radius, centre + radius],
        outline=RING,
        width=13,
    )

    # Prohibition slash, top-left to bottom-right, staying inside the ring.
    offset = 54
    draw.line(
        [(centre - offset, centre - offset), (centre + offset, centre + offset)],
        fill=SLASH,
        width=17,
    )

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    image.save(OUT, "PNG")
    print("wrote %s (%dx%d, %d bytes)" % (OUT, SIZE, SIZE, os.path.getsize(OUT)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
