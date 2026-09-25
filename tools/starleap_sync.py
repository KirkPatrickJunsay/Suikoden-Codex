import argparse
import datetime
import json
import os
import sys

from starleap.lua_reader import read_module
from starleap.pack import build_unit, diff_units, portrait_path, slugify, validate_units, write_pack
from starleap.transform import build_kit, build_stats, parse_infobox
from starleap.wiki import WIKI_PAGE_BASE, WikiClient

CHARACTER_FIELDS = ("_pageName=page,name,title,image,rarity,role,element,element2,"
                    "weapon,weapon2,released,illustration,voice,basedon,game")
STAT_MODULE = "Module:SP stat range/data"
KIT_MODULE = "Module:SP battle kit/data"
TEXT_MODULE = "Module:SP battle kit/text"


def _require(table, key, module):
    if not isinstance(table, dict) or key not in table:
        raise SystemExit(f"error: {module} has no '{key}' table — the wiki format may have changed")
    return table[key]


def _load_published(published_dir):
    manifest_path = os.path.join(published_dir, "manifest.json")
    units_path = os.path.join(published_dir, "units.json")
    if not (os.path.exists(manifest_path) and os.path.exists(units_path)):
        return None, []
    with open(manifest_path, encoding="utf-8") as f:
        manifest = json.load(f)
    with open(units_path, encoding="utf-8") as f:
        units = json.load(f)
    return manifest, units


def fetch_portraits(client, rows, image_info, cache_dir):
    os.makedirs(cache_dir, exist_ok=True)
    portraits, downloaded = {}, 0
    for r in rows:
        info = image_info.get("File:" + r["image"]) if r.get("image") else None
        if not info:
            continue
        url, sha1 = info
        cached = os.path.join(cache_dir, f"{sha1}.png")
        if not os.path.exists(cached):
            with open(cached, "wb") as f:
                f.write(client.download(url))
            downloaded += 1
            print(f"  downloaded {downloaded}: {r['page']}", flush=True)
        with open(cached, "rb") as f:
            portraits[portrait_path(slugify(r["page"]))] = f.read()
    print(f"Portraits: {len(portraits)} ready ({downloaded} downloaded, {len(portraits) - downloaded} from cache)", flush=True)
    return portraits


def main(argv=None, client=None, now=None):
    parser = argparse.ArgumentParser(description="Build the Star Leap guide data pack from Gensopedia STAR LEAP.")
    parser.add_argument("--out", default="build/starleap")
    parser.add_argument("--published", default="docs/starleap")
    parser.add_argument("--cache", default="build/cache")
    parser.add_argument("--allow-shrink", action="store_true")
    args = parser.parse_args(argv)
    client = client or WikiClient()

    print("Fetching roster…", flush=True)
    rows = client.cargo("SP_characters", CHARACTER_FIELDS)
    pages = [r["page"] for r in rows]
    texts = client.wikitext(pages)

    print("Fetching stats and skill kits…", flush=True)
    modules = client.wikitext([STAT_MODULE, KIT_MODULE, TEXT_MODULE])
    for title in (STAT_MODULE, KIT_MODULE, TEXT_MODULE):
        if not modules.get(title):
            raise SystemExit(f"error: could not fetch {title}")
    stat_locals, stat_module = read_module(modules[STAT_MODULE])
    _require(stat_module, "units", STAT_MODULE)
    _, kit_data = read_module(modules[KIT_MODULE])
    for key in ("skills", "allies"):
        _require(kit_data, key, KIT_MODULE)
    _, kit_text = read_module(modules[TEXT_MODULE])
    _require(kit_text, "skills", TEXT_MODULE)

    print("Checking portraits…", flush=True)
    image_info = client.image_info(["File:" + r["image"] for r in rows if r.get("image")])
    portraits = fetch_portraits(client, rows, image_info, os.path.join(args.cache, "portraits"))

    warnings = []
    units = []
    for r in rows:
        stats, weapon = build_stats(r["page"], stat_locals, stat_module)
        kit = build_kit(r["page"], kit_data, kit_text, warnings)
        infobox = parse_infobox(texts.get(r["page"]) or "")
        units.append(build_unit(r, infobox, stats, weapon, kit, WIKI_PAGE_BASE))
    units.sort(key=lambda u: u["id"])

    previous_manifest, previous_units = _load_published(args.published)
    errors, value_warnings = validate_units(units, portraits,
                                            previous_count=len(previous_units) if previous_manifest else None,
                                            allow_shrink=args.allow_shrink)
    for w in warnings + value_warnings:
        print(f"  warning: {w}")
    if errors:
        print(f"Validation failed ({len(errors)} problem(s)); nothing was built:")
        for e in errors:
            print(f"  - {e}")
        return 1

    changes = diff_units(previous_units, units)
    print(f"Changes since published version {previous_manifest['version'] if previous_manifest else '(none)'}:")
    for line in changes or ["(no changes)"]:
        print(f"  {line}")

    generated_at = (now or datetime.datetime.now(datetime.timezone.utc)).strftime("%Y-%m-%dT%H:%M:%SZ")
    manifest = write_pack(args.out, units, portraits,
                          previous_manifest["version"] if previous_manifest else None, generated_at)
    size = sum(f["bytes"] for f in manifest["files"])
    print(f"Built version {manifest['version']}: {len(units)} units, {size / 1_000_000:.1f} MB → {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
