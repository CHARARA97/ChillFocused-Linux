#!/usr/bin/env python3
"""Watch the game's Easy Save 3 state files and report what changes.

The game stores its state as GZip-compressed JSON in

    <prefix>/drive_c/users/steamuser/AppData/LocalLow/nestopi/Chill With You/
        SaveData/Release/v2/<steamid>/*.es3

unencrypted, which makes the game's own state readable from the host -- no need
to touch Unity, BepInEx or the plugin. This tool exists to answer "which field
tells us the player is in a work/creative session?" by diffing snapshots while
someone switches modes in game.

    scripts/watch-game-state.py --duration 120
    scripts/watch-game-state.py --interesting-only

It is also the basis for Focused's game-state gate: the same read produces the
value that decides whether the blacklist is enforced.
"""

from __future__ import annotations

import argparse
import gzip
import json
import os
import re
import sys
import time
from typing import Any, Dict, Iterable, Tuple

DEFAULT_SUFFIX = os.path.join(
    "compatdata", "3548580", "pfx", "drive_c", "users", "steamuser",
    "AppData", "LocalLow", "nestopi", "Chill With You", "SaveData", "Release", "v2",
)

#: Fields whose change is most likely to mean "a work session started or stopped".
INTERESTING = re.compile(
    r"(mode|state|work|focus|timer|pomodoro|session|break|running|active|start|stop)",
    re.IGNORECASE,
)


def default_save_dir() -> str | None:
    steam = os.path.expanduser("~/.local/share/Steam")
    base = os.path.join(steam, "steamapps", DEFAULT_SUFFIX)
    if not os.path.isdir(base):
        return None
    for name in sorted(os.listdir(base)):
        candidate = os.path.join(base, name)
        if os.path.isdir(candidate):
            return candidate
    return None


def flatten(value: Any, prefix: str = "") -> Dict[str, Any]:
    """Flatten nested dicts into dotted keys; lists and scalars stay as values."""
    out: Dict[str, Any] = {}
    if isinstance(value, dict):
        for key, item in value.items():
            if key == "__type":
                continue
            out.update(flatten(item, prefix + key + "." if prefix else key + "."))
    elif isinstance(value, list):
        # Keep lists whole: they are presets/history, not session state.
        out[prefix.rstrip(".")] = json.dumps(value, ensure_ascii=False, sort_keys=True)[:200]
    else:
        out[prefix.rstrip(".")] = value
    return out


def snapshot(directory: str) -> Dict[str, Any]:
    """Read every .es3 into one flat key -> value map."""
    state: Dict[str, Any] = {}
    try:
        names = sorted(os.listdir(directory))
    except OSError:
        return state

    for name in names:
        if not name.endswith(".es3"):
            continue
        path = os.path.join(directory, name)
        try:
            payload = json.loads(gzip.decompress(open(path, "rb").read()).decode("utf-8"))
        except Exception:
            continue
        for key, value in (payload.items() if isinstance(payload, dict) else []):
            short = value.get("__type", "?").split(",")[0] if isinstance(value, dict) else "?"
            body = {k: v for k, v in value.items() if k != "__type"} if isinstance(value, dict) else value
            for flat_key, flat_value in flatten(body).items():
                # Prefix with the type so identical field names stay distinct.
                state["%s::%s" % (short, flat_key)] = flat_value
    return state


def short(value: Any, limit: int = 70) -> str:
    text = repr(value)
    return text if len(text) <= limit else text[: limit - 1] + "\u2026"


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dir", help="save directory (auto-detected by default)")
    parser.add_argument("--interval", type=float, default=1.0, help="poll interval in seconds")
    parser.add_argument("--duration", type=float, default=120.0, help="how long to watch, in seconds")
    parser.add_argument("--interesting-only", action="store_true",
                        help="only report fields whose name looks session-related")
    parser.add_argument("--heartbeat", type=float, default=0.0, metavar="SECONDS",
                        help="also print the key session fields every N seconds")
    args = parser.parse_args(list(argv) if argv is not None else None)

    directory = args.dir or default_save_dir()
    if not directory or not os.path.isdir(directory):
        print("cannot find the save directory; pass --dir", file=sys.stderr)
        return 2

    print("watching %s" % directory)
    print("interval %.1fs, duration %.0fs" % (args.interval, args.duration))
    print("-" * 78)
    sys.stdout.flush()

    previous = snapshot(directory)
    print("[%s] baseline: %d fields from the current save" % (
        time.strftime("%H:%M:%S"), len(previous)))
    sys.stdout.flush()

    #: Fields most likely to distinguish "a work session is running" from idle.
    key_fields = (
        "Bulbul.PomodoroData::value.CurrentTimerMode",
        "Bulbul.PomodoroData::value.LastUpdatedWorkSeconds",
        "Bulbul.PomodoroData::value._lastWorkStartDateTimeString",
        "Bulbul.PlayerDataV3::value.CurrentWorkSeconds",
        "Bulbul.PlayerDataV3::value.PomodoroTotalWorkSeconds",
    )

    deadline = time.time() + args.duration
    next_heartbeat = time.time() + args.heartbeat if args.heartbeat > 0 else float("inf")
    while time.time() < deadline:
        time.sleep(max(0.2, args.interval))
        current = snapshot(directory)

        if time.time() >= next_heartbeat:
            next_heartbeat = time.time() + args.heartbeat
            print("[%s] heartbeat: %s" % (
                time.strftime("%H:%M:%S"),
                "  ".join("%s=%s" % (k.split("::")[1].split(".")[-1], short(current.get(k), 24))
                          for k in key_fields)))

        for key in sorted(set(current) | set(previous)):
            old = previous.get(key, "<absent>")
            new = current.get(key, "<absent>")
            if old == new:
                continue
            if args.interesting_only and not INTERESTING.search(key):
                continue
            print("[%s] %s\n            %s  ->  %s" % (
                time.strftime("%H:%M:%S"), key, short(old), short(new)))
            sys.stdout.flush()

        previous = current

    print("-" * 78)
    print("done")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
