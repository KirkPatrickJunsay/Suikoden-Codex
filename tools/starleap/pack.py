import hashlib
import json
import os
import re
import shutil
import urllib.parse

from .transform import clean_text

SCHEMA = 1
SOURCE = "https://starleap.gensopedia.org"
LICENSE = "CC BY-NC-SA 4.0"
ATTRIBUTION = "Adapted from Gensopedia STAR LEAP"
RARITIES = {"108 Stars", "Guest", "SSR", "SR", "R"}
ELEMENTS = {"Fire", "Water", "Wind", "Earth", "Lightning", "Holy", "Dark"}
ROLES = {"Attack", "Defense", "Support", "Recover"}
WEAPONS = {"Sword", "Melee", "Wisdom"}
SLOTS = {"Normal", "Tech A", "Tech B", "Tech C", "Special", "Support", "Trait", "Leader"}
MARKUP = re.compile(r"\[\[|\]\]|\{\{|\}\}|<span|<!--|File:|<[a-zA-Z/]")
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def slugify(page):
    s = page.lower().replace("'", "").replace("'", "")
    return re.sub(r"[^a-z0-9]+", "-", s).strip("-")


def portrait_path(unit_id):
    return f"portraits/{unit_id}.png"


def _opt(value):
    cleaned = clean_text(value)
    return cleaned or None


def build_unit(row, infobox, stats, weapon, kit, wiki_page_base):
    unit_id = slugify(row["page"])
    return {
        "id": unit_id,
        "name": clean_text(row.get("name")),
        "nameJp": _opt(infobox.get("name_jp")),
        "title": clean_text(row.get("title")),
        "titleJp": _opt(infobox.get("title_jp")),
        "rarity": clean_text(row.get("rarity")),
        "role": clean_text(row.get("role")),
        "elements": [e for e in (clean_text(row.get("element")), clean_text(row.get("element2"))) if e],
        "weapons": [w for w in (clean_text(row.get("weapon")), clean_text(row.get("weapon2"))) if w],
        "obtained": _opt(infobox.get("obtained")),
        "origin": _opt(row.get("game")),
        "basedOn": _opt(row.get("basedon")),
        "released": (row.get("released") or "")[:10] or None,
        "voice": _opt(row.get("voice")),
        "illustration": _opt(row.get("illustration")),
        "portrait": portrait_path(unit_id),
        "stats": stats,
        "weapon": weapon,
        "kit": kit,
        "wikiUrl": wiki_page_base + urllib.parse.quote(row["page"].replace(" ", "_")),
    }


def _strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for v in value.values():
            yield from _strings(v)
    elif isinstance(value, list):
        for v in value:
            yield from _strings(v)


def validate_units(units, portraits, previous_count=None, allow_shrink=False):
    errors, warnings = [], []
    seen = set()
    known = {"rarity": RARITIES, "role": ROLES}
    for u in units:
        label = u.get("id") or u.get("name") or "?"
        for field in ("id", "name", "rarity", "role", "portrait"):
            if not u.get(field):
                errors.append(f"{label}: missing {field}")
        if not u.get("elements"):
            errors.append(f"{label}: missing elements")
        if u.get("id") in seen:
            errors.append(f"{label}: duplicate id")
        seen.add(u.get("id"))
        for field, allowed in known.items():
            if u.get(field) and u[field] not in allowed:
                warnings.append(f"{label}: new {field} value {u[field]!r}")
        for e in u.get("elements", []):
            if e not in ELEMENTS:
                warnings.append(f"{label}: new element value {e!r}")
        for w in u.get("weapons", []):
            if w not in WEAPONS:
                warnings.append(f"{label}: new weapon value {w!r}")
        for skill in u.get("kit", []):
            if skill.get("slot") not in SLOTS:
                errors.append(f"{label}: unknown kit slot {skill.get('slot')!r}")
        data = portraits.get(u.get("portrait"))
        if data is None:
            errors.append(f"{label}: portrait not downloaded")
        elif not data.startswith(PNG_SIGNATURE):
            errors.append(f"{label}: portrait is not a PNG")
        for s in _strings(u):
            m = MARKUP.search(s)
            if m:
                errors.append(f"{label}: leftover markup {m.group(0)!r} in {s[:60]!r}")
                break
    if previous_count is not None and len(units) < previous_count and not allow_shrink:
        errors.append(f"unit count dropped from {previous_count} to {len(units)} (use --allow-shrink if intended)")
    return errors, warnings


def diff_units(old_units, new_units):
    old = {u["id"]: u for u in old_units}
    new = {u["id"]: u for u in new_units}
    name = lambda u: f"{u['name']} — {u['title']}" if u.get("title") else u["name"]
    lines = [f"+ {name(new[i])}" for i in sorted(new.keys() - old.keys())]
    lines += [f"- {name(old[i])}" for i in sorted(old.keys() - new.keys())]
    for i in sorted(new.keys() & old.keys()):
        changed = [k for k in new[i] if new[i].get(k) != old[i].get(k)]
        if changed:
            lines.append(f"~ {name(new[i])}: {', '.join(changed)}")
    return lines


def _sha256(data):
    return hashlib.sha256(data).hexdigest()


def write_pack(out_dir, units, portraits, previous_version, generated_at):
    if os.path.isdir(out_dir):
        shutil.rmtree(out_dir)
    os.makedirs(os.path.join(out_dir, "portraits"))
    payloads = {"units.json": json.dumps(units, ensure_ascii=False, separators=(",", ":")).encode("utf-8")}
    for u in units:
        payloads[u["portrait"]] = portraits[u["portrait"]]
    files = []
    for path in sorted(payloads):
        data = payloads[path]
        with open(os.path.join(out_dir, path), "wb") as f:
            f.write(data)
        files.append({"path": path, "sha256": _sha256(data), "bytes": len(data)})
    manifest = {
        "schema": SCHEMA,
        "version": (previous_version or 0) + 1,
        "generatedAt": generated_at,
        "source": SOURCE,
        "license": LICENSE,
        "attribution": ATTRIBUTION,
        "files": files,
    }
    with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    return manifest
