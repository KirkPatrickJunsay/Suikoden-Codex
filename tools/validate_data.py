#!/usr/bin/env python3
"""Data-integrity checks for SuikodenCodex bundled assets.

Run standalone:  python3 tools/validate_data.py
Also wired into the .csproj as a pre-build target (Unix) so bad data fails the build.

Checks:
  * entries.json / recruitment.json are valid JSON
  * entry ids are unique; required fields present; category is a known value
  * every entry.imageName resolves to a file in Resources/Images
  * every entry.crossRefs id resolves to an existing entry
  * every recruitment entryId (when set) resolves to an existing entry
  * warns about orphan art_*.jpg images not referenced by any entry
Exits non-zero if any ERROR is found (warnings do not fail the build).
"""
import json, os, sys, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(ROOT, "Resources", "Raw")
IMG = os.path.join(ROOT, "Resources", "Images")
CATEGORIES = {"Character", "Monster", "Item", "Rune", "Region", "Faction", "War", "ComboAttack", "Other"}

errors, warnings = [], []

def load(name):
    try:
        with open(os.path.join(RAW, name), encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        errors.append(f"{name}: invalid JSON — {e}")
        return None

entries = load("entries.json") or []
recruitment = load("recruitment.json") or {}

ids = set()
referenced_images = set()
for e in entries:
    eid = e.get("id")
    if not eid:
        errors.append(f"entry missing id: {e.get('name','?')}"); continue
    if eid in ids:
        errors.append(f"duplicate entry id: {eid}")
    ids.add(eid)
    if not e.get("name"):
        errors.append(f"{eid}: missing name")
    if e.get("category") not in CATEGORIES:
        errors.append(f"{eid}: bad category {e.get('category')!r}")
    img = e.get("imageName")
    if img:
        referenced_images.add(img)
        if not os.path.exists(os.path.join(IMG, img)):
            errors.append(f"{eid}: imageName {img} not found")

for e in entries:
    for ref in e.get("crossRefs", []):
        if ref not in ids:
            errors.append(f"{e.get('id')}: crossRef '{ref}' does not resolve")

for game, lst in recruitment.items():
    nums = set()
    for r in lst:
        n = r.get("num")
        if n in nums:
            errors.append(f"{game}: duplicate recruit num {n}")
        nums.add(n)
        eid = r.get("entryId")
        if eid and eid not in ids:
            errors.append(f"{game} #{n} ({r.get('character')}): entryId '{eid}' does not resolve")

# orphan images (warning only)
if os.path.isdir(IMG):
    for fn in os.listdir(IMG):
        if fn.startswith("art_") and fn.endswith(".jpg") and fn not in referenced_images:
            warnings.append(f"orphan image not referenced by any entry: {fn}")

print(f"[validate_data] entries={len(entries)} "
      f"images_referenced={len(referenced_images)} "
      f"recruitment_games={len(recruitment)}")
for w in warnings:
    print(f"  WARN: {w}")
if errors:
    print(f"[validate_data] FAILED with {len(errors)} error(s):")
    for er in errors:
        print(f"  ERROR: {er}")
    sys.exit(1)
print(f"[validate_data] OK — no errors ({len(warnings)} warning(s)).")
