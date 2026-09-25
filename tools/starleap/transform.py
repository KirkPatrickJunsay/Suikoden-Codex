import html
import re

_UNTRANSLATED = re.compile(r"<!--\s*TODO:\s*untranslated,\s*raw JP:\s*(.*?)\s*-->", re.S)
_RUBY = re.compile(r"<ruby=[^>]*>(.*?)</ruby>", re.S)
_SKILL_LV = re.compile(r"\{\{\s*SP skill lv\s*\|([^{}]*)\}\}")
_TEMPLATE = re.compile(r"\{\{[^{}]*\}\}")
_WIKILINK = re.compile(r"\[\[(?:[^\]|]*\|)?([^\]]*)\]\]")
_EXTLINK = re.compile(r"\[https?://\S+\s+([^\]]*)\]")
_BR = re.compile(r"<br\s*/?>", re.I)
_TAG = re.compile(r"<[^>]+>")
_COMMENT = re.compile(r"<!--.*?-->", re.S)

KNOWN_ROW_KINDS = {"physical", "magic", "heal", "drain", "buff", "debuff", "text"}
POWER_LABEL = {"physical": "Physical Power", "magic": "Magic Power"}


def clean_text(value):
    if value is None:
        return ""
    s = str(value)
    s = _UNTRANSLATED.sub(lambda m: m.group(1), s)
    s = _RUBY.sub(lambda m: m.group(1), s)
    s = _COMMENT.sub("", s)
    s = _SKILL_LV.sub(lambda m: " → ".join(p.strip() for p in m.group(1).split("|") if p.strip()), s)
    s = _TEMPLATE.sub("", s)
    s = _WIKILINK.sub(lambda m: m.group(1), s)
    s = _EXTLINK.sub(lambda m: m.group(1), s)
    s = _BR.sub(" ", s)
    s = _TAG.sub("", s)
    s = s.replace("'''", "").replace("''", "")
    s = html.unescape(s)
    return re.sub(r"\s+", " ", s).strip()


def _as_list(value):
    if value is None:
        return []
    if isinstance(value, list):
        return value
    if isinstance(value, dict):
        return [value[k] for k in sorted(value)]
    return [value]


def effect(label, values=(), tags=(), hits=None, note=None):
    return {"label": label, "values": list(values), "tags": list(tags), "hits": hits, "note": note}


def effect_from_row(row):
    kind = row.get("k")
    if kind not in KNOWN_ROW_KINDS:
        raise ValueError(f"unknown effect row kind {kind!r}")
    values = [clean_text(v) for v in _as_list(row.get("v"))]
    if kind == "text":
        return effect(clean_text(row.get("t")))
    if kind == "heal":
        return effect(f"Heals {clean_text(row.get('who')) or 'one ally'}", values, note="HP")
    if kind == "drain":
        return effect("Heals self", values, note="of damage dealt as HP")
    if kind in ("buff", "debuff"):
        stats = [clean_text(s) for s in _as_list(row.get("stat"))] or ["?"]
        word = "Buff" if kind == "buff" else "Debuff"
        return effect(f"{word}: {', '.join(stats)}", values, note=clean_text(row.get("chance")) or None)
    tags = []
    for chip in _as_list(row.get("at")):
        chip = _as_list(chip)
        name = clean_text(chip[0]) if chip else ""
        count = chip[2] if len(chip) > 2 else 1
        if name:
            tags.append(f"{name} {count}")
    hits = row.get("hits")
    return effect(POWER_LABEL[kind], values, tags, hits if isinstance(hits, int) and hits > 1 else None)


def _overlay_text_rows(rows, texts):
    texts = [clean_text(t) for t in _as_list(texts)]
    out, i = [], 0
    for row in rows:
        if row.get("k") == "text" and i < len(texts):
            if texts[i]:
                row = {"k": "text", "t": texts[i]}
            i += 1
        out.append(row)
    return out


def _merge(base, override):
    if not isinstance(override, dict):
        return dict(base)
    merged = dict(base)
    merged.update(override)
    return merged


def _variant(table, skill_key, page):
    per_skill = table.get(skill_key) if isinstance(table, dict) else None
    if isinstance(per_skill, dict):
        return per_skill.get(page)
    return None


def build_kit(page, kit_data, kit_text, warnings=None):
    warnings = warnings if warnings is not None else []
    ally = kit_data["allies"].get(page)
    if ally is None:
        return []
    skills_data = kit_data["skills"]
    text_skills = kit_text.get("skills") or {}
    text_allies = (kit_text.get("allies") or {}).get(page) or {}
    kit = []

    na = ally.get("na") or {}
    if na:
        label = POWER_LABEL["magic"] if na.get("type") == "magic" else POWER_LABEL["physical"]
        tags = [clean_text(na["weapon"])] if na.get("weapon") else []
        kit.append(_skill("Normal", "Normal attack", effects=[effect(label, [clean_text(v) for v in _as_list(na.get("v"))], tags)]))

    tech_letters = iter("ABC")
    for slot in _as_list(ally.get("slots")):
        slot = _as_list(slot)
        kind, key = slot[0], slot[1]
        label = f"Tech {next(tech_letters)}" if kind == "Tech" else kind
        data = skills_data.get(key)
        if data is None:
            warnings.append(f"{page}: no data yet for skill {key!r}")
            kit.append(_skill(label, clean_text(key)))
            continue
        data = _merge(data, _variant(kit_data.get("variants") or {}, key, page))
        text = _merge(text_skills.get(key) or {}, _variant(kit_text.get("variants") or {}, key, page))
        rows = _as_list(data.get("support")) if kind == "Support" and data.get("support") else _as_list(data.get("rows"))
        rows = _overlay_text_rows(rows, text.get("texts"))
        kit.append(_skill(
            label,
            clean_text(text.get("name") or key),
            target=clean_text(data.get("target")) or None,
            uses=clean_text(data.get("uses")) or None,
            rune=clean_text(data.get("rune")) or None,
            description=clean_text(text.get("desc") or data.get("desc")) or None,
            effects=[effect_from_row(r) for r in rows],
        ))

    trait = ally.get("trait")
    if trait:
        over = text_allies.get("trait") or {}
        effects = [effect(clean_text(line)) for line in _as_list(trait.get("lines"))]
        attached = trait.get("attached")
        if attached:
            over_att = over.get("attached") or {}
            name = clean_text(over_att.get("name") or attached.get("name"))
            trigger = clean_text(over_att.get("trigger") or attached.get("trigger"))
            effects.append(effect(f"Attached skill: {name}" + (f" ({trigger})" if trigger else "")))
            effects.extend(effect(clean_text(line)) for line in _as_list(attached.get("lines")))
        kit.append(_skill(
            "Trait",
            clean_text(over.get("name") or trait.get("name")) or "Trait",
            uses=clean_text(trait.get("uses")) or None,
            trigger=clean_text(over.get("trigger") or trait.get("trigger")) or None,
            effects=effects,
        ))

    leader = ally.get("leader")
    if leader:
        over = text_allies.get("leader") or {}
        spec = clean_text(leader.get("spec"))
        kit.append(_skill(
            "Leader",
            "Leader skill",
            trigger=clean_text(over.get("trigger") or leader.get("trigger")) or None,
            description=f"Applies to {spec} allies" if spec else None,
            effects=[effect(clean_text(line)) for line in _as_list(leader.get("lines"))],
        ))
    return kit


def _skill(slot, name, target=None, uses=None, rune=None, description=None, trigger=None, effects=()):
    return {"slot": slot, "name": name, "target": target, "uses": uses, "rune": rune,
            "description": description, "trigger": trigger, "effects": list(effects)}


def build_stats(page, stat_locals, stat_module):
    units = stat_module["units"]
    if isinstance(units, list):
        units = dict(enumerate(units, start=1))
    uid = (stat_locals.get("aliases") or {}).get(page)
    unit = units.get(uid) if uid is not None else None
    if unit is None:
        for u in units.values():
            key = f"{u['name']}: {u['title']}" if u.get("title") else u["name"]
            if key == page:
                unit = u
                break
    if unit is None:
        return None, None
    pair = lambda k: [int(x) for x in _as_list(unit.get(k))] if unit.get(k) is not None else None
    training = {k.lower(): int(v) for k, v in (unit.get("training") or {}).items() if k != "tree"}
    stats = {
        "hp": pair("HP"), "patk": pair("PATK"), "matk": pair("MATK"),
        "pdef": pair("PDEF"), "mdef": pair("MDEF"),
        "agi": unit.get("AGI"), "hit": unit.get("HIT"), "dodge": unit.get("DODGE"),
        "training": training,
    }
    weapon = None
    w = unit.get("weapon")
    if w:
        weapon = {"type": clean_text(w.get("type")), "growth": clean_text(w.get("growth")) or None,
                  "names": [clean_text(n) for n in _as_list(w.get("names"))]}
    return stats, weapon


def split_template_params(body):
    params, depth_t, depth_l, cur = [], 0, 0, []
    i = 0
    while i < len(body):
        two = body[i:i + 2]
        if two == "{{":
            depth_t += 1; cur.append(two); i += 2; continue
        if two == "}}":
            depth_t -= 1; cur.append(two); i += 2; continue
        if two == "[[":
            depth_l += 1; cur.append(two); i += 2; continue
        if two == "]]":
            depth_l -= 1; cur.append(two); i += 2; continue
        c = body[i]
        if c == "|" and depth_t == 0 and depth_l == 0:
            params.append("".join(cur)); cur = []
        else:
            cur.append(c)
        i += 1
    params.append("".join(cur))
    out = {}
    for p in params[1:]:
        if "=" in p:
            k, v = p.split("=", 1)
            out[k.strip()] = v.strip()
    return out


def parse_infobox(wikitext, name="SP character infobox"):
    start = wikitext.find("{{" + name)
    if start < 0:
        return {}
    depth, i = 0, start
    while i < len(wikitext):
        if wikitext.startswith("{{", i):
            depth += 1; i += 2; continue
        if wikitext.startswith("}}", i):
            depth -= 1; i += 2
            if depth == 0:
                return split_template_params(wikitext[start + 2:i - 2])
            continue
        i += 1
    return {}
