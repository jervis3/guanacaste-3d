"""Lists ribbon commands that have no command handler yet.

Read-only. Parses the C# sources with regular expressions, so it never starts the app.

Usage (from anywhere):
    python -I tools/parity-audit/ribbon_gap.py [--json out.json]

A command id from ``controls/ribbon-catalog*.cs`` that matches no ``case`` in the three
dispatchers ends in ``ShowPlannedRibbonCommand`` (a "planned" status message). A ``case``
whose body is only ``return false;`` does not count as implemented. ``L(...)``/``S(...)`` in
the catalog mean large/compact button size, not implementation status.

Limit: the check is static. A handler registered through an unconventional route is not seen.
"""
import argparse
import json
import re
from collections import defaultdict
from pathlib import Path

DESKTOP = Path(__file__).resolve().parents[2] / "src" / "Guanacaste3D.Desktop"
DISPATCH_FILES = [
    "main-window-professional-ribbons.cs",
    "main-window-structure-commands.cs",
    "main-window-presentation-commands.cs",
    "main-window-wall-join-commands.cs",
    "main-window-wall-edit-commands.cs",
]
# The structure dispatcher renames these catalog ids before its switch.
ALIASES = {"StructColumn": "Column", "WallByFace": "WallFoundation", "ConnectionAuto": "ConnectionAutomation"}

METHOD_RE = re.compile(r"IReadOnlyList<RibbonGroupDefinition>\s+(\w+)\(\)")
GROUP_RE = re.compile(r'G\(\s*"RibbonGroup\.(\w+)"')
COMMAND_RE = re.compile(r'\b[LS]\(\s*"(\w+)"\s*,\s*"RibbonCommand\.(\w+)"')
CASE_RE = re.compile(r'case\s+"([\w.]+)"\s*:')
NEXT_BRANCH_RE = re.compile(r'\bcase\s+"|\bdefault\s*:')


def read_catalog():
    """Returns (workspace, group, id, label) for every command in the ribbon catalog."""
    entries = []
    for path in sorted((DESKTOP / "controls").glob("ribbon-catalog*.cs")):
        workspace, group = path.stem, ""
        for line in path.read_text(encoding="utf-8").splitlines():
            if match := METHOD_RE.search(line):
                workspace = match.group(1)
            if match := GROUP_RE.search(line):
                group = match.group(1)
            for command in COMMAND_RE.finditer(line):
                entries.append((workspace, group, command.group(1), command.group(2)))
    return entries


def read_handled():
    """Returns the ids that have a ``case`` doing more than ``return false``."""
    handled = set()
    for name in DISPATCH_FILES:
        text = (DESKTOP / name).read_text(encoding="utf-8")
        for part in re.split(r'(?=\bcase\s+"|\bdefault\s*:)', text):
            match = CASE_RE.match(part)
            if not match:
                continue
            body = NEXT_BRANCH_RE.split(part[match.end():])[0].strip()
            body = re.sub(r"^(//[^\n]*\n\s*)+", "", body)
            if re.match(r"^return\s+false\s*;", body):
                continue
            handled.add(match.group(1).replace("RibbonCommand.", ""))
    handled.update(alias for alias, target in ALIASES.items() if target in handled)
    return handled


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--json", type=Path, help="also write the per-workspace result to this file")
    args = parser.parse_args()

    handled = read_handled()
    summary = defaultdict(lambda: {"total": 0, "live": 0, "planned": []})
    for workspace, group, command_id, label in read_catalog():
        entry = summary[workspace]
        entry["total"] += 1
        if command_id in handled or label in handled:
            entry["live"] += 1
        else:
            entry["planned"].append(f"{group}/{command_id}")

    total = sum(v["total"] for v in summary.values())
    live = sum(v["live"] for v in summary.values())
    print(f"catalog entries: {total}  with handler: {live}  without handler: {total - live}")
    for workspace, entry in sorted(summary.items(), key=lambda item: -len(item[1]["planned"])):
        print(f"\n[{workspace}] total={entry['total']} live={entry['live']} planned={len(entry['planned'])}")
        if entry["planned"]:
            print("   " + ", ".join(entry["planned"]))
    if args.json:
        args.json.write_text(json.dumps(summary, indent=1, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    main()
