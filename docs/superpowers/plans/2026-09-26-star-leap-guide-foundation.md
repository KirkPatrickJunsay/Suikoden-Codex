# Star Leap Guide — Foundation + Character Database Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Star Leap tab to Suikoden Codex with a refreshable, offline-first data pack and a 144-unit character database (stats, skill kits, portraits), published by one owner-run command.

**Architecture:** A stdlib-only Python sync (`tools/starleap/`) turns the Star Leap wiki's cargo table, unit infoboxes and datamined Lua modules into a validated pack (`manifest.json` + `units.json` + portraits) that is bundled in the app and published to GitHub Pages. A MAUI-free .NET library (`lib/SuikodenCodex.StarLeap`) loads packs and applies downloaded updates atomically; the MAUI app wraps it in a `StarLeapData` service and three pages (hub, list, detail).

**Tech Stack:** Python 3 standard library + `unittest`; .NET 10 (`net10.0` library, xUnit tests); .NET MAUI 10 Android app (`net10.0-android`), CommunityToolkit.Mvvm; GitHub Pages (`main:/docs`).

**Spec:** `docs/superpowers/specs/2026-09-26-star-leap-guide-foundation-design.md`

## Global Constraints

- Repository root = the MAUI project (`SuikodenCodex.csproj`). Do **not** add a `.sln` at the root — `release.sh` and deploy commands run `dotnet build`/`dotnet publish` without a project argument and would fail with MSB1011.
- **No code comments** in any code you add or change (C#, XAML, Python, shell). Existing comments stay as they are.
- Commits: authored by the repo's configured identity (**Kirk**), **no `Co-Authored-By` or other Claude/AI attribution lines**.
- Data source: `https://starleap.gensopedia.org` (API `/api.php`), licence **CC BY-NC-SA 4.0**; send the User-Agent defined in `tools/starleap/wiki.py`.
- Pack URL: `https://kirkpatrickjunsay.github.io/Suikoden-Codex/starleap/`; `schema` = **1**.
- Deploy/test device: Lenovo Legion Y700 tablet, adb serial **`HA2J6GAS`** (landscape, 3040×1904). Never use emulator `emulator-5554`.
- Python tests: `python3 -m unittest discover -s tools/tests -v` (from repo root). .NET tests: `dotnet test tests/SuikodenCodex.StarLeap.Tests`. App build: `dotnet build -c Debug -f net10.0-android`.
- Exclude quotes and voice lines (verbatim game text) from the pack.

## Before you start (owner decision required)

The working tree on `main` holds earlier, uncommitted work (walkthroughs, `store-assets/`, Star Leap codex entries, this spec). Ask the owner how to commit it; do not decide yourself. Once `git status` is clean:

```bash
git checkout -b feature/star-leap-guide
```

## File Structure

| Path | Responsibility |
|------|----------------|
| `tools/starleap/lua_reader.py` | Reads the table-literal parts of a Lua data module |
| `tools/starleap/transform.py` | Wiki text cleaning, skill-kit and stats transforms, infobox parsing |
| `tools/starleap/wiki.py` | Polite, batched MediaWiki API client |
| `tools/starleap/pack.py` | Unit records, validation, diff, pack writing |
| `tools/starleap_sync.py` | CLI: fetch → transform → validate → diff → write `build/starleap/` |
| `tools/tests/test_*.py` | unittest suites for the above |
| `publish_starleap.sh` | Owner command: sync, confirm, copy to `docs/` + bundle, commit, push |
| `lib/SuikodenCodex.StarLeap/` | Pack models, loader, updater, update policy, formatter, query, classic matcher |
| `tests/SuikodenCodex.StarLeap.Tests/` | xUnit tests for the library |
| `Services/StarLeapData.cs` | App service: picks bundled vs cached pack, update checks, portraits, codex links |
| `ViewModels/SlUnitRow.cs`, `FilterChip.cs`, `StarLeap*ViewModel.cs` | View models |
| `Pages/StarLeap*Page.xaml(.cs)` | Hub, character list, unit detail |
| `Resources/Raw/starleap/` | Bundled baseline pack |
| `docs/starleap/` | Published pack (GitHub Pages) |

---

### Task 1: Confirm GitHub Pages builds from `main:/docs`

The last Pages build (2026-06-25) failed two minutes after the site moved into `docs/`; its logs have expired. `docs/.nojekyll` already exists. Publishing depends on Pages, so diagnose before building anything.

**Files:** none expected.

- [ ] **Step 1: Trigger a fresh Pages build**

```bash
gh api -X POST repos/KirkPatrickJunsay/Suikoden-Codex/pages/builds
```

- [ ] **Step 2: Wait ~1 minute, then read the result**

```bash
gh api repos/KirkPatrickJunsay/Suikoden-Codex/pages/builds/latest --jq '{status, error: .error.message, created_at}'
gh run list --repo KirkPatrickJunsay/Suikoden-Codex --workflow pages-build-deployment --limit 1
```

Expected: `"status": "built"` and a `success` run.

- [ ] **Step 3: Verify the site serves from `docs/`**

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://kirkpatrickjunsay.github.io/Suikoden-Codex/privacy/
```

Expected: `200`.

- [ ] **Step 4: If the build failed, stop and report**

```bash
gh run view <run-id> --repo KirkPatrickJunsay/Suikoden-Codex --log-failed
```

Report the failing log lines to the owner and do not continue past Task 6 until Pages builds (Tasks 2–6 are local and can proceed). Do not guess at a fix.

---

### Task 2: Lua table-literal reader

**Files:**
- Create: `tools/starleap/__init__.py` (empty)
- Create: `tools/starleap/lua_reader.py`
- Test: `tools/tests/test_lua_reader.py`

**Interfaces:**
- Produces: `read_module(src: str) -> tuple[dict, object]` returning `(locals, returned)` — each top-level `local NAME = { … }` in `locals`, the top-level `return { … }` as `returned` (a field naming a local resolves to it). Tables whose keys are exactly `1..n` become Python lists, otherwise dicts. Other statements are skipped. `LuaReadError` on unsupported values.

- [ ] **Step 1: Write the failing test** — `tools/tests/test_lua_reader.py`

```python
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from starleap.lua_reader import LuaReadError, read_module


class ReadModuleTests(unittest.TestCase):
    def test_reads_keyed_array_and_nested_tables(self):
        locals_, _ = read_module("""
            local units = {
                [310001001] = { name = 'Hero', HP = { 72, 639 }, flag = true, none = nil, delta = -3 },
                ['Saki: Academy Star'] = { names = { [1] = 'A', [2] = 'B' } },
            }
        """)
        units = locals_["units"]
        self.assertEqual(units[310001001]["HP"], [72, 639])
        self.assertIs(units[310001001]["flag"], True)
        self.assertEqual(units[310001001]["delta"], -3)
        self.assertEqual(units["Saki: Academy Star"]["names"], ["A", "B"])

    def test_handles_strings_escapes_and_comments(self):
        locals_, _ = read_module("""
            -- line comment
            --[[ block
                 comment ]]
            local t = { a = 'Mind\\'s eye', b = "two\\nlines", c = [[long
text]], d = [==[x]]y]==] }
        """)
        t = locals_["t"]
        self.assertEqual(t["a"], "Mind's eye")
        self.assertEqual(t["b"], "two\nlines")
        self.assertEqual(t["c"], "long\ntext")
        self.assertEqual(t["d"], "x]]y")

    def test_return_table_resolves_locals(self):
        _, returned = read_module("""
            local skills = { ['Ambush'] = { slot = 'Tech' } }
            return { skills = skills, count = 1 }
        """)
        self.assertEqual(returned["skills"]["Ambush"]["slot"], "Tech")
        self.assertEqual(returned["count"], 1)

    def test_skips_loops_and_functions(self):
        locals_, returned = read_module("""
            local units = { [1] = { name = 'A' } }
            local index = {}
            for id, u in pairs(units) do index[u.name] = id end
            local function helper(x) if x then return x end end
            return { units = units }
        """)
        self.assertEqual(returned["units"], [{"name": "A"}])
        self.assertEqual(locals_["index"], [])

    def test_rejects_unsupported_values(self):
        with self.assertRaises(LuaReadError):
            read_module("local t = { a = some.call() }")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run it to verify it fails**

Run: `python3 -m unittest discover -s tools/tests -p "test_lua_reader.py" -v`
Expected: ERROR — `ModuleNotFoundError: No module named 'starleap'`.

- [ ] **Step 3: Implement** — create an empty `tools/starleap/__init__.py`, then `tools/starleap/lua_reader.py`:

```python
import re


class LuaReadError(Exception):
    pass


_TOKEN = re.compile(
    r"""
    (?P<ws>\s+)
  | (?P<lcomment>--\[(?P<leq>=*)\[.*?\](?P=leq)\])
  | (?P<comment>--[^\n]*)
  | (?P<lstring>\[(?P<seq>=*)\[.*?\](?P=seq)\])
  | (?P<string>"(?:[^"\\\n]|\\.)*"|'(?:[^'\\\n]|\\.)*')
  | (?P<number>0[xX][0-9a-fA-F]+|\d+\.?\d*(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?)
  | (?P<name>[A-Za-z_][A-Za-z0-9_]*)
  | (?P<op>\.\.\.|\.\.|==|~=|<=|>=|[{}\[\]=,;()+\-*/%^#<>.:])
    """,
    re.S | re.X,
)

_ESCAPES = {"n": "\n", "t": "\t", "r": "\r", "\\": "\\", '"': '"', "'": "'", "\n": "\n", "a": "\a", "b": "\b", "f": "\f", "v": "\v"}


def _unescape(body):
    out, i = [], 0
    while i < len(body):
        c = body[i]
        if c != "\\":
            out.append(c)
            i += 1
            continue
        i += 1
        if i >= len(body):
            break
        e = body[i]
        if e in _ESCAPES:
            out.append(_ESCAPES[e])
            i += 1
        elif e.isdigit():
            j = i
            while j < len(body) and j - i < 3 and body[j].isdigit():
                j += 1
            out.append(chr(int(body[i:j])))
            i = j
        else:
            out.append(e)
            i += 1
    return "".join(out)


def tokenize(src):
    tokens, pos = [], 0
    while pos < len(src):
        m = _TOKEN.match(src, pos)
        if not m:
            raise LuaReadError(f"unexpected character {src[pos]!r} at offset {pos}")
        pos = m.end()
        kind = m.lastgroup
        if kind in ("leq", "seq"):
            kind = "lcomment" if m.group("lcomment") else "lstring"
        if kind in ("ws", "comment", "lcomment"):
            continue
        text = m.group(0)
        if kind == "string":
            tokens.append(("str", _unescape(text[1:-1])))
        elif kind == "lstring":
            level = len(m.group("seq"))
            body = text[level + 2: -(level + 2)]
            if body.startswith("\n"):
                body = body[1:]
            tokens.append(("str", body))
        elif kind == "number":
            tokens.append(("num", int(text, 16) if text.lower().startswith("0x") else (float(text) if any(c in text for c in ".eE") else int(text))))
        elif kind == "name":
            tokens.append(("name", text))
        else:
            tokens.append(("op", text))
    return tokens


class _Parser:
    def __init__(self, tokens, env):
        self.t, self.i, self.env = tokens, 0, env

    def peek(self, k=0):
        j = self.i + k
        return self.t[j] if j < len(self.t) else ("eof", None)

    def take(self):
        tok = self.peek()
        self.i += 1
        return tok

    def expect(self, kind, value=None):
        tok = self.take()
        if tok[0] != kind or (value is not None and tok[1] != value):
            raise LuaReadError(f"expected {value or kind}, got {tok[1]!r}")
        return tok

    def value(self):
        kind, val = self.peek()
        if kind == "op" and val == "{":
            return self.table()
        if kind == "str":
            self.take()
            parts = [val]
            while self.peek() == ("op", ".."):
                self.take()
                nxt = self.value()
                parts.append(str(nxt))
            return "".join(parts)
        if kind == "num":
            self.take()
            return val
        if kind == "op" and val == "-" and self.peek(1)[0] == "num":
            self.take()
            return -self.take()[1]
        if kind == "name":
            self.take()
            if val == "true":
                return True
            if val == "false":
                return False
            if val == "nil":
                return None
            if val in self.env:
                return self.env[val]
            raise LuaReadError(f"unsupported expression starting with {val!r}")
        raise LuaReadError(f"unsupported value {val!r}")

    def table(self):
        self.expect("op", "{")
        array, keyed = [], {}
        while self.peek() != ("op", "}"):
            kind, val = self.peek()
            if kind == "op" and val == "[":
                self.take()
                key = self.value()
                self.expect("op", "]")
                self.expect("op", "=")
                keyed[key] = self.value()
            elif kind == "name" and self.peek(1) == ("op", "="):
                self.take()
                self.take()
                keyed[val] = self.value()
            else:
                array.append(self.value())
            if self.peek()[0] == "op" and self.peek()[1] in (",", ";"):
                self.take()
            elif self.peek() != ("op", "}"):
                raise LuaReadError(f"expected , or }} in table, got {self.peek()[1]!r}")
        self.expect("op", "}")
        if not keyed:
            return array
        for n, v in enumerate(array, start=1):
            keyed[n] = v
        if keyed and all(isinstance(k, int) for k in keyed) and sorted(keyed) == list(range(1, len(keyed) + 1)):
            return [keyed[k] for k in sorted(keyed)]
        return keyed


def _skip_statement(p):
    depth = 0
    while p.peek()[0] != "eof":
        kind, val = p.peek()
        if kind == "name" and val in ("function", "do", "then", "repeat"):
            depth += 1
        elif kind == "name" and val in ("end", "until"):
            depth -= 1
            p.take()
            if depth <= 0:
                return
            continue
        elif depth == 0 and kind == "name" and val in ("local", "return"):
            return
        p.take()


def read_module(src):
    p = _Parser(tokenize(src), {})
    returned = None
    while p.peek()[0] != "eof":
        kind, val = p.peek()
        if kind == "name" and val == "local" and p.peek(1)[0] == "name" and p.peek(2) == ("op", "=") and p.peek(3) == ("op", "{"):
            p.take()
            name = p.take()[1]
            p.take()
            try:
                p.env[name] = p.table()
            except LuaReadError as e:
                raise LuaReadError(f"local {name}: {e}") from e
            continue
        if kind == "name" and val == "return" and p.peek(1) == ("op", "{"):
            p.take()
            returned = p.table()
            continue
        p.take()
        _skip_statement(p)
    return p.env, returned
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `python3 -m unittest discover -s tools/tests -p "test_lua_reader.py" -v`
Expected: `Ran 5 tests … OK`.

- [ ] **Step 5: Commit**

```bash
git add tools/starleap/__init__.py tools/starleap/lua_reader.py tools/tests/test_lua_reader.py
git commit -m "Add Lua data-module reader for the Star Leap sync"
```

---

### Task 3: Text cleaning, skill kits, stats and infobox parsing

**Files:**
- Create: `tools/starleap/transform.py`
- Test: `tools/tests/test_transform.py`

**Interfaces:**
- Consumes: nothing from earlier tasks (operates on already-parsed dicts).
- Produces:
  - `clean_text(value) -> str` — resolves untranslated `<!-- TODO: untranslated, raw JP: … -->` markers to the Japanese text, `{{SP skill lv|a|b}}` → `a → b`, strips templates, wiki links, HTML and `<ruby>`.
  - `effect_from_row(row: dict) -> dict` with keys `label, values, tags, hits, note`; raises `ValueError` for unknown row kinds (`physical, magic, heal, drain, buff, debuff, text` are known).
  - `build_kit(page, kit_data, kit_text, warnings=None) -> list[dict]` — skills with keys `slot, name, target, uses, rune, description, trigger, effects`; slots `Normal, Tech A/B/C, Special, Support, Trait, Leader`; a slot whose skill has no data yet becomes a name-only skill and appends a message to `warnings`.
  - `build_stats(page, stat_locals, stat_module) -> (stats | None, weapon | None)`; `stats` keys `hp, patk, matk, pdef, mdef` (`[lv1, max]`), `agi, hit, dodge`, `training` (lower-case keys); `weapon` keys `type, growth, names`. Resolves via `stat_locals["aliases"][page]` first, then `"Name: Title"`.
  - `parse_infobox(wikitext, name="SP character infobox") -> dict` — splits parameters on top-level `|` (several parameters may share a line).

- [ ] **Step 1: Write the failing test** — `tools/tests/test_transform.py`

```python
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from starleap.transform import build_kit, build_stats, clean_text, effect_from_row, parse_infobox

KIT_DATA = {
    "skills": {
        "Ringblade Arc": {"slot": "Tech", "target": "Row", "rune": "Leopard Rune", "desc": "data desc",
                          "rows": [{"k": "physical", "v": ["12", "13", "14"], "at": [["Sword", "weapon", 1]]}]},
        "Flowing Pierce": {"slot": "Tech", "target": "Column",
                           "rows": [{"k": "physical", "v": ["16", "18", "20"], "at": [["Water", "element", 1]]}],
                           "support": [{"k": "physical", "v": ["16"], "at": [["Water", "element", 1]]}]},
        "Big Finish": {"slot": "Special", "target": "Single",
                       "rows": [{"k": "physical", "v": ["22", "26"], "hits": 3}, {"k": "text", "t": "old text"}]},
    },
    "variants": {"Big Finish": {"Orhan: Raging War-Scythe": {"target": "All"}}},
    "allies": {
        "Orhan: Raging War-Scythe": {
            "na": {"weapon": "Sword"},
            "slots": [["Tech", "Ringblade Arc"], ["Tech", "Flowing Pierce"], ["Special", "Big Finish"],
                      ["Support", "Flowing Pierce"], ["Tech", "Missing Skill"]],
            "trait": {"name": "raw trait", "trigger": "At the start of a battle",
                      "lines": ["Increase PATK by {{SP skill lv|100%|150%}}"],
                      "attached": {"name": "<strong>Excess HP</strong>", "trigger": "At the start of a battle",
                                   "lines": ["HP overloaded by 20%"]}},
            "leader": {"trigger": "At all times", "spec": "Village Guard", "lines": ["Increase HP by 18%"]},
        }
    },
}
KIT_TEXT = {
    "skills": {"Ringblade Arc": {"desc": "Mows down the enemy."}, "Big Finish": {"texts": ["new text"]}},
    "allies": {"Orhan: Raging War-Scythe": {"trait": {"name": "HEY, HEY!"}}},
}


class CleanTextTests(unittest.TestCase):
    def test_untranslated_marker_becomes_japanese_text(self):
        raw = "<!-- TODO: untranslated, raw JP: <ruby=あらまかみ>荒真神</ruby> -->"
        self.assertEqual(clean_text(raw), "荒真神")

    def test_skill_levels_links_and_tags(self):
        raw = "Increase PATK by {{SP skill lv|80%|130%}} vs [[Toran Checkpoint|the checkpoint]]<br><strong>now</strong>"
        self.assertEqual(clean_text(raw), "Increase PATK by 80% → 130% vs the checkpoint now")


class EffectRowTests(unittest.TestCase):
    def test_power_row_with_chips_and_hits(self):
        effect = effect_from_row({"k": "magic", "v": ["16", "18"], "at": [["Lightning", "element", 2]], "hits": 3})
        self.assertEqual(effect, {"label": "Magic Power", "values": ["16", "18"], "tags": ["Lightning 2"], "hits": 3, "note": None})

    def test_buff_with_stat_list_and_chance(self):
        effect = effect_from_row({"k": "debuff", "v": ["1"], "stat": ["DEF Down", "MDEF Down"], "chance": "(10% chance)"})
        self.assertEqual(effect["label"], "Debuff: DEF Down, MDEF Down")
        self.assertEqual(effect["note"], "(10% chance)")

    def test_heal_defaults_to_one_ally(self):
        self.assertEqual(effect_from_row({"k": "heal", "v": ["40"]})["label"], "Heals one ally")

    def test_unknown_kind_raises(self):
        with self.assertRaises(ValueError):
            effect_from_row({"k": "teleport"})


class BuildKitTests(unittest.TestCase):
    def setUp(self):
        self.warnings = []
        self.kit = build_kit("Orhan: Raging War-Scythe", KIT_DATA, KIT_TEXT, self.warnings)
        self.by_slot = {s["slot"]: s for s in self.kit}

    def test_slots_are_labelled_in_order(self):
        self.assertEqual([s["slot"] for s in self.kit],
                         ["Normal", "Tech A", "Tech B", "Special", "Support", "Tech C", "Trait", "Leader"])

    def test_text_overlay_and_variants_apply(self):
        self.assertEqual(self.by_slot["Tech A"]["description"], "Mows down the enemy.")
        self.assertEqual(self.by_slot["Special"]["target"], "All")
        self.assertEqual(self.by_slot["Special"]["effects"][1]["label"], "new text")

    def test_support_slot_uses_support_rows(self):
        self.assertEqual(self.by_slot["Support"]["effects"][0]["values"], ["16"])

    def test_missing_skill_is_name_only_with_warning(self):
        self.assertEqual(self.by_slot["Tech C"]["name"], "Missing Skill")
        self.assertEqual(self.by_slot["Tech C"]["effects"], [])
        self.assertEqual(len(self.warnings), 1)

    def test_trait_and_leader(self):
        trait = self.by_slot["Trait"]
        self.assertEqual(trait["name"], "HEY, HEY!")
        self.assertEqual([e["label"] for e in trait["effects"]],
                         ["Increase PATK by 100% → 150%", "Attached skill: Excess HP (At the start of a battle)", "HP overloaded by 20%"])
        self.assertEqual(self.by_slot["Leader"]["description"], "Applies to Village Guard allies")


class BuildStatsTests(unittest.TestCase):
    STAT_MODULE = {"units": {
        310001004: {"name": "Hero", "HP": [93, 783], "PATK": [31, 239], "MATK": [31, 242], "PDEF": [29, 199],
                    "MDEF": [29, 202], "AGI": 72, "HIT": 78, "DODGE": 72,
                    "weapon": {"type": "Wand", "growth": "Balanced", "names": ["Fang Scepter"]}},
        310002001: {"name": "Hou", "HP": [72, 639], "training": {"tree": 1, "HP": 90}},
    }}
    STAT_LOCALS = {"aliases": {"Hero: Hero of an Enchanted Land": 310001004}}

    def test_alias_lookup(self):
        stats, weapon = build_stats("Hero: Hero of an Enchanted Land", self.STAT_LOCALS, self.STAT_MODULE)
        self.assertEqual(stats["hp"], [93, 783])
        self.assertEqual(weapon, {"type": "Wand", "growth": "Balanced", "names": ["Fang Scepter"]})

    def test_name_fallback_and_training(self):
        stats, weapon = build_stats("Hou", self.STAT_LOCALS, self.STAT_MODULE)
        self.assertEqual(stats["training"], {"hp": 90})
        self.assertIsNone(weapon)

    def test_unknown_unit(self):
        self.assertEqual(build_stats("Nobody", self.STAT_LOCALS, self.STAT_MODULE), (None, None))


class InfoboxTests(unittest.TestCase):
    def test_parses_params_on_one_line_and_nested_links(self):
        text = "intro {{SP character infobox\n|name= Anji |obtained= |voice= Miyuu\n|from=[[X|Y]] {{JP}}\n}} rest"
        self.assertEqual(parse_infobox(text), {"name": "Anji", "obtained": "", "voice": "Miyuu", "from": "[[X|Y]] {{JP}}"})

    def test_missing_infobox(self):
        self.assertEqual(parse_infobox("no box here"), {})


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run it to verify it fails**

Run: `python3 -m unittest discover -s tools/tests -p "test_transform.py" -v`
Expected: ERROR — `ModuleNotFoundError: No module named 'starleap.transform'`.

- [ ] **Step 3: Implement** — `tools/starleap/transform.py`

```python
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `python3 -m unittest discover -s tools/tests -p "test_transform.py" -v`
Expected: `Ran 16 tests … OK`.

- [ ] **Step 5: Commit**

```bash
git add tools/starleap/transform.py tools/tests/test_transform.py
git commit -m "Add Star Leap wiki text, skill kit and stats transforms"
```

---

### Task 4: Wiki API client

**Files:**
- Create: `tools/starleap/wiki.py`
- Test: `tools/tests/test_wiki.py`

**Interfaces:**
- Produces: `WikiClient(api=API, fetch=http_get, pause=0.5)` with `query(**params) -> dict` (raises `RuntimeError` on API errors), `cargo(table, fields) -> list[dict]` (pages through 500-row batches), `wikitext(titles) -> dict[title, str | None]` (≤50 titles per request, maps normalized titles back), `image_info(file_titles) -> dict[title, (url, sha1) | None]` (url = the 128 px thumbnail when the wiki provides one, else the original), `download(url) -> bytes`. Constants `API`, `WIKI_PAGE_BASE`, `USER_AGENT`.

- [ ] **Step 1: Write the failing test** — `tools/tests/test_wiki.py`

```python
import json
import os
import sys
import unittest
import urllib.parse

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from starleap.wiki import WikiClient


class FakeFetch:
    def __init__(self, responder):
        self.responder = responder
        self.urls = []

    def __call__(self, url):
        self.urls.append(url)
        params = dict(urllib.parse.parse_qsl(urllib.parse.urlparse(url).query))
        return json.dumps(self.responder(params)).encode()


class WikiClientTests(unittest.TestCase):
    def test_cargo_pages_through_results(self):
        def respond(p):
            count = 500 if p["offset"] == "0" else 3
            return {"cargoquery": [{"title": {"n": i}} for i in range(count)]}
        client = WikiClient(fetch=FakeFetch(respond), pause=0)
        self.assertEqual(len(client.cargo("SP_characters", "name")), 503)

    def test_wikitext_maps_normalized_titles_back(self):
        def respond(p):
            return {"query": {"normalized": [{"from": "a_b", "to": "A b"}],
                              "pages": {"1": {"title": "A b", "revisions": [{"slots": {"main": {"*": "text"}}}]},
                                        "-1": {"title": "Missing", "missing": ""}}}}
        client = WikiClient(fetch=FakeFetch(respond), pause=0)
        self.assertEqual(client.wikitext(["a_b", "Missing"]), {"a_b": "text", "Missing": None})

    def test_image_info_prefers_128px_thumbnail(self):
        def respond(p):
            self.assertEqual(p["iiurlwidth"], "128")
            return {"query": {"pages": {
                "1": {"title": "File:X.png", "imageinfo": [{"url": "https://i/X.png", "thumburl": "https://i/128px-X.png", "sha1": "abc"}]},
                "2": {"title": "File:Y.png", "imageinfo": [{"url": "https://i/Y.png", "sha1": "def"}]}}}}
        client = WikiClient(fetch=FakeFetch(respond), pause=0)
        self.assertEqual(client.image_info(["File:X.png", "File:Y.png"]),
                         {"File:X.png": ("https://i/128px-X.png", "abc"), "File:Y.png": ("https://i/Y.png", "def")})

    def test_api_error_raises(self):
        client = WikiClient(fetch=FakeFetch(lambda p: {"error": {"info": "bad"}}), pause=0)
        with self.assertRaises(RuntimeError):
            client.query(action="query")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run it to verify it fails**

Run: `python3 -m unittest discover -s tools/tests -p "test_wiki.py" -v`
Expected: ERROR — `ModuleNotFoundError: No module named 'starleap.wiki'`.

- [ ] **Step 3: Implement** — `tools/starleap/wiki.py`

```python
import json
import time
import urllib.parse
import urllib.request

API = "https://starleap.gensopedia.org/api.php"
WIKI_PAGE_BASE = "https://starleap.gensopedia.org/w/"
USER_AGENT = "SuikodenCodex-StarLeapSync/1.0 (non-commercial fan guide; https://github.com/KirkPatrickJunsay/Suikoden-Codex)"
BATCH = 50


def http_get(url):
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


class WikiClient:
    def __init__(self, api=API, fetch=http_get, pause=0.5):
        self.api = api
        self.fetch = fetch
        self.pause = pause

    def query(self, **params):
        url = self.api + "?" + urllib.parse.urlencode({**params, "format": "json"})
        data = json.loads(self.fetch(url))
        if "error" in data:
            raise RuntimeError(f"wiki API error: {data['error'].get('info')}")
        return data

    def cargo(self, table, fields):
        rows, offset = [], 0
        while True:
            data = self.query(action="cargoquery", tables=table, fields=fields, limit="500", offset=str(offset))
            batch = [item["title"] for item in data.get("cargoquery", [])]
            rows.extend(batch)
            if len(batch) < 500:
                return rows
            offset += 500

    def _pages(self, titles, **params):
        for i in range(0, len(titles), BATCH):
            chunk = titles[i:i + BATCH]
            data = self.query(action="query", titles="|".join(chunk), **params)
            renamed = {n["to"]: n["from"] for n in data["query"].get("normalized", [])}
            for page in data["query"]["pages"].values():
                yield renamed.get(page["title"], page["title"]), page
            if i + BATCH < len(titles):
                time.sleep(self.pause)

    def wikitext(self, titles):
        out = {}
        for title, page in self._pages(titles, prop="revisions", rvprop="content", rvslots="main"):
            revisions = page.get("revisions")
            out[title] = revisions[0]["slots"]["main"]["*"] if revisions else None
        return out

    def image_info(self, file_titles):
        out = {}
        for title, page in self._pages(file_titles, prop="imageinfo", iiprop="url|sha1", iiurlwidth="128"):
            info = page.get("imageinfo")
            out[title] = (info[0].get("thumburl") or info[0]["url"], info[0]["sha1"]) if info else None
        return out

    def download(self, url):
        data = self.fetch(url)
        time.sleep(self.pause / 5)
        return data
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `python3 -m unittest discover -s tools/tests -p "test_wiki.py" -v`
Expected: `Ran 4 tests … OK`.

- [ ] **Step 5: Commit**

```bash
git add tools/starleap/wiki.py tools/tests/test_wiki.py
git commit -m "Add batched Gensopedia STAR LEAP API client"
```

---

### Task 5: Unit records, validation, diff and pack writing

**Files:**
- Create: `tools/starleap/pack.py`
- Test: `tools/tests/test_pack.py`

**Interfaces:**
- Consumes: `clean_text` (Task 3).
- Produces:
  - `slugify(page) -> str`, `portrait_path(unit_id) -> "portraits/<id>.png"`.
  - `build_unit(row, infobox, stats, weapon, kit, wiki_page_base) -> dict` — the `units.json` record (keys `id, name, nameJp, title, titleJp, rarity, role, elements, weapons, obtained, origin, basedOn, released, voice, illustration, portrait, stats, weapon, kit, wikiUrl`).
  - `validate_units(units, portraits, previous_count=None, allow_shrink=False) -> (errors, warnings)` — errors: missing `id/name/rarity/role/portrait/elements`, duplicate id, unknown kit slot, missing or non-PNG portrait, leftover markup, unit count shrinking without `allow_shrink`. Warnings: rarity/role/element/weapon values outside the known sets.
  - `diff_units(old_units, new_units) -> list[str]` (`+ added`, `- removed`, `~ changed: fields`).
  - `write_pack(out_dir, units, portraits, previous_version, generated_at) -> manifest` — writes `units.json` (compact UTF-8), portraits and `manifest.json` (`schema, version, generatedAt, source, license, attribution, files[{path, sha256, bytes}]`); `version = previous_version + 1` (or 1).

- [ ] **Step 1: Write the failing test** — `tools/tests/test_pack.py`

```python
import hashlib
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from starleap.pack import build_unit, diff_units, slugify, validate_units, write_pack

PNG = b"\x89PNG\r\n\x1a\nrest"


def unit(**overrides):
    base = {"id": "flik-blue-thunder", "name": "Flik", "title": "Blue Thunder", "rarity": "Guest", "role": "Attack",
            "elements": ["Lightning"], "weapons": ["Sword"], "portrait": "portraits/flik-blue-thunder.png",
            "kit": [{"slot": "Tech A", "name": "Blue Thunder", "effects": []}]}
    base.update(overrides)
    return base


class SlugTests(unittest.TestCase):
    def test_slugify(self):
        self.assertEqual(slugify("Aegir: Night Lightning's Shadow"), "aegir-night-lightnings-shadow")


class BuildUnitTests(unittest.TestCase):
    def test_maps_roster_row_and_infobox(self):
        row = {"page": "Anji: Top Dog of the Lake", "name": "Anji", "title": "Top Dog of the Lake", "rarity": "SSR",
               "role": "Attack", "element": "Water", "element2": "", "weapon": "Melee", "weapon2": "",
               "released": "2026-07-08 00:00:00", "voice": "[[Someone]]", "illustration": "", "basedon": "Anji",
               "game": "Suikoden"}
        infobox = {"name_jp": "アンジー", "title_jp": "湖の番長", "obtained": "Gacha Banner: [[Dark Galaxy Conqueror]]"}
        u = build_unit(row, infobox, None, None, [], "https://w/")
        self.assertEqual(u["id"], "anji-top-dog-of-the-lake")
        self.assertEqual(u["elements"], ["Water"])
        self.assertEqual(u["released"], "2026-07-08")
        self.assertEqual(u["voice"], "Someone")
        self.assertIsNone(u["illustration"])
        self.assertEqual(u["obtained"], "Gacha Banner: Dark Galaxy Conqueror")
        self.assertEqual(u["wikiUrl"], "https://w/Anji%3A_Top_Dog_of_the_Lake")


class ValidateTests(unittest.TestCase):
    portraits = {"portraits/flik-blue-thunder.png": PNG}

    def check(self, units, **kw):
        return validate_units(units, kw.pop("portraits", self.portraits), **kw)

    def test_valid_unit_passes(self):
        self.assertEqual(self.check([unit()]), ([], []))

    def test_missing_field_duplicate_and_bad_slot(self):
        errors, _ = self.check([unit(role=""), unit(kit=[{"slot": "Ultimate"}])])
        self.assertTrue(any("missing role" in e for e in errors))
        self.assertTrue(any("duplicate id" in e for e in errors))
        self.assertTrue(any("unknown kit slot" in e for e in errors))

    def test_portrait_problems(self):
        self.assertTrue(any("not downloaded" in e for e in self.check([unit()], portraits={})[0]))
        bad = {"portraits/flik-blue-thunder.png": b"GIF89a"}
        self.assertTrue(any("not a PNG" in e for e in self.check([unit()], portraits=bad)[0]))

    def test_leftover_markup(self):
        errors, _ = self.check([unit(obtained="see [[Page]]")])
        self.assertTrue(any("leftover markup" in e for e in errors))

    def test_shrink_blocked_unless_allowed(self):
        self.assertTrue(any("dropped" in e for e in self.check([unit()], previous_count=2)[0]))
        self.assertEqual(self.check([unit()], previous_count=2, allow_shrink=True)[0], [])

    def test_new_enum_values_warn_but_do_not_fail(self):
        errors, warnings = self.check([unit(elements=["Starlight"])])
        self.assertEqual(errors, [])
        self.assertEqual(warnings, ["flik-blue-thunder: new element value 'Starlight'"])


class DiffTests(unittest.TestCase):
    def test_reports_added_removed_changed(self):
        old = [unit(), unit(id="gone", name="Gone", title="")]
        new = [unit(role="Defense"), unit(id="new", name="New", title="Arrival")]
        self.assertEqual(diff_units(old, new), ["+ New — Arrival", "- Gone", "~ Flik — Blue Thunder: role"])


class WritePackTests(unittest.TestCase):
    def test_writes_files_and_manifest(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = os.path.join(tmp, "pack")
            manifest = write_pack(out, [unit()], {"portraits/flik-blue-thunder.png": PNG}, 4, "2026-09-26T00:00:00Z")
            self.assertEqual(manifest["version"], 5)
            self.assertEqual(manifest["schema"], 1)
            self.assertEqual([f["path"] for f in manifest["files"]], ["portraits/flik-blue-thunder.png", "units.json"])
            with open(os.path.join(out, "units.json"), "rb") as f:
                data = f.read()
            entry = next(f for f in manifest["files"] if f["path"] == "units.json")
            self.assertEqual(entry["sha256"], hashlib.sha256(data).hexdigest())
            with open(os.path.join(out, "manifest.json"), encoding="utf-8") as f:
                self.assertEqual(json.load(f)["license"], "CC BY-NC-SA 4.0")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run it to verify it fails**

Run: `python3 -m unittest discover -s tools/tests -p "test_pack.py" -v`
Expected: ERROR — `ModuleNotFoundError: No module named 'starleap.pack'`.

- [ ] **Step 3: Implement** — `tools/starleap/pack.py`

```python
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
    s = page.lower().replace("'", "").replace("’", "")
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `python3 -m unittest discover -s tools/tests -p "test_pack.py" -v`
Expected: `Ran 10 tests … OK`.

- [ ] **Step 5: Commit**

```bash
git add tools/starleap/pack.py tools/tests/test_pack.py
git commit -m "Add Star Leap pack building, validation and diff"
```

---

### Task 6: Sync CLI, publish command, and a real run

**Files:**
- Create: `tools/starleap_sync.py`
- Create: `publish_starleap.sh` (executable)
- Modify: `.gitignore` (append two lines)
- Test: `tools/tests/test_sync.py`

**Interfaces:**
- Consumes: `read_module` (Task 2); `build_kit`, `build_stats`, `parse_infobox` (Task 3); `WikiClient`, `WIKI_PAGE_BASE` (Task 4); `build_unit`, `diff_units`, `portrait_path`, `slugify`, `validate_units`, `write_pack` (Task 5).
- Produces: `starleap_sync.main(argv=None, client=None, now=None) -> int` (0 = built, 1 = validation failed); CLI flags `--out` (default `build/starleap`), `--published` (default `docs/starleap`), `--cache` (default `build/cache`), `--allow-shrink`. Constants `STAT_MODULE`, `KIT_MODULE`, `TEXT_MODULE`.

- [ ] **Step 1: Write the failing test** — `tools/tests/test_sync.py`

```python
import datetime
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import starleap_sync

PNG = b"\x89PNG\r\n\x1a\nface"
STAT = "local units = { [1] = { name = 'Flik', title = 'Blue Thunder', HP = { 80, 700 } } }\nreturn { units = units }"
KIT = ("local skills = { ['Blue Thunder'] = { slot = 'Tech', target = 'Row', "
       "rows = { { k = 'magic', v = { '20', '24' } } } } }\n"
       "local allies = { ['Flik: Blue Thunder'] = { na = { weapon = 'Sword' }, slots = { { 'Tech', 'Blue Thunder' } } } }\n"
       "return { skills = skills, variants = {}, allies = allies }")
TEXT = "return { skills = { ['Blue Thunder'] = { desc = 'Calls lightning.' } }, allies = {} }"


class FakeClient:
    def __init__(self, element="Lightning"):
        self.element = element
        self.downloads = 0

    def cargo(self, table, fields):
        return [{"page": "Flik: Blue Thunder", "name": "Flik", "title": "Blue Thunder", "image": "Flik (SP character face).png",
                 "rarity": "Guest", "role": "Attack", "element": self.element, "element2": "", "weapon": "Sword",
                 "weapon2": "", "released": "2026-08-07 00:00:00", "illustration": "", "voice": "", "basedon": "Flik",
                 "game": "Suikoden"}]

    def wikitext(self, titles):
        texts = {starleap_sync.STAT_MODULE: STAT, starleap_sync.KIT_MODULE: KIT, starleap_sync.TEXT_MODULE: TEXT}
        return {t: texts.get(t, "{{SP character infobox\n|name_jp=フリック\n|obtained=Main Story\n}}") for t in titles}

    def image_info(self, titles):
        return {t: ("https://i/flik.png", "sha1flik") for t in titles}

    def download(self, url):
        self.downloads += 1
        return PNG


class SyncTests(unittest.TestCase):
    NOW = datetime.datetime(2026, 9, 26, tzinfo=datetime.timezone.utc)

    def run_sync(self, tmp, client, *extra):
        args = ["--out", os.path.join(tmp, "out"), "--published", os.path.join(tmp, "published"),
                "--cache", os.path.join(tmp, "cache"), *extra]
        return starleap_sync.main(args, client=client, now=self.NOW)

    def test_builds_a_pack_and_reuses_cached_portraits(self):
        with tempfile.TemporaryDirectory() as tmp:
            client = FakeClient()
            self.assertEqual(self.run_sync(tmp, client), 0)
            with open(os.path.join(tmp, "out", "units.json"), encoding="utf-8") as f:
                units = json.load(f)
            self.assertEqual(units[0]["nameJp"], "フリック")
            self.assertEqual(units[0]["kit"][1]["description"], "Calls lightning.")
            self.assertEqual(units[0]["stats"]["hp"], [80, 700])
            os.rename(os.path.join(tmp, "out"), os.path.join(tmp, "published"))
            self.assertEqual(self.run_sync(tmp, client), 0)
            with open(os.path.join(tmp, "out", "manifest.json"), encoding="utf-8") as f:
                self.assertEqual(json.load(f)["version"], 2)
            self.assertEqual(client.downloads, 1)

    def test_validation_failure_builds_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            client = FakeClient()
            client.cargo = lambda table, fields: [dict(FakeClient().cargo(table, fields)[0], role="")]
            self.assertEqual(self.run_sync(tmp, client), 1)
            self.assertFalse(os.path.exists(os.path.join(tmp, "out")))


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run it to verify it fails**

Run: `python3 -m unittest discover -s tools/tests -p "test_sync.py" -v`
Expected: ERROR — `ModuleNotFoundError: No module named 'starleap_sync'`.

- [ ] **Step 3: Implement** — `tools/starleap_sync.py`

```python
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
```

- [ ] **Step 4: Run the whole Python suite**

Run: `python3 -m unittest discover -s tools/tests -v`
Expected: `Ran 37 tests … OK` (one test prints a deliberate "Validation failed" message).

- [ ] **Step 5: Add the publish command** — `publish_starleap.sh`, then `chmod +x publish_starleap.sh`

```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

branch=$(git rev-parse --abbrev-ref HEAD)
if [ "$branch" != "main" ]; then
  echo "❌ Publish from main (current branch: $branch). GitHub Pages serves docs/ on main."
  exit 1
fi

python3 tools/starleap_sync.py "$@"

version=$(python3 -c "import json; print(json.load(open('build/starleap/manifest.json'))['version'])")
read -r -p "Publish Star Leap guide data version ${version}? [y/N] " answer
if [[ ! "$answer" =~ ^[Yy]$ ]]; then
  echo "Not published."
  exit 0
fi

rm -rf docs/starleap Resources/Raw/starleap
mkdir -p docs Resources/Raw
cp -R build/starleap docs/starleap
cp -R build/starleap Resources/Raw/starleap
git add docs/starleap Resources/Raw/starleap
git commit -m "Update Star Leap guide data (v${version})"
git push origin main

echo ""
echo "✅ Published version ${version}."
echo "   Live within a few minutes at https://kirkpatrickjunsay.github.io/Suikoden-Codex/starleap/manifest.json"
echo "   The next ./release.sh build bundles this version."
```

- [ ] **Step 6: Ignore build output** — append to `.gitignore`:

```gitignore

# ---- Star Leap sync ----
build/
__pycache__/
```

- [ ] **Step 7: Real run against the wiki**

Run: `python3 -u tools/starleap_sync.py`
Expected (≈5 min the first time, ≈12 s once `build/cache/portraits/` is warm): portrait progress lines, one warning `Mina: Freedom Dancer: no data yet for skill "Grand Pas d'Action"` (plus any new-value warnings), a `+` line per unit, and finally `Built version 1: 144 units, 4.8 MB → build/starleap` (counts may be higher if the wiki has grown). If validation fails, report the listed problems to the owner instead of changing validation rules.

- [ ] **Step 8: Commit** (build output stays ignored)

```bash
git add tools/starleap_sync.py tools/tests/test_sync.py publish_starleap.sh .gitignore
git commit -m "Add Star Leap sync CLI and publish command"
```

---

### Task 7: .NET library — pack models and loader (wired into the app build)

**Files:**
- Create: `lib/SuikodenCodex.StarLeap/SuikodenCodex.StarLeap.csproj` (via template)
- Create: `lib/SuikodenCodex.StarLeap/Models.cs`, `PackFormat.cs`, `PackFiles.cs`, `PackLoader.cs`
- Create: `tests/SuikodenCodex.StarLeap.Tests/` (via template), `TestPack.cs`, `PackLoaderTests.cs`
- Modify: `SuikodenCodex.csproj` (exclude `lib/`, `tests/`, `build/`; reference the library)

**Interfaces:**
- Produces (namespace `SuikodenCodex.StarLeap`):
  - Models: `SlFileEntry{Path, Sha256, Bytes}`, `SlManifest{Schema, Version, GeneratedAt, Source, License, Attribution, Files}`, `SlEffect{Label, Values, Tags, Hits, Note}`, `SlSkill{Slot, Name, Target, Uses, Rune, Description, Trigger, Effects}`, `SlStats{Hp, Patk, Matk, Pdef, Mdef (List<int>?), Agi, Hit, Dodge (int?), Training}`, `SlWeapon{Type, Growth, Names}`, `SlUnit{Id, Name, NameJp, Title, TitleJp, Rarity, Role, Elements, Weapons, Obtained, Origin, BasedOn, Released, Voice, Illustration, Portrait, Stats, Weapon, Kit, WikiUrl, IsStarLeapEra, IsClassicVersion, DisplayName}` with `const StarLeapOrigin = "Suikoden STAR LEAP"`, `record SlPack(SlManifest Manifest, IReadOnlyList<SlUnit> Units)`.
  - `PackFormat.SupportedSchema = 1`, `ManifestFile`, `UnitsFile`, `Json` (camelCase, case-insensitive); `PackException`.
  - `interface IPackFiles { Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default); }` (null = missing); `DirectoryPackFiles(string root)`.
  - `PackLoader.LoadAsync(IPackFiles, CancellationToken) -> Task<SlPack>` — throws `PackException` for missing/invalid files or unsupported schema.

- [ ] **Step 1: Scaffold the projects**

```bash
dotnet new classlib -n SuikodenCodex.StarLeap -o lib/SuikodenCodex.StarLeap -f net10.0
rm lib/SuikodenCodex.StarLeap/Class1.cs
dotnet new xunit -n SuikodenCodex.StarLeap.Tests -o tests/SuikodenCodex.StarLeap.Tests -f net10.0
rm tests/SuikodenCodex.StarLeap.Tests/UnitTest1.cs
dotnet add tests/SuikodenCodex.StarLeap.Tests reference lib/SuikodenCodex.StarLeap
```

- [ ] **Step 2: Keep the app from compiling the new folders, and reference the library** — in `SuikodenCodex.csproj`, insert this block immediately **before** the `<ItemGroup>` that contains `<PackageReference Include="Microsoft.Maui.Controls" …>`:

```xml
	<ItemGroup>
		<Compile Remove="lib\**;tests\**;build\**" />
		<None Remove="lib\**;tests\**;build\**" />
		<Content Remove="lib\**;tests\**;build\**" />
		<EmbeddedResource Remove="lib\**;tests\**;build\**" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="lib\SuikodenCodex.StarLeap\SuikodenCodex.StarLeap.csproj" />
	</ItemGroup>

```

- [ ] **Step 3: Write the failing tests** — `tests/SuikodenCodex.StarLeap.Tests/TestPack.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public static class TestPack
{
    public const string UnitsJson = """
        [{"id":"flik-blue-thunder","name":"Flik","nameJp":"フリック","title":"Blue Thunder","rarity":"Guest",
          "role":"Attack","elements":["Lightning"],"weapons":["Sword"],"origin":"Suikoden","basedOn":"Flik",
          "released":"2026-08-07","portrait":"portraits/flik-blue-thunder.png","kit":[]}]
        """;

    public static Dictionary<string, byte[]> Files(int version, int schema = 1, string unitsJson = UnitsJson, byte[]? portrait = null)
    {
        var files = new Dictionary<string, byte[]>
        {
            ["units.json"] = Encoding.UTF8.GetBytes(unitsJson),
            ["portraits/flik-blue-thunder.png"] = portrait ?? new byte[] { 1, 2, 3 },
        };
        var manifest = new SlManifest
        {
            Schema = schema,
            Version = version,
            GeneratedAt = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
            Source = "https://starleap.gensopedia.org",
            License = "CC BY-NC-SA 4.0",
            Attribution = "Adapted from Gensopedia STAR LEAP",
            Files = files.Select(f => new SlFileEntry { Path = f.Key, Sha256 = Sha(f.Value), Bytes = f.Value.Length }).ToList(),
        };
        files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, PackFormat.Json);
        return files;
    }

    public static SlManifest ManifestOf(Dictionary<string, byte[]> files) =>
        JsonSerializer.Deserialize<SlManifest>(files["manifest.json"], PackFormat.Json)!;

    public static string Sha(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public static string WriteToTempDir(Dictionary<string, byte[]> files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "slpack-" + Guid.NewGuid().ToString("N"));
        foreach (var (path, bytes) in files)
        {
            var full = Path.Combine(dir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }
        return dir;
    }

    public static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "slcache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
```

and `tests/SuikodenCodex.StarLeap.Tests/PackLoaderTests.cs`

```csharp
using System.Text;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class PackLoaderTests
{
    [Fact]
    public async Task Loads_manifest_and_units()
    {
        var dir = TestPack.WriteToTempDir(TestPack.Files(version: 3));

        var pack = await PackLoader.LoadAsync(new DirectoryPackFiles(dir));

        Assert.Equal(3, pack.Manifest.Version);
        var unit = Assert.Single(pack.Units);
        Assert.Equal("Flik — Blue Thunder", unit.DisplayName);
        Assert.True(unit.IsClassicVersion);
    }

    [Fact]
    public async Task Rejects_unsupported_schema()
    {
        var dir = TestPack.WriteToTempDir(TestPack.Files(version: 1, schema: 2));

        var ex = await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(new DirectoryPackFiles(dir)));
        Assert.Contains("schema", ex.Message);
    }

    [Fact]
    public async Task Rejects_missing_units_file()
    {
        var files = TestPack.Files(version: 1);
        files.Remove("units.json");
        var dir = TestPack.WriteToTempDir(files);

        var ex = await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(new DirectoryPackFiles(dir)));
        Assert.Contains("units.json", ex.Message);
    }

    [Fact]
    public async Task Rejects_malformed_json()
    {
        var files = TestPack.Files(version: 1);
        files["units.json"] = Encoding.UTF8.GetBytes("[{\"id\":");
        var dir = TestPack.WriteToTempDir(files);

        await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(new DirectoryPackFiles(dir)));
    }
}
```

- [ ] **Step 4: Run to verify they fail**

Run: `dotnet test tests/SuikodenCodex.StarLeap.Tests`
Expected: build errors — `The type or namespace name 'SlManifest' could not be found` (and similar).

- [ ] **Step 5: Implement** — `lib/SuikodenCodex.StarLeap/Models.cs`

```csharp
namespace SuikodenCodex.StarLeap;

public sealed class SlFileEntry
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Bytes { get; set; }
}

public sealed class SlManifest
{
    public int Schema { get; set; }
    public int Version { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string Source { get; set; } = "";
    public string License { get; set; } = "";
    public string Attribution { get; set; } = "";
    public List<SlFileEntry> Files { get; set; } = new();
}

public sealed class SlEffect
{
    public string Label { get; set; } = "";
    public List<string> Values { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int? Hits { get; set; }
    public string? Note { get; set; }
}

public sealed class SlSkill
{
    public string Slot { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Target { get; set; }
    public string? Uses { get; set; }
    public string? Rune { get; set; }
    public string? Description { get; set; }
    public string? Trigger { get; set; }
    public List<SlEffect> Effects { get; set; } = new();
}

public sealed class SlStats
{
    public List<int>? Hp { get; set; }
    public List<int>? Patk { get; set; }
    public List<int>? Matk { get; set; }
    public List<int>? Pdef { get; set; }
    public List<int>? Mdef { get; set; }
    public int? Agi { get; set; }
    public int? Hit { get; set; }
    public int? Dodge { get; set; }
    public Dictionary<string, int> Training { get; set; } = new();
}

public sealed class SlWeapon
{
    public string Type { get; set; } = "";
    public string? Growth { get; set; }
    public List<string> Names { get; set; } = new();
}

public sealed class SlUnit
{
    public const string StarLeapOrigin = "Suikoden STAR LEAP";

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NameJp { get; set; }
    public string Title { get; set; } = "";
    public string? TitleJp { get; set; }
    public string Rarity { get; set; } = "";
    public string Role { get; set; } = "";
    public List<string> Elements { get; set; } = new();
    public List<string> Weapons { get; set; } = new();
    public string? Obtained { get; set; }
    public string? Origin { get; set; }
    public string? BasedOn { get; set; }
    public string? Released { get; set; }
    public string? Voice { get; set; }
    public string? Illustration { get; set; }
    public string Portrait { get; set; } = "";
    public SlStats? Stats { get; set; }
    public SlWeapon? Weapon { get; set; }
    public List<SlSkill> Kit { get; set; } = new();
    public string? WikiUrl { get; set; }

    public bool IsStarLeapEra => Origin == StarLeapOrigin;
    public bool IsClassicVersion => !string.IsNullOrEmpty(Origin) && !IsStarLeapEra;
    public string DisplayName => string.IsNullOrEmpty(Title) ? Name : $"{Name} — {Title}";
}

public sealed record SlPack(SlManifest Manifest, IReadOnlyList<SlUnit> Units);
```

`lib/SuikodenCodex.StarLeap/PackFormat.cs`

```csharp
using System.Text.Json;

namespace SuikodenCodex.StarLeap;

public static class PackFormat
{
    public const int SupportedSchema = 1;
    public const string ManifestFile = "manifest.json";
    public const string UnitsFile = "units.json";

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class PackException : Exception
{
    public PackException(string message, Exception? inner = null) : base(message, inner) { }
}
```

`lib/SuikodenCodex.StarLeap/PackFiles.cs`

```csharp
namespace SuikodenCodex.StarLeap;

public interface IPackFiles
{
    Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default);
}

public sealed class DirectoryPackFiles : IPackFiles
{
    private readonly string _root;

    public DirectoryPackFiles(string root) => _root = root;

    public Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var full = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
        return Task.FromResult<Stream?>(File.Exists(full) ? File.OpenRead(full) : null);
    }
}
```

`lib/SuikodenCodex.StarLeap/PackLoader.cs`

```csharp
using System.Text.Json;

namespace SuikodenCodex.StarLeap;

public static class PackLoader
{
    public static async Task<SlPack> LoadAsync(IPackFiles files, CancellationToken ct = default)
    {
        var manifest = await ReadAsync<SlManifest>(files, PackFormat.ManifestFile, ct);
        if (manifest.Schema < 1 || manifest.Schema > PackFormat.SupportedSchema)
            throw new PackException($"Unsupported pack schema {manifest.Schema}");
        var units = await ReadAsync<List<SlUnit>>(files, PackFormat.UnitsFile, ct);
        return new SlPack(manifest, units);
    }

    private static async Task<T> ReadAsync<T>(IPackFiles files, string path, CancellationToken ct)
    {
        await using var stream = await files.OpenReadAsync(path, ct)
            ?? throw new PackException($"Missing {path}");
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(stream, PackFormat.Json, ct)
                ?? throw new PackException($"Empty {path}");
        }
        catch (JsonException e)
        {
            throw new PackException($"Invalid {path}: {e.Message}", e);
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/SuikodenCodex.StarLeap.Tests`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 7: Confirm the app still builds with the library referenced**

Run: `dotnet build -c Debug -f net10.0-android`
Expected: `Build succeeded`, `0 Error(s)` (no duplicate-attribute errors from `lib/**/obj`).

- [ ] **Step 8: Commit**

```bash
git add lib tests SuikodenCodex.csproj
git commit -m "Add Star Leap pack library with models and loader"
```

---

### Task 8: Safe pack updater and update policy

**Files:**
- Create: `lib/SuikodenCodex.StarLeap/PackUpdater.cs`, `UpdatePolicy.cs`
- Test: `tests/SuikodenCodex.StarLeap.Tests/FakeServer.cs`, `PackUpdaterTests.cs`, `UpdatePolicyTests.cs`

**Interfaces:**
- Consumes: `SlManifest`, `SlFileEntry`, `SlPack`, `IPackFiles`, `DirectoryPackFiles`, `PackLoader`, `PackFormat`, `PackException` (Task 7).
- Produces:
  - `enum UpdateOutcome { UpToDate, Updated, AppUpdateRequired, Failed }`, `record UpdateResult(UpdateOutcome Outcome, string? Reason = null, SlPack? Pack = null)`.
  - `PackUpdater(HttpClient http, Uri baseUri, string cacheRoot)`: `string CurrentDirectory` (`<cacheRoot>/current`), `void RecoverInterruptedSwap()`, `Task<UpdateResult> CheckAndUpdateAsync(SlManifest? currentManifest, IPackFiles? currentFiles, CancellationToken ct = default)` — never throws; reuses unchanged files from `currentFiles`, verifies every sha256, loads the staged pack, swaps `staging → current` keeping `previous` until done.
  - `UpdatePolicy(Func<DateTimeOffset> clock, TimeSpan interval)`: `bool IsDue(DateTimeOffset? lastSuccessfulCheck)`.

- [ ] **Step 1: Write the failing tests** — `tests/SuikodenCodex.StarLeap.Tests/FakeServer.cs`

```csharp
using System.Net;

namespace SuikodenCodex.StarLeap.Tests;

public sealed class FakeServer : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _files;

    public FakeServer(Dictionary<string, byte[]> files) => _files = files;

    public List<string> Requested { get; } = new();
    public bool Offline { get; set; }
    public Dictionary<string, byte[]> Overrides { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Offline)
            throw new HttpRequestException("offline");
        var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Replace("/pack/", ""));
        Requested.Add(path);
        var body = Overrides.TryGetValue(path, out var o) ? o : _files.GetValueOrDefault(path);
        return Task.FromResult(body is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}
```

`tests/SuikodenCodex.StarLeap.Tests/PackUpdaterTests.cs`

```csharp
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class PackUpdaterTests
{
    private static readonly Uri Base = new("https://example.test/pack/");

    private static (PackUpdater updater, FakeServer server, string cache) Create(Dictionary<string, byte[]> remote)
    {
        var server = new FakeServer(remote);
        var cache = TestPack.NewTempDir();
        return (new PackUpdater(new HttpClient(server), Base, cache), server, cache);
    }

    [Fact]
    public async Task Up_to_date_when_remote_version_is_not_newer()
    {
        var (updater, _, _) = Create(TestPack.Files(version: 5));
        var current = TestPack.ManifestOf(TestPack.Files(version: 5));

        var result = await updater.CheckAndUpdateAsync(current, null);

        Assert.Equal(UpdateOutcome.UpToDate, result.Outcome);
    }

    [Fact]
    public async Task Requires_app_update_for_newer_schema()
    {
        var (updater, _, _) = Create(TestPack.Files(version: 9, schema: 2));

        var result = await updater.CheckAndUpdateAsync(null, null);

        Assert.Equal(UpdateOutcome.AppUpdateRequired, result.Outcome);
    }

    [Fact]
    public async Task Downloads_and_installs_a_newer_pack()
    {
        var (updater, _, _) = Create(TestPack.Files(version: 2));

        var result = await updater.CheckAndUpdateAsync(null, null);

        Assert.Equal(UpdateOutcome.Updated, result.Outcome);
        Assert.Equal(2, result.Pack!.Manifest.Version);
        var reloaded = await PackLoader.LoadAsync(new DirectoryPackFiles(updater.CurrentDirectory));
        Assert.Equal(2, reloaded.Manifest.Version);
    }

    [Fact]
    public async Task Downloads_only_files_whose_checksum_changed()
    {
        var old = TestPack.Files(version: 1);
        var oldDir = TestPack.WriteToTempDir(old);
        var (updater, server, _) = Create(TestPack.Files(version: 2, portrait: new byte[] { 9, 9, 9 }));

        var result = await updater.CheckAndUpdateAsync(TestPack.ManifestOf(old), new DirectoryPackFiles(oldDir));

        Assert.Equal(UpdateOutcome.Updated, result.Outcome);
        Assert.Contains("portraits/flik-blue-thunder.png", server.Requested);
        Assert.DoesNotContain("units.json", server.Requested);
    }

    [Fact]
    public async Task Checksum_mismatch_fails_and_keeps_current_pack()
    {
        var first = Create(TestPack.Files(version: 1));
        await first.updater.CheckAndUpdateAsync(null, null);
        var remote = TestPack.Files(version: 2);
        var server = new FakeServer(remote);
        server.Overrides["units.json"] = new byte[] { 0 };
        var updater = new PackUpdater(new HttpClient(server), Base, first.cache);

        var result = await updater.CheckAndUpdateAsync(TestPack.ManifestOf(TestPack.Files(version: 1)), null);

        Assert.Equal(UpdateOutcome.Failed, result.Outcome);
        var kept = await PackLoader.LoadAsync(new DirectoryPackFiles(updater.CurrentDirectory));
        Assert.Equal(1, kept.Manifest.Version);
        Assert.False(Directory.Exists(Path.Combine(first.cache, "staging")));
    }

    [Fact]
    public async Task Network_failure_returns_failed_without_throwing()
    {
        var (updater, server, _) = Create(TestPack.Files(version: 2));
        server.Offline = true;

        var result = await updater.CheckAndUpdateAsync(null, null);

        Assert.Equal(UpdateOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Unsafe_manifest_path_is_rejected()
    {
        var remote = TestPack.Files(version: 2);
        var manifest = TestPack.ManifestOf(remote);
        manifest.Files.Add(new SlFileEntry { Path = "../escape.txt", Sha256 = "00", Bytes = 1 });
        remote["manifest.json"] = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manifest, PackFormat.Json);
        var (updater, _, cache) = Create(remote);

        var result = await updater.CheckAndUpdateAsync(null, null);

        Assert.Equal(UpdateOutcome.Failed, result.Outcome);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(cache)!, "escape.txt")));
    }

    [Fact]
    public void Recovers_previous_pack_after_interrupted_swap()
    {
        var cache = TestPack.NewTempDir();
        var previous = Path.Combine(cache, "previous");
        Directory.CreateDirectory(previous);
        File.WriteAllText(Path.Combine(previous, "marker.txt"), "ok");
        Directory.CreateDirectory(Path.Combine(cache, "staging"));
        var updater = new PackUpdater(new HttpClient(new FakeServer(new())), Base, cache);

        updater.RecoverInterruptedSwap();

        Assert.True(File.Exists(Path.Combine(updater.CurrentDirectory, "marker.txt")));
        Assert.False(Directory.Exists(previous));
        Assert.False(Directory.Exists(Path.Combine(cache, "staging")));
    }
}
```

`tests/SuikodenCodex.StarLeap.Tests/UpdatePolicyTests.cs`

```csharp
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class UpdatePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly UpdatePolicy _policy = new(() => Now, TimeSpan.FromHours(24));

    [Fact] public void Due_when_never_checked() => Assert.True(_policy.IsDue(null));
    [Fact] public void Not_due_within_interval() => Assert.False(_policy.IsDue(Now.AddHours(-23)));
    [Fact] public void Due_after_interval() => Assert.True(_policy.IsDue(Now.AddHours(-25)));
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/SuikodenCodex.StarLeap.Tests`
Expected: build errors — `PackUpdater` / `UpdatePolicy` / `UpdateOutcome` not found.

- [ ] **Step 3: Implement** — `lib/SuikodenCodex.StarLeap/PackUpdater.cs`

```csharp
using System.Security.Cryptography;
using System.Text.Json;

namespace SuikodenCodex.StarLeap;

public enum UpdateOutcome
{
    UpToDate,
    Updated,
    AppUpdateRequired,
    Failed,
}

public sealed record UpdateResult(UpdateOutcome Outcome, string? Reason = null, SlPack? Pack = null);

public sealed class PackUpdater
{
    private readonly HttpClient _http;
    private readonly Uri _base;
    private readonly string _root;

    public PackUpdater(HttpClient http, Uri baseUri, string cacheRoot)
    {
        _http = http;
        _base = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
        _root = cacheRoot;
    }

    public string CurrentDirectory => Path.Combine(_root, "current");
    private string StagingDirectory => Path.Combine(_root, "staging");
    private string PreviousDirectory => Path.Combine(_root, "previous");

    public void RecoverInterruptedSwap()
    {
        if (!Directory.Exists(CurrentDirectory) && Directory.Exists(PreviousDirectory))
            Directory.Move(PreviousDirectory, CurrentDirectory);
        DeleteIfExists(StagingDirectory);
        DeleteIfExists(PreviousDirectory);
    }

    public async Task<UpdateResult> CheckAndUpdateAsync(SlManifest? currentManifest, IPackFiles? currentFiles, CancellationToken ct = default)
    {
        try
        {
            var manifestBytes = await _http.GetByteArrayAsync(new Uri(_base, PackFormat.ManifestFile), ct);
            var remote = JsonSerializer.Deserialize<SlManifest>(manifestBytes, PackFormat.Json)
                ?? throw new PackException("Empty manifest");
            if (remote.Schema > PackFormat.SupportedSchema)
                return new UpdateResult(UpdateOutcome.AppUpdateRequired);
            if (currentManifest is not null && remote.Version <= currentManifest.Version)
                return new UpdateResult(UpdateOutcome.UpToDate);

            DeleteIfExists(StagingDirectory);
            Directory.CreateDirectory(StagingDirectory);
            var currentHashes = currentManifest?.Files.ToDictionary(f => f.Path, f => f.Sha256, StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var entry in remote.Files)
            {
                var target = StagedPath(entry.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var reused = currentFiles is not null
                    && currentHashes.TryGetValue(entry.Path, out var hash)
                    && string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase)
                    && await CopyFromCurrentAsync(currentFiles, entry.Path, target, ct);
                if (!reused)
                    await DownloadAsync(entry.Path, target, ct);
                if (!string.Equals(await Sha256Async(target, ct), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    return Fail($"Checksum mismatch for {entry.Path}");
            }

            await File.WriteAllBytesAsync(Path.Combine(StagingDirectory, PackFormat.ManifestFile), manifestBytes, ct);
            var pack = await PackLoader.LoadAsync(new DirectoryPackFiles(StagingDirectory), ct);
            Swap();
            return new UpdateResult(UpdateOutcome.Updated, Pack: pack);
        }
        catch (Exception e)
        {
            return Fail(e.Message);
        }
    }

    private UpdateResult Fail(string reason)
    {
        TryDelete(StagingDirectory);
        return new UpdateResult(UpdateOutcome.Failed, reason);
    }

    private string StagedPath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(s => s == ".."))
            throw new PackException($"Unsafe path in manifest: {relative}");
        return Path.Combine(StagingDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static async Task<bool> CopyFromCurrentAsync(IPackFiles files, string path, string target, CancellationToken ct)
    {
        await using var source = await files.OpenReadAsync(path, ct);
        if (source is null)
            return false;
        await using var destination = File.Create(target);
        await source.CopyToAsync(destination, ct);
        return true;
    }

    private async Task DownloadAsync(string path, string target, CancellationToken ct)
    {
        var escaped = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        await using var source = await _http.GetStreamAsync(new Uri(_base, escaped), ct);
        await using var destination = File.Create(target);
        await source.CopyToAsync(destination, ct);
    }

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
    }

    private void Swap()
    {
        DeleteIfExists(PreviousDirectory);
        if (Directory.Exists(CurrentDirectory))
            Directory.Move(CurrentDirectory, PreviousDirectory);
        try
        {
            Directory.Move(StagingDirectory, CurrentDirectory);
        }
        catch
        {
            if (!Directory.Exists(CurrentDirectory) && Directory.Exists(PreviousDirectory))
                Directory.Move(PreviousDirectory, CurrentDirectory);
            throw;
        }
        DeleteIfExists(PreviousDirectory);
    }

    private static void DeleteIfExists(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    private static void TryDelete(string dir)
    {
        try { DeleteIfExists(dir); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
```

`lib/SuikodenCodex.StarLeap/UpdatePolicy.cs`

```csharp
namespace SuikodenCodex.StarLeap;

public sealed class UpdatePolicy
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _interval;

    public UpdatePolicy(Func<DateTimeOffset> clock, TimeSpan interval)
    {
        _clock = clock;
        _interval = interval;
    }

    public bool IsDue(DateTimeOffset? lastSuccessfulCheck) =>
        lastSuccessfulCheck is null || _clock() - lastSuccessfulCheck.Value >= _interval;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/SuikodenCodex.StarLeap.Tests`
Expected: `Passed! - Failed: 0, Passed: 15`.

- [ ] **Step 5: Commit**

```bash
git add lib/SuikodenCodex.StarLeap/PackUpdater.cs lib/SuikodenCodex.StarLeap/UpdatePolicy.cs tests/SuikodenCodex.StarLeap.Tests
git commit -m "Add atomic Star Leap pack updater and daily check policy"
```

---

### Task 9: Effect formatting, unit query and classic codex matching

**Files:**
- Create: `lib/SuikodenCodex.StarLeap/EffectFormatter.cs`, `UnitQuery.cs`, `ClassicMatcher.cs`
- Test: `tests/SuikodenCodex.StarLeap.Tests/EffectFormatterTests.cs`, `UnitQueryTests.cs`, `ClassicMatcherTests.cs`

**Interfaces:**
- Consumes: `SlEffect`, `SlUnit` (Task 7).
- Produces:
  - `EffectFormatter.Format(SlEffect) -> string` — `Label` + ` a → b → c` + ` · tag, tag` + ` ×hits` (hits > 1) + ` note`.
  - `enum OriginFilter { Any, StarLeap, Classic }`, `enum UnitSort { Name, Newest }`, `UnitQuery { Text, Rarity, Element, Role, Weapon, Origin, Sort; IReadOnlyList<SlUnit> Apply(IEnumerable<SlUnit>) }` — text matches `Name/NameJp/Title/TitleJp`; `Origin` uses `IsStarLeapEra` / `IsClassicVersion`.
  - `record ClassicCharacter(string Id, string Name, string? Game)`; `ClassicMatcher.Match(SlUnit, IReadOnlyList<ClassicCharacter>) -> string?` — name = `BasedOn ?? Name` (alias Viki → Vicky); single candidate wins; multiple → prefer the unit's own game token (`"Suikoden"` = I; Star Leap-era versions prefer I).

- [ ] **Step 1: Write the failing tests** — `tests/SuikodenCodex.StarLeap.Tests/EffectFormatterTests.cs`

```csharp
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class EffectFormatterTests
{
    [Fact]
    public void Formats_levels_tags_and_hits()
    {
        var effect = new SlEffect { Label = "Physical Power", Values = { "22", "26", "30" }, Tags = { "Sword 1" }, Hits = 3 };
        Assert.Equal("Physical Power 22 → 26 → 30 · Sword 1 ×3", EffectFormatter.Format(effect));
    }

    [Fact]
    public void Formats_note_after_values()
    {
        var effect = new SlEffect { Label = "Heals one ally", Values = { "40", "50" }, Note = "HP" };
        Assert.Equal("Heals one ally 40 → 50 HP", EffectFormatter.Format(effect));
    }

    [Fact]
    public void Formats_plain_text_line()
    {
        Assert.Equal("Fill the special gauge by 100%", EffectFormatter.Format(new SlEffect { Label = "Fill the special gauge by 100%" }));
    }
}
```

`tests/SuikodenCodex.StarLeap.Tests/UnitQueryTests.cs`

```csharp
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class UnitQueryTests
{
    private static readonly SlUnit[] Units =
    {
        new() { Id = "a", Name = "Aegir", NameJp = "エギル", Title = "Night Lightning's Shadow", Rarity = "SSR", Role = "Attack",
                Elements = { "Lightning" }, Weapons = { "Sword" }, Origin = SlUnit.StarLeapOrigin, Released = "2026-07-08" },
        new() { Id = "f", Name = "Flik", NameJp = "フリック", Title = "Blue Thunder", Rarity = "Guest", Role = "Attack",
                Elements = { "Lightning" }, Weapons = { "Sword" }, Origin = "Suikoden", Released = "2026-08-26" },
        new() { Id = "h", Name = "Hisui", NameJp = "ヒスイ", Title = "", Rarity = "108 Stars", Role = "Recover",
                Elements = { "Wind" }, Weapons = { "Wisdom" }, Origin = SlUnit.StarLeapOrigin, Released = "2026-08-07" },
    };

    [Fact]
    public void Matches_japanese_names() =>
        Assert.Equal(new[] { "f" }, new UnitQuery { Text = "フリック" }.Apply(Units).Select(u => u.Id));

    [Fact]
    public void Filters_by_rarity_and_element() =>
        Assert.Equal(new[] { "a" }, new UnitQuery { Rarity = "SSR", Element = "Lightning" }.Apply(Units).Select(u => u.Id));

    [Fact]
    public void Filters_classic_origin() =>
        Assert.Equal(new[] { "f" }, new UnitQuery { Origin = OriginFilter.Classic }.Apply(Units).Select(u => u.Id));

    [Fact]
    public void Sorts_newest_first() =>
        Assert.Equal(new[] { "f", "h", "a" }, new UnitQuery { Sort = UnitSort.Newest }.Apply(Units).Select(u => u.Id));
}
```

`tests/SuikodenCodex.StarLeap.Tests/ClassicMatcherTests.cs`

```csharp
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class ClassicMatcherTests
{
    private static readonly ClassicCharacter[] Codex =
    {
        new("flik", "Flik", "Suikoden I & II"),
        new("vicky", "Vicky", "Series"),
        new("s2-gremio", "Gremio", "Suikoden II"),
        new("s1-gremio", "Gremio", "Suikoden I"),
    };

    [Fact]
    public void Matches_by_based_on_name() =>
        Assert.Equal("flik", ClassicMatcher.Match(new SlUnit { Name = "Flik", BasedOn = "Flik", Origin = "Suikoden" }, Codex));

    [Fact]
    public void Uses_alias_for_known_spelling_difference() =>
        Assert.Equal("vicky", ClassicMatcher.Match(new SlUnit { Name = "Viki", Origin = "Suikoden" }, Codex));

    [Fact]
    public void Prefers_entry_from_the_origin_game() =>
        Assert.Equal("s1-gremio", ClassicMatcher.Match(new SlUnit { Name = "Gremio", Origin = "Suikoden" }, Codex));

    [Fact]
    public void Star_leap_era_version_links_to_its_classic_entry() =>
        Assert.Equal("s1-gremio", ClassicMatcher.Match(new SlUnit { Name = "Gremio", Origin = SlUnit.StarLeapOrigin }, Codex));

    [Fact]
    public void New_star_leap_characters_have_no_classic_entry() =>
        Assert.Null(ClassicMatcher.Match(new SlUnit { Name = "Hisui", Origin = SlUnit.StarLeapOrigin }, Codex));
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/SuikodenCodex.StarLeap.Tests`
Expected: build errors — `EffectFormatter`, `UnitQuery`, `ClassicMatcher` not found.

- [ ] **Step 3: Implement** — `lib/SuikodenCodex.StarLeap/EffectFormatter.cs`

```csharp
using System.Text;

namespace SuikodenCodex.StarLeap;

public static class EffectFormatter
{
    public static string Format(SlEffect effect)
    {
        var text = new StringBuilder(effect.Label);
        if (effect.Values.Count > 0)
            text.Append(' ').Append(string.Join(" → ", effect.Values));
        if (effect.Tags.Count > 0)
            text.Append(" · ").Append(string.Join(", ", effect.Tags));
        if (effect.Hits is > 1)
            text.Append(" ×").Append(effect.Hits.Value);
        if (!string.IsNullOrEmpty(effect.Note))
            text.Append(' ').Append(effect.Note);
        return text.ToString();
    }
}
```

`lib/SuikodenCodex.StarLeap/UnitQuery.cs`

```csharp
namespace SuikodenCodex.StarLeap;

public enum OriginFilter
{
    Any,
    StarLeap,
    Classic,
}

public enum UnitSort
{
    Name,
    Newest,
}

public sealed class UnitQuery
{
    public string? Text { get; init; }
    public string? Rarity { get; init; }
    public string? Element { get; init; }
    public string? Role { get; init; }
    public string? Weapon { get; init; }
    public OriginFilter Origin { get; init; }
    public UnitSort Sort { get; init; }

    public IReadOnlyList<SlUnit> Apply(IEnumerable<SlUnit> units)
    {
        var text = Text?.Trim();
        var result = units.Where(u =>
            (string.IsNullOrEmpty(text) || Matches(u, text)) &&
            (Rarity is null || u.Rarity == Rarity) &&
            (Element is null || u.Elements.Contains(Element)) &&
            (Role is null || u.Role == Role) &&
            (Weapon is null || u.Weapons.Contains(Weapon)) &&
            (Origin == OriginFilter.Any
                || (Origin == OriginFilter.StarLeap && u.IsStarLeapEra)
                || (Origin == OriginFilter.Classic && u.IsClassicVersion)));

        var ordered = Sort == UnitSort.Newest
            ? result.OrderByDescending(u => u.Released ?? "", StringComparer.Ordinal)
                .ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            : result.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.Title, StringComparer.OrdinalIgnoreCase);
        return ordered.ToList();
    }

    private static bool Matches(SlUnit u, string text) =>
        Contains(u.Name, text) || Contains(u.NameJp, text) || Contains(u.Title, text) || Contains(u.TitleJp, text);

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
```

`lib/SuikodenCodex.StarLeap/ClassicMatcher.cs`

```csharp
using System.Text.RegularExpressions;

namespace SuikodenCodex.StarLeap;

public sealed record ClassicCharacter(string Id, string Name, string? Game);

public static partial class ClassicMatcher
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Viki"] = "Vicky",
    };

    public static string? Match(SlUnit unit, IReadOnlyList<ClassicCharacter> classics)
    {
        var name = unit.BasedOn ?? unit.Name;
        if (Aliases.TryGetValue(name, out var alias))
            name = alias;
        var candidates = classics.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count <= 1)
            return candidates.FirstOrDefault()?.Id;
        var token = unit.IsClassicVersion ? GameToken(unit.Origin!) : "I";
        return (candidates.FirstOrDefault(c => token is not null && GameTokens(c.Game).Contains(token)) ?? candidates[0]).Id;
    }

    private static string? GameToken(string origin)
    {
        var trimmed = origin.Trim();
        if (string.Equals(trimmed, "Suikoden", StringComparison.OrdinalIgnoreCase))
            return "I";
        var match = RomanRegex().Match(trimmed);
        return match.Success ? match.Value : null;
    }

    private static IReadOnlyCollection<string> GameTokens(string? game) =>
        string.IsNullOrEmpty(game) ? Array.Empty<string>() : RomanRegex().Matches(game).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\b(III|II|IV|I|V)\b")]
    private static partial Regex RomanRegex();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/SuikodenCodex.StarLeap.Tests`
Expected: `Passed! - Failed: 0, Passed: 27`.

- [ ] **Step 5: Commit**

```bash
git add lib/SuikodenCodex.StarLeap tests/SuikodenCodex.StarLeap.Tests
git commit -m "Add Star Leap effect formatting, unit filtering and classic codex matching"
```

---

### Task 10: Bundle the pack, gate it at build time, add the app data service

**Files:**
- Modify: `tools/validate_data.py`
- Create: `Resources/Raw/starleap/` (copied from `build/starleap/`)
- Create: `Services/StarLeapData.cs`
- Modify: `MauiProgram.cs`

**Interfaces:**
- Consumes: everything in `SuikodenCodex.StarLeap` (Tasks 7–9); `CodexData.EnsureLoadedAsync()`, `CodexData.Entries`, `EntryCategory.Character` (existing).
- Produces: `StarLeapData` singleton — `static Uri PackUri`, `const string Credit`, `SlManifest? Manifest`, `IReadOnlyList<SlUnit> Units`, `string Status`, `event EventHandler? Changed`, `Task EnsureLoadedAsync()`, `Task<UpdateResult> CheckForUpdatesAsync(bool force)`, `SlUnit? GetUnit(string id)`, `IReadOnlyList<SlUnit> OtherVersionsOf(SlUnit)`, `Task<string?> ClassicEntryIdAsync(SlUnit)`, `ImageSource Portrait(SlUnit)`. Loads the higher-version pack of bundled (`starleap/…` app-package files) vs cached (`AppDataDirectory/starleap/current`).

- [ ] **Step 1: Extend the build-time validator (failing first)** — in `tools/validate_data.py`:

Change the import line `import json, os, sys, re` to:

```python
import hashlib, json, os, sys, re
```

Insert this block immediately **before** the line that starts `print(f"[validate_data] entries={len(entries)} "`:

```python
STARLEAP = os.path.join(RAW, "starleap")


def check_starleap():
    manifest_path = os.path.join(STARLEAP, "manifest.json")
    if not os.path.exists(manifest_path):
        errors.append("starleap: bundled pack missing (Resources/Raw/starleap/manifest.json)")
        return 0
    try:
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f)
    except Exception as e:
        errors.append(f"starleap/manifest.json: invalid JSON — {e}")
        return 0
    if manifest.get("schema") != 1:
        errors.append(f"starleap: unsupported schema {manifest.get('schema')!r}")
    listed = set()
    for entry in manifest.get("files", []):
        listed.add(entry.get("path"))
        path = os.path.join(STARLEAP, entry.get("path", ""))
        if not os.path.isfile(path):
            errors.append(f"starleap: {entry.get('path')} is listed but missing")
            continue
        with open(path, "rb") as f:
            if hashlib.sha256(f.read()).hexdigest() != entry.get("sha256"):
                errors.append(f"starleap: checksum mismatch for {entry.get('path')}")
    try:
        with open(os.path.join(STARLEAP, "units.json"), encoding="utf-8") as f:
            units = json.load(f)
    except Exception as e:
        errors.append(f"starleap/units.json: invalid JSON — {e}")
        return 0
    for u in units:
        if u.get("portrait") not in listed:
            errors.append(f"starleap: {u.get('id')} portrait {u.get('portrait')} is not in the manifest")
    return len(units)


starleap_units = check_starleap()

```

and change `      f"recruitment_games={len(recruitment)}")` to:

```python
      f"recruitment_games={len(recruitment)} starleap_units={starleap_units}")
```

- [ ] **Step 2: Run it to verify it fails**

Run: `python3 tools/validate_data.py`
Expected: exit 1 with `ERROR: starleap: bundled pack missing (Resources/Raw/starleap/manifest.json)`.

- [ ] **Step 3: Bundle the pack built in Task 6**

```bash
cp -R build/starleap Resources/Raw/starleap
```

- [ ] **Step 4: Run the validator to verify it passes**

Run: `python3 tools/validate_data.py`
Expected: `… starleap_units=144` and `OK — no errors`.

- [ ] **Step 5: Add the service** — `Services/StarLeapData.cs`

```csharp
using SuikodenCodex.Models;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.Services;

public sealed class StarLeapData
{
    public static readonly Uri PackUri = new("https://kirkpatrickjunsay.github.io/Suikoden-Codex/starleap/");
    public const string Credit = "Source: Gensopedia STAR LEAP (CC BY-NC-SA 4.0)";
    private const string LastCheckKey = "sl_last_check";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly CodexData _codex;
    private readonly PackUpdater _updater;
    private readonly UpdatePolicy _policy = new(() => DateTimeOffset.UtcNow, TimeSpan.FromHours(24));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPackFiles _files = new BundledPackFiles();
    private Dictionary<string, SlUnit> _byId = new();
    private List<ClassicCharacter>? _classics;

    public StarLeapData(CodexData codex)
    {
        _codex = codex;
        _updater = new PackUpdater(Http, PackUri, Path.Combine(FileSystem.AppDataDirectory, "starleap"));
    }

    public SlManifest? Manifest { get; private set; }
    public IReadOnlyList<SlUnit> Units { get; private set; } = Array.Empty<SlUnit>();
    public string Status { get; private set; } = "";
    public event EventHandler? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (Manifest is not null)
            return;
        await _gate.WaitAsync();
        try
        {
            if (Manifest is not null)
                return;
            await Task.Run(_updater.RecoverInterruptedSwap);
            var bundledFiles = new BundledPackFiles();
            var bundled = await PackLoader.LoadAsync(bundledFiles);
            var cachedFiles = new DirectoryPackFiles(_updater.CurrentDirectory);
            var cached = await TryLoadAsync(cachedFiles);
            if (cached is not null && cached.Manifest.Version > bundled.Manifest.Version)
            {
                _files = cachedFiles;
                Apply(cached);
            }
            else
            {
                _files = bundledFiles;
                Apply(bundled);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UpdateResult> CheckForUpdatesAsync(bool force)
    {
        await EnsureLoadedAsync();
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            SetStatus("Offline — showing saved data");
            return new UpdateResult(UpdateOutcome.Failed, "offline");
        }
        if (!force && !_policy.IsDue(LastCheck))
            return new UpdateResult(UpdateOutcome.UpToDate);

        SetStatus("Checking for updates…");
        var result = await Task.Run(() => _updater.CheckAndUpdateAsync(Manifest, _files));
        switch (result.Outcome)
        {
            case UpdateOutcome.Updated:
                _files = new DirectoryPackFiles(_updater.CurrentDirectory);
                Apply(result.Pack!);
                LastCheck = DateTimeOffset.UtcNow;
                SetStatus("Updated to the latest data");
                break;
            case UpdateOutcome.UpToDate:
                LastCheck = DateTimeOffset.UtcNow;
                SetStatus("Up to date");
                break;
            case UpdateOutcome.AppUpdateRequired:
                SetStatus("Update the app for the latest Star Leap data");
                break;
            default:
                SetStatus("Couldn't check for updates");
                break;
        }
        return result;
    }

    public SlUnit? GetUnit(string id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<SlUnit> OtherVersionsOf(SlUnit unit) =>
        Units.Where(u => u.Id != unit.Id &&
                         string.Equals(u.BasedOn ?? u.Name, unit.BasedOn ?? unit.Name, StringComparison.OrdinalIgnoreCase))
             .ToList();

    public async Task<string?> ClassicEntryIdAsync(SlUnit unit)
    {
        await _codex.EnsureLoadedAsync();
        _classics ??= _codex.Entries
            .Where(e => e.Category == EntryCategory.Character)
            .Select(e => new ClassicCharacter(e.Id, e.Name, e.Game))
            .ToList();
        return ClassicMatcher.Match(unit, _classics);
    }

    public ImageSource Portrait(SlUnit unit)
    {
        var files = _files;
        var path = unit.Portrait;
        return ImageSource.FromStream(async ct => await files.OpenReadAsync(path, ct) ?? Stream.Null);
    }

    private static async Task<SlPack?> TryLoadAsync(IPackFiles files)
    {
        try
        {
            return await PackLoader.LoadAsync(files);
        }
        catch (PackException)
        {
            return null;
        }
    }

    private void Apply(SlPack pack)
    {
        Manifest = pack.Manifest;
        Units = pack.Units;
        _byId = pack.Units.ToDictionary(u => u.Id);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetStatus(string status)
    {
        Status = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static DateTimeOffset? LastCheck
    {
        get
        {
            var ticks = Preferences.Get(LastCheckKey, 0L);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
        set => Preferences.Set(LastCheckKey, value?.UtcTicks ?? 0L);
    }

    private sealed class BundledPackFiles : IPackFiles
    {
        public async Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
        {
            try
            {
                return await FileSystem.OpenAppPackageFileAsync("starleap/" + path);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 6: Register it** — in `MauiProgram.cs` replace

```csharp
		builder.Services.AddSingleton<WalkthroughData>();
```

with

```csharp
		builder.Services.AddSingleton<WalkthroughData>();
		builder.Services.AddSingleton<StarLeapData>();
```

- [ ] **Step 7: Build**

Run: `dotnet build -c Debug -f net10.0-android`
Expected: `Build succeeded`, `0 Error(s)`, validator line shows `starleap_units=144`.

- [ ] **Step 8: Commit**

```bash
git add tools/validate_data.py Resources/Raw/starleap Services/StarLeapData.cs MauiProgram.cs
git commit -m "Bundle the Star Leap pack and add the Star Leap data service"
```

---

### Task 11: Unit detail page

**Files:**
- Create: `ViewModels/SlUnitRow.cs`, `ViewModels/StarLeapUnitDetailViewModel.cs`
- Create: `Pages/StarLeapUnitDetailPage.xaml`, `Pages/StarLeapUnitDetailPage.xaml.cs`
- Modify: `AppShell.xaml.cs`, `MauiProgram.cs`

**Interfaces:**
- Consumes: `StarLeapData` (Task 10); `EffectFormatter`, `SlUnit`, `SlSkill` (library); `EntryDetailPage` route (existing).
- Produces: `SlUnitRow(SlUnit unit, ImageSource portrait)` with `Unit, Portrait, Id, Name, DisplayName, Subtitle`; route `StarLeapUnitDetailPage?id=<unit id>`.

- [ ] **Step 1: Create `ViewModels/SlUnitRow.cs`**

```csharp
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.ViewModels;

public sealed class SlUnitRow
{
    public SlUnitRow(SlUnit unit, ImageSource portrait)
    {
        Unit = unit;
        Portrait = portrait;
    }

    public SlUnit Unit { get; }
    public ImageSource Portrait { get; }
    public string Id => Unit.Id;
    public string Name => Unit.Name;
    public string DisplayName => Unit.DisplayName;

    public string Subtitle => string.Join("  •  ",
        new[] { Unit.Rarity, string.Join("/", Unit.Elements), Unit.Role }.Where(s => !string.IsNullOrEmpty(s)));
}
```

- [ ] **Step 2: Create `ViewModels/StarLeapUnitDetailViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.ViewModels;

public sealed record InfoRow(string Label, string Value);

public sealed record StatRow(string Label, string Level1, string Max);

public sealed class SkillVM
{
    public SkillVM(SlSkill skill)
    {
        Slot = skill.Slot.ToUpperInvariant();
        Name = skill.Name;
        Meta = string.Join("  •  ", new[]
        {
            skill.Target,
            skill.Uses,
            skill.Rune is null ? null : $"Rune: {skill.Rune}",
            skill.Trigger,
        }.Where(s => !string.IsNullOrEmpty(s)));
        Description = skill.Description ?? "";
        Effects = skill.Effects.Select(EffectFormatter.Format).ToList();
    }

    public string Slot { get; }
    public string Name { get; }
    public string Meta { get; }
    public bool HasMeta => Meta.Length > 0;
    public string Description { get; }
    public bool HasDescription => Description.Length > 0;
    public List<string> Effects { get; }
}

[QueryProperty(nameof(UnitId), "id")]
public partial class StarLeapUnitDetailViewModel : ObservableObject
{
    private readonly StarLeapData _data;
    private string? _unitId;

    public StarLeapUnitDetailViewModel(StarLeapData data) => _data = data;

    public ObservableCollection<InfoRow> Info { get; } = new();
    public ObservableCollection<StatRow> Stats { get; } = new();
    public ObservableCollection<SkillVM> Kit { get; } = new();
    public ObservableCollection<SlUnitRow> OtherVersions { get; } = new();
    public string Credit => StarLeapData.Credit;

    [ObservableProperty]
    private SlUnit? _unit;

    [ObservableProperty]
    private ImageSource? _portrait;

    [ObservableProperty]
    private string _japaneseName = "";

    [ObservableProperty]
    private string _badges = "";

    [ObservableProperty]
    private bool _hasStats;

    [ObservableProperty]
    private string _trainingText = "";

    [ObservableProperty]
    private bool _hasTraining;

    [ObservableProperty]
    private string _weaponText = "";

    [ObservableProperty]
    private bool _hasWeapon;

    [ObservableProperty]
    private bool _hasKit;

    [ObservableProperty]
    private bool _hasOtherVersions;

    [ObservableProperty]
    private string? _classicEntryId;

    [ObservableProperty]
    private bool _hasClassic;

    public string? UnitId
    {
        get => _unitId;
        set
        {
            _unitId = value;
            Load();
        }
    }

    [RelayCommand]
    private Task OpenVersion(SlUnitRow? row) =>
        row is null ? Task.CompletedTask : Shell.Current.GoToAsync($"{nameof(StarLeapUnitDetailPage)}?id={row.Id}");

    [RelayCommand]
    private Task OpenClassic() =>
        ClassicEntryId is null ? Task.CompletedTask : Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={ClassicEntryId}");

    [RelayCommand]
    private async Task OpenWiki()
    {
        if (Unit?.WikiUrl is { Length: > 0 } url)
            await Launcher.OpenAsync(url);
    }

    private async void Load()
    {
        await _data.EnsureLoadedAsync();
        if (string.IsNullOrEmpty(_unitId) || _data.GetUnit(_unitId) is not { } unit)
            return;

        Unit = unit;
        Portrait = _data.Portrait(unit);
        JapaneseName = string.Join("  ", new[] { unit.NameJp, unit.TitleJp }.Where(s => !string.IsNullOrEmpty(s)));
        Badges = string.Join("  •  ", new[]
        {
            unit.Rarity,
            string.Join("/", unit.Elements),
            unit.Role,
            string.Join("/", unit.Weapons),
        }.Where(s => !string.IsNullOrEmpty(s)));

        Info.Clear();
        AddInfo("Obtained", unit.Obtained);
        AddInfo("Released", unit.Released);
        AddInfo("From", unit.Origin);
        AddInfo("Voice", unit.Voice);
        AddInfo("Illustration", unit.Illustration);

        Stats.Clear();
        if (unit.Stats is { } s)
        {
            AddRange("HP", s.Hp);
            AddRange("PATK", s.Patk);
            AddRange("MATK", s.Matk);
            AddRange("PDEF", s.Pdef);
            AddRange("MDEF", s.Mdef);
            AddSingle("AGI", s.Agi);
            AddSingle("HIT", s.Hit);
            AddSingle("DODGE", s.Dodge);
            TrainingText = string.Join("  ·  ", s.Training.Select(t => $"{t.Key.ToUpperInvariant()} +{t.Value}"));
        }
        else
        {
            TrainingText = "";
        }
        HasStats = Stats.Count > 0;
        HasTraining = TrainingText.Length > 0;

        WeaponText = unit.Weapon is { } w
            ? $"{w.Type}{(string.IsNullOrEmpty(w.Growth) ? "" : $" ({w.Growth})")}: {string.Join(" → ", w.Names)}"
            : "";
        HasWeapon = WeaponText.Length > 0;

        Kit.Clear();
        foreach (var skill in unit.Kit)
            Kit.Add(new SkillVM(skill));
        HasKit = Kit.Count > 0;

        OtherVersions.Clear();
        foreach (var other in _data.OtherVersionsOf(unit))
            OtherVersions.Add(new SlUnitRow(other, _data.Portrait(other)));
        HasOtherVersions = OtherVersions.Count > 0;

        ClassicEntryId = await _data.ClassicEntryIdAsync(unit);
        HasClassic = ClassicEntryId is not null;
    }

    private void AddInfo(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            Info.Add(new InfoRow(label, value));
    }

    private void AddRange(string label, List<int>? values)
    {
        if (values is { Count: 2 })
            Stats.Add(new StatRow(label, values[0].ToString(), values[1].ToString()));
    }

    private void AddSingle(string label, int? value)
    {
        if (value is { } v)
            Stats.Add(new StatRow(label, v.ToString(), v.ToString()));
    }
}
```

- [ ] **Step 3: Create `Pages/StarLeapUnitDetailPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage
    x:Class="SuikodenCodex.Pages.StarLeapUnitDetailPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:vm="clr-namespace:SuikodenCodex.ViewModels"
    x:Name="Page"
    x:DataType="vm:StarLeapUnitDetailViewModel"
    Title="{Binding Unit.Name}">

    <ScrollView>
        <VerticalStackLayout Padding="16" Spacing="14">

            <Grid ColumnDefinitions="Auto,*" ColumnSpacing="14">
                <Border WidthRequest="96" HeightRequest="96" StrokeShape="RoundRectangle 14"
                        Stroke="#44D9A636" BackgroundColor="#20264A" Padding="0" VerticalOptions="Start">
                    <Image Source="{Binding Portrait}" Aspect="AspectFill" />
                </Border>
                <VerticalStackLayout Grid.Column="1" Spacing="3" VerticalOptions="Center">
                    <Label Text="{Binding Unit.Name}" FontSize="22" FontAttributes="Bold" />
                    <Label Text="{Binding Unit.Title}" FontSize="15" Opacity="0.85" />
                    <Label Text="{Binding JapaneseName}" FontSize="13" Opacity="0.6" />
                    <Label Text="{Binding Badges}" FontSize="12" TextColor="{StaticResource Primary}" />
                </VerticalStackLayout>
            </Grid>

            <Border StrokeShape="RoundRectangle 8" Stroke="#30808080" Padding="14,12">
                <VerticalStackLayout Spacing="6" BindableLayout.ItemsSource="{Binding Info}">
                    <BindableLayout.ItemTemplate>
                        <DataTemplate x:DataType="vm:InfoRow">
                            <Grid ColumnDefinitions="104,*" ColumnSpacing="10">
                                <Label Text="{Binding Label}" FontSize="13" FontAttributes="Bold" Opacity="0.55" />
                                <Label Grid.Column="1" Text="{Binding Value}" FontSize="13" />
                            </Grid>
                        </DataTemplate>
                    </BindableLayout.ItemTemplate>
                </VerticalStackLayout>
            </Border>

            <Border IsVisible="{Binding HasStats}" StrokeShape="RoundRectangle 8" Stroke="#30808080" Padding="14,12">
                <VerticalStackLayout Spacing="6">
                    <Grid ColumnDefinitions="104,*,*">
                        <Label Text="Stats" FontSize="13" FontAttributes="Bold" />
                        <Label Grid.Column="1" Text="Lv 1" FontSize="12" Opacity="0.6" HorizontalTextAlignment="End" />
                        <Label Grid.Column="2" Text="Max" FontSize="12" Opacity="0.6" HorizontalTextAlignment="End" />
                    </Grid>
                    <VerticalStackLayout Spacing="4" BindableLayout.ItemsSource="{Binding Stats}">
                        <BindableLayout.ItemTemplate>
                            <DataTemplate x:DataType="vm:StatRow">
                                <Grid ColumnDefinitions="104,*,*">
                                    <Label Text="{Binding Label}" FontSize="13" FontAttributes="Bold" Opacity="0.55" />
                                    <Label Grid.Column="1" Text="{Binding Level1}" FontSize="13" HorizontalTextAlignment="End" />
                                    <Label Grid.Column="2" Text="{Binding Max}" FontSize="13" HorizontalTextAlignment="End" />
                                </Grid>
                            </DataTemplate>
                        </BindableLayout.ItemTemplate>
                    </VerticalStackLayout>
                    <Label IsVisible="{Binding HasTraining}" Text="{Binding TrainingText, StringFormat='Full training: {0}'}"
                           FontSize="12" Opacity="0.7" Margin="0,4,0,0" />
                    <Label IsVisible="{Binding HasWeapon}" Text="{Binding WeaponText, StringFormat='Weapon: {0}'}"
                           FontSize="12" Opacity="0.7" />
                </VerticalStackLayout>
            </Border>

            <Label IsVisible="{Binding HasKit}" Text="Skill kit" FontSize="13" FontAttributes="Bold" Opacity="0.6" />
            <VerticalStackLayout Spacing="10" BindableLayout.ItemsSource="{Binding Kit}">
                <BindableLayout.ItemTemplate>
                    <DataTemplate x:DataType="vm:SkillVM">
                        <Border StrokeShape="RoundRectangle 10" Stroke="#2A335E" BackgroundColor="#1C2348" Padding="14,12">
                            <VerticalStackLayout Spacing="4">
                                <Label Text="{Binding Slot}" FontSize="11" FontAttributes="Bold" TextColor="{StaticResource Primary}" />
                                <Label Text="{Binding Name}" FontSize="15" FontAttributes="Bold" TextColor="#E2E8FB" />
                                <Label Text="{Binding Meta}" IsVisible="{Binding HasMeta}" FontSize="12" TextColor="#9AA6D8" />
                                <Label Text="{Binding Description}" IsVisible="{Binding HasDescription}" FontSize="13" TextColor="#D3DAF2" />
                                <VerticalStackLayout Spacing="2" BindableLayout.ItemsSource="{Binding Effects}">
                                    <BindableLayout.ItemTemplate>
                                        <DataTemplate x:DataType="x:String">
                                            <Label Text="{Binding ., StringFormat='• {0}'}" FontSize="12.5" TextColor="#D3DAF2" />
                                        </DataTemplate>
                                    </BindableLayout.ItemTemplate>
                                </VerticalStackLayout>
                            </VerticalStackLayout>
                        </Border>
                    </DataTemplate>
                </BindableLayout.ItemTemplate>
            </VerticalStackLayout>

            <Label IsVisible="{Binding HasOtherVersions}" Text="Other versions" FontSize="13" FontAttributes="Bold" Opacity="0.6" />
            <VerticalStackLayout IsVisible="{Binding HasOtherVersions}" Spacing="6" BindableLayout.ItemsSource="{Binding OtherVersions}">
                <BindableLayout.ItemTemplate>
                    <DataTemplate x:DataType="vm:SlUnitRow">
                        <Grid ColumnDefinitions="Auto,*" ColumnSpacing="12" Padding="0,4">
                            <Border WidthRequest="44" HeightRequest="44" StrokeShape="RoundRectangle 8"
                                    Stroke="#22808080" BackgroundColor="#20264A" Padding="0">
                                <Image Source="{Binding Portrait}" Aspect="AspectFill" />
                            </Border>
                            <VerticalStackLayout Grid.Column="1" VerticalOptions="Center">
                                <Label Text="{Binding DisplayName}" FontSize="14" FontAttributes="Bold" />
                                <Label Text="{Binding Subtitle}" FontSize="12" Opacity="0.6" />
                            </VerticalStackLayout>
                            <Grid.GestureRecognizers>
                                <TapGestureRecognizer
                                    Command="{Binding Source={x:Reference Page}, Path=BindingContext.OpenVersionCommand}"
                                    CommandParameter="{Binding .}" />
                            </Grid.GestureRecognizers>
                        </Grid>
                    </DataTemplate>
                </BindableLayout.ItemTemplate>
            </VerticalStackLayout>

            <Button IsVisible="{Binding HasClassic}" Text="Classic codex entry ›" Command="{Binding OpenClassicCommand}"
                    FontSize="13" HeightRequest="40" BackgroundColor="#20264A" TextColor="#ECC56A"
                    BorderColor="#55D9A636" BorderWidth="1" />

            <VerticalStackLayout Spacing="4" Margin="0,8,0,16">
                <Label Text="{Binding Credit}" FontSize="11" Opacity="0.55" />
                <Button Text="View on Gensopedia" Command="{Binding OpenWikiCommand}" HorizontalOptions="Start"
                        FontSize="12" HeightRequest="34" Padding="12,0"
                        BackgroundColor="Transparent" TextColor="{StaticResource Primary}" />
            </VerticalStackLayout>

        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

- [ ] **Step 4: Create `Pages/StarLeapUnitDetailPage.xaml.cs`**

```csharp
using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class StarLeapUnitDetailPage : ContentPage
{
    public StarLeapUnitDetailPage(StarLeapUnitDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
```

- [ ] **Step 5: Register route and DI** — in `AppShell.xaml.cs` replace

```csharp
		Routing.RegisterRoute(nameof(WalkthroughPage), typeof(WalkthroughPage));
```

with

```csharp
		Routing.RegisterRoute(nameof(WalkthroughPage), typeof(WalkthroughPage));
		Routing.RegisterRoute(nameof(StarLeapUnitDetailPage), typeof(StarLeapUnitDetailPage));
```

In `MauiProgram.cs` replace `		builder.Services.AddTransient<WalkthroughViewModel>();` with

```csharp
		builder.Services.AddTransient<WalkthroughViewModel>();
		builder.Services.AddTransient<StarLeapUnitDetailViewModel>();
```

and replace `		builder.Services.AddTransient<WalkthroughPage>();` with

```csharp
		builder.Services.AddTransient<WalkthroughPage>();
		builder.Services.AddTransient<StarLeapUnitDetailPage>();
```

- [ ] **Step 6: Build** (the page is not reachable until Task 13)

Run: `dotnet build -c Debug -f net10.0-android`
Expected: `Build succeeded`, `0 Error(s)` — compiled XAML bindings (`x:DataType`) are type-checked here.

- [ ] **Step 7: Commit**

```bash
git add ViewModels/SlUnitRow.cs ViewModels/StarLeapUnitDetailViewModel.cs Pages/StarLeapUnitDetailPage.xaml Pages/StarLeapUnitDetailPage.xaml.cs AppShell.xaml.cs MauiProgram.cs
git commit -m "Add Star Leap unit detail page"
```

---

### Task 12: Character list page with search, filters and sort

**Files:**
- Create: `ViewModels/FilterChip.cs`, `ViewModels/StarLeapUnitsViewModel.cs`
- Create: `Pages/StarLeapUnitsPage.xaml`, `Pages/StarLeapUnitsPage.xaml.cs`
- Modify: `AppShell.xaml.cs`, `MauiProgram.cs`

**Interfaces:**
- Consumes: `StarLeapData` (Task 10), `SlUnitRow` (Task 11), `UnitQuery`, `OriginFilter`, `UnitSort` (Task 9), route `StarLeapUnitDetailPage` (Task 11).
- Produces: `FilterChip(string dimension, string value, string label)` with `IsSelected`; route `StarLeapUnitsPage`. Chips are built from the data (rarity in `108 Stars, Guest, SSR, SR, R` order, elements in `Fire, Water, Wind, Earth, Lightning, Holy, Dark` order, others alphabetical) plus era chips "Star Leap era" / "Classic-game versions"; one selection per dimension.

- [ ] **Step 1: Create `ViewModels/FilterChip.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace SuikodenCodex.ViewModels;

public partial class FilterChip : ObservableObject
{
    public FilterChip(string dimension, string value, string label)
    {
        Dimension = dimension;
        Value = value;
        Label = label;
    }

    public string Dimension { get; }
    public string Value { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;
}
```

- [ ] **Step 2: Create `ViewModels/StarLeapUnitsViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.ViewModels;

public partial class StarLeapUnitsViewModel : ObservableObject
{
    private static readonly string[] RarityOrder = { "108 Stars", "Guest", "SSR", "SR", "R" };
    private static readonly string[] ElementOrder = { "Fire", "Water", "Wind", "Earth", "Lightning", "Holy", "Dark" };
    private readonly StarLeapData _data;

    public StarLeapUnitsViewModel(StarLeapData data) => _data = data;

    public ObservableCollection<FilterChip> Chips { get; } = new();
    public ObservableCollection<SlUnitRow> Rows { get; } = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _resultSummary = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortLabel))]
    private UnitSort _sort = UnitSort.Name;

    public string SortLabel => Sort == UnitSort.Name ? "Sort: A–Z" : "Sort: Newest";

    public async Task InitializeAsync()
    {
        await _data.EnsureLoadedAsync();
        if (Chips.Count == 0)
            BuildChips();
        Apply();
    }

    partial void OnSearchTextChanged(string value) => Apply();

    partial void OnSortChanged(UnitSort value) => Apply();

    [RelayCommand]
    private void ToggleChip(FilterChip? chip)
    {
        if (chip is null)
            return;
        var select = !chip.IsSelected;
        foreach (var c in Chips.Where(c => c.Dimension == chip.Dimension))
            c.IsSelected = false;
        chip.IsSelected = select;
        Apply();
    }

    [RelayCommand]
    private void ToggleSort() => Sort = Sort == UnitSort.Name ? UnitSort.Newest : UnitSort.Name;

    [RelayCommand]
    private Task OpenUnit(SlUnitRow? row) =>
        row is null ? Task.CompletedTask : Shell.Current.GoToAsync($"{nameof(StarLeapUnitDetailPage)}?id={row.Id}");

    private void BuildChips()
    {
        var units = _data.Units;
        AddChips("rarity", Ordered(units.Select(u => u.Rarity), RarityOrder));
        AddChips("element", Ordered(units.SelectMany(u => u.Elements), ElementOrder));
        AddChips("role", Ordered(units.Select(u => u.Role), Array.Empty<string>()));
        AddChips("weapon", Ordered(units.SelectMany(u => u.Weapons), Array.Empty<string>()));
        Chips.Add(new FilterChip("origin", nameof(OriginFilter.StarLeap), "Star Leap era"));
        Chips.Add(new FilterChip("origin", nameof(OriginFilter.Classic), "Classic-game versions"));
    }

    private void AddChips(string dimension, IEnumerable<string> values)
    {
        foreach (var value in values)
            Chips.Add(new FilterChip(dimension, value, value));
    }

    private static IEnumerable<string> Ordered(IEnumerable<string> values, string[] preferred)
    {
        var present = values.Where(v => !string.IsNullOrEmpty(v)).ToHashSet();
        return preferred.Where(present.Contains)
            .Concat(present.Except(preferred).OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
    }

    private string? Selected(string dimension) =>
        Chips.FirstOrDefault(c => c.Dimension == dimension && c.IsSelected)?.Value;

    private void Apply()
    {
        var origin = Selected("origin") switch
        {
            nameof(OriginFilter.StarLeap) => OriginFilter.StarLeap,
            nameof(OriginFilter.Classic) => OriginFilter.Classic,
            _ => OriginFilter.Any,
        };
        var query = new UnitQuery
        {
            Text = SearchText,
            Rarity = Selected("rarity"),
            Element = Selected("element"),
            Role = Selected("role"),
            Weapon = Selected("weapon"),
            Origin = origin,
            Sort = Sort,
        };
        var result = query.Apply(_data.Units);
        Rows.Clear();
        foreach (var unit in result)
            Rows.Add(new SlUnitRow(unit, _data.Portrait(unit)));
        ResultSummary = $"{result.Count} of {_data.Units.Count} units";
    }
}
```

- [ ] **Step 3: Create `Pages/StarLeapUnitsPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage
    x:Class="SuikodenCodex.Pages.StarLeapUnitsPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:vm="clr-namespace:SuikodenCodex.ViewModels"
    x:DataType="vm:StarLeapUnitsViewModel"
    Title="Star Leap characters">

    <Grid RowDefinitions="Auto,Auto,Auto,*">

        <SearchBar Grid.Row="0" Placeholder="Search English or Japanese names…"
                   Text="{Binding SearchText, Mode=TwoWay}" />

        <CollectionView Grid.Row="1" ItemsSource="{Binding Chips}" ItemsLayout="HorizontalList"
                        HeightRequest="48" Margin="8,0" SelectionMode="None">
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="vm:FilterChip">
                    <Border StrokeShape="RoundRectangle 18" Margin="4,6" Padding="12,6"
                            Stroke="{Binding IsSelected, Converter={StaticResource BoolToColor}, ConverterParameter=#D9A636|#55808080}"
                            BackgroundColor="{Binding IsSelected, Converter={StaticResource BoolToColor}, ConverterParameter=#D9A636|#00000000}">
                        <Label Text="{Binding Label}" FontSize="13" VerticalOptions="Center"
                               TextColor="{Binding IsSelected, Converter={StaticResource BoolToColor}, ConverterParameter=#1C2348|#9AA0B5}" />
                        <Border.GestureRecognizers>
                            <TapGestureRecognizer
                                Command="{Binding Source={RelativeSource AncestorType={x:Type vm:StarLeapUnitsViewModel}}, Path=ToggleChipCommand}"
                                CommandParameter="{Binding .}" />
                        </Border.GestureRecognizers>
                    </Border>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>

        <Grid Grid.Row="2" ColumnDefinitions="*,Auto" Padding="16,2,12,4">
            <Label Text="{Binding ResultSummary}" FontSize="12" Opacity="0.6" VerticalOptions="Center" />
            <Button Grid.Column="1" Text="{Binding SortLabel}" Command="{Binding ToggleSortCommand}"
                    FontSize="12" HeightRequest="32" Padding="12,0"
                    BackgroundColor="Transparent" TextColor="{StaticResource Primary}" />
        </Grid>

        <CollectionView Grid.Row="3" ItemsSource="{Binding Rows}" SelectionMode="None">
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="vm:SlUnitRow">
                    <Grid Padding="16,10" ColumnDefinitions="Auto,*" ColumnSpacing="14">
                        <Border WidthRequest="50" HeightRequest="50" Padding="0" VerticalOptions="Center"
                                StrokeShape="RoundRectangle 10" Stroke="#22808080" BackgroundColor="#20264A">
                            <Image Source="{Binding Portrait}" Aspect="AspectFill" />
                        </Border>
                        <VerticalStackLayout Grid.Column="1" Spacing="1" VerticalOptions="Center">
                            <Label Text="{Binding DisplayName}" FontSize="16" FontAttributes="Bold"
                                   MaxLines="1" LineBreakMode="TailTruncation" />
                            <Label Text="{Binding Subtitle}" FontSize="12" Opacity="0.6"
                                   MaxLines="1" LineBreakMode="TailTruncation" />
                        </VerticalStackLayout>
                        <Grid.GestureRecognizers>
                            <TapGestureRecognizer
                                Command="{Binding Source={RelativeSource AncestorType={x:Type vm:StarLeapUnitsViewModel}}, Path=OpenUnitCommand}"
                                CommandParameter="{Binding .}" />
                        </Grid.GestureRecognizers>
                    </Grid>
                </DataTemplate>
            </CollectionView.ItemTemplate>
            <CollectionView.EmptyView>
                <Label Text="No units match your search." HorizontalOptions="Center" Margin="0,40" Opacity="0.6" />
            </CollectionView.EmptyView>
        </CollectionView>
    </Grid>
</ContentPage>
```

- [ ] **Step 4: Create `Pages/StarLeapUnitsPage.xaml.cs`**

```csharp
using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class StarLeapUnitsPage : ContentPage
{
    private readonly StarLeapUnitsViewModel _vm;

    public StarLeapUnitsPage(StarLeapUnitsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
```

- [ ] **Step 5: Register route and DI** — in `AppShell.xaml.cs` replace

```csharp
		Routing.RegisterRoute(nameof(StarLeapUnitDetailPage), typeof(StarLeapUnitDetailPage));
```

with

```csharp
		Routing.RegisterRoute(nameof(StarLeapUnitDetailPage), typeof(StarLeapUnitDetailPage));
		Routing.RegisterRoute(nameof(StarLeapUnitsPage), typeof(StarLeapUnitsPage));
```

In `MauiProgram.cs` replace `		builder.Services.AddTransient<StarLeapUnitDetailViewModel>();` with

```csharp
		builder.Services.AddTransient<StarLeapUnitDetailViewModel>();
		builder.Services.AddTransient<StarLeapUnitsViewModel>();
```

and replace `		builder.Services.AddTransient<StarLeapUnitDetailPage>();` with

```csharp
		builder.Services.AddTransient<StarLeapUnitDetailPage>();
		builder.Services.AddTransient<StarLeapUnitsPage>();
```

- [ ] **Step 6: Build**

Run: `dotnet build -c Debug -f net10.0-android`
Expected: `Build succeeded`, `0 Error(s)`.

- [ ] **Step 7: Commit**

```bash
git add ViewModels/FilterChip.cs ViewModels/StarLeapUnitsViewModel.cs Pages/StarLeapUnitsPage.xaml Pages/StarLeapUnitsPage.xaml.cs AppShell.xaml.cs MauiProgram.cs
git commit -m "Add Star Leap character list with search, filters and sort"
```

---

### Task 13: Star Leap tab and hub; Card Stories moves into More

**Files:**
- Create: `ViewModels/StarLeapHubViewModel.cs`, `Pages/StarLeapHubPage.xaml`, `Pages/StarLeapHubPage.xaml.cs`, `Resources/Images/tab_starleap.svg`
- Modify: `AppShell.xaml`, `AppShell.xaml.cs`, `MauiProgram.cs`, `ViewModels/MoreViewModel.cs`, `Pages/MorePage.xaml`, `Platforms/Android/ShortcutRouter.cs`

**Interfaces:**
- Consumes: `StarLeapData` (Task 10), `SlUnitRow` (Task 11), routes `StarLeapUnitsPage`, `StarLeapUnitDetailPage`, `EntryDetailPage?id=game-suikoden_star_leap` (existing codex entry).
- Produces: tab route `starleap`; push route `CardsPage`; `MoreViewModel.OpenCardsCommand`.

- [ ] **Step 1: Create `ViewModels/StarLeapHubViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

public partial class StarLeapHubViewModel : ObservableObject
{
    private readonly StarLeapData _data;

    public StarLeapHubViewModel(StarLeapData data)
    {
        _data = data;
        _data.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
    }

    public ObservableCollection<SlUnitRow> Newest { get; } = new();
    public string Credit => StarLeapData.Credit;

    [ObservableProperty]
    private string _dataAsOf = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string _unitCountText = "";

    [ObservableProperty]
    private bool _isChecking;

    public async Task InitializeAsync()
    {
        await _data.EnsureLoadedAsync();
        Refresh();
        await CheckAsync(force: false);
    }

    [RelayCommand]
    private Task CheckForUpdates() => CheckAsync(force: true);

    [RelayCommand]
    private Task OpenCharacters() => Shell.Current.GoToAsync(nameof(StarLeapUnitsPage));

    [RelayCommand]
    private Task OpenUnit(SlUnitRow? row) =>
        row is null ? Task.CompletedTask : Shell.Current.GoToAsync($"{nameof(StarLeapUnitDetailPage)}?id={row.Id}");

    [RelayCommand]
    private Task OpenAbout() => Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id=game-suikoden_star_leap");

    private async Task CheckAsync(bool force)
    {
        if (IsChecking)
            return;
        IsChecking = true;
        try
        {
            await _data.CheckForUpdatesAsync(force);
        }
        finally
        {
            IsChecking = false;
            Refresh();
        }
    }

    private void Refresh()
    {
        DataAsOf = _data.Manifest is { } m ? $"Data as of {m.GeneratedAt.ToLocalTime():d MMM yyyy}" : "";
        Status = _data.Status;
        UnitCountText = $"{_data.Units.Count} units";
        Newest.Clear();
        foreach (var unit in _data.Units
                     .OrderByDescending(u => u.Released ?? "", StringComparer.Ordinal)
                     .ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                     .Take(12))
            Newest.Add(new SlUnitRow(unit, _data.Portrait(unit)));
    }
}
```

- [ ] **Step 2: Create `Pages/StarLeapHubPage.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage
    x:Class="SuikodenCodex.Pages.StarLeapHubPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:vm="clr-namespace:SuikodenCodex.ViewModels"
    x:Name="Page"
    x:DataType="vm:StarLeapHubViewModel"
    Title="Star Leap">

    <ScrollView>
        <VerticalStackLayout Padding="16" Spacing="14">

            <Border StrokeShape="RoundRectangle 12" Stroke="#44D9A636" BackgroundColor="#1AD9A636" Padding="16,14">
                <VerticalStackLayout Spacing="6">
                    <Label Text="Suikoden STAR LEAP guide" FontSize="18" FontAttributes="Bold" />
                    <Label Text="{Binding DataAsOf}" FontSize="13" Opacity="0.8" />
                    <Label Text="{Binding Credit}" FontSize="11" Opacity="0.6" />
                    <Grid ColumnDefinitions="Auto,*" ColumnSpacing="12" Margin="0,6,0,0">
                        <Button Text="Check for updates" Command="{Binding CheckForUpdatesCommand}"
                                IsEnabled="{Binding IsChecking, Converter={StaticResource InvertedBool}}"
                                FontSize="12" HeightRequest="36" Padding="14,0"
                                BackgroundColor="#20264A" TextColor="#ECC56A" BorderColor="#55D9A636" BorderWidth="1" />
                        <HorizontalStackLayout Grid.Column="1" Spacing="8" VerticalOptions="Center">
                            <ActivityIndicator IsRunning="{Binding IsChecking}" IsVisible="{Binding IsChecking}"
                                               HeightRequest="18" WidthRequest="18" Color="{StaticResource Primary}" />
                            <Label Text="{Binding Status}" FontSize="12" Opacity="0.7" VerticalOptions="Center" />
                        </HorizontalStackLayout>
                    </Grid>
                </VerticalStackLayout>
            </Border>

            <Border StrokeShape="RoundRectangle 10" Stroke="#30808080" Padding="16">
                <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="14">
                    <Label Text="🧑" FontSize="26" VerticalOptions="Center" />
                    <VerticalStackLayout Grid.Column="1" VerticalOptions="Center">
                        <Label Text="Characters" FontSize="16" FontAttributes="Bold" />
                        <Label Text="{Binding UnitCountText}" FontSize="12" Opacity="0.6" />
                    </VerticalStackLayout>
                    <Label Grid.Column="2" Text="›" FontSize="22" Opacity="0.4" VerticalOptions="Center" />
                    <Grid.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding OpenCharactersCommand}" />
                    </Grid.GestureRecognizers>
                </Grid>
            </Border>

            <Label Text="Newest units" FontSize="13" FontAttributes="Bold" Opacity="0.6" Margin="0,6,0,0" />
            <CollectionView ItemsSource="{Binding Newest}" HeightRequest="118" SelectionMode="None">
                <CollectionView.ItemsLayout>
                    <LinearItemsLayout Orientation="Horizontal" ItemSpacing="10" />
                </CollectionView.ItemsLayout>
                <CollectionView.ItemTemplate>
                    <DataTemplate x:DataType="vm:SlUnitRow">
                        <VerticalStackLayout WidthRequest="80" Spacing="4">
                            <Border WidthRequest="72" HeightRequest="72" StrokeShape="RoundRectangle 10"
                                    Stroke="#33808080" BackgroundColor="#20264A" Padding="0" HorizontalOptions="Center">
                                <Image Source="{Binding Portrait}" Aspect="AspectFill" />
                            </Border>
                            <Label Text="{Binding Name}" FontSize="11" HorizontalTextAlignment="Center"
                                   MaxLines="2" LineBreakMode="TailTruncation" />
                            <VerticalStackLayout.GestureRecognizers>
                                <TapGestureRecognizer
                                    Command="{Binding Source={x:Reference Page}, Path=BindingContext.OpenUnitCommand}"
                                    CommandParameter="{Binding .}" />
                            </VerticalStackLayout.GestureRecognizers>
                        </VerticalStackLayout>
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>

            <Border StrokeShape="RoundRectangle 10" Stroke="#30808080" Padding="16">
                <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="14">
                    <Label Text="📖" FontSize="24" VerticalOptions="Center" />
                    <VerticalStackLayout Grid.Column="1" VerticalOptions="Center">
                        <Label Text="About Star Leap" FontSize="16" FontAttributes="Bold" />
                        <Label Text="Story, setting and how Stars differ from gacha characters" FontSize="12" Opacity="0.6" />
                    </VerticalStackLayout>
                    <Label Grid.Column="2" Text="›" FontSize="22" Opacity="0.4" VerticalOptions="Center" />
                    <Grid.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding OpenAboutCommand}" />
                    </Grid.GestureRecognizers>
                </Grid>
            </Border>

        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

- [ ] **Step 3: Create `Pages/StarLeapHubPage.xaml.cs`**

```csharp
using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class StarLeapHubPage : ContentPage
{
    private readonly StarLeapHubViewModel _vm;

    public StarLeapHubPage(StarLeapHubViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
```

- [ ] **Step 4: Create the tab icon** — `Resources/Images/tab_starleap.svg`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg" fill="#000000">
  <path d="M11 2 C11.9 8.2 12.8 9.1 19 10 C12.8 10.9 11.9 11.8 11 18 C10.1 11.8 9.2 10.9 3 10 C9.2 9.1 10.1 8.2 11 2 Z"/>
  <path d="M19 15 C19.4 17.3 19.7 17.6 22 18 C19.7 18.4 19.4 18.7 19 21 C18.6 18.7 18.3 18.4 16 18 C18.3 17.6 18.6 17.3 19 15 Z"/>
</svg>
```

- [ ] **Step 5: Swap the Cards tab for Star Leap** — in `AppShell.xaml` replace

```xml
        <ShellContent
            Title="Cards"
            Icon="tab_cards.png"
            ContentTemplate="{DataTemplate pages:CardsPage}"
            Route="cards" />
```

with

```xml
        <ShellContent
            Title="Star Leap"
            Icon="tab_starleap.png"
            ContentTemplate="{DataTemplate pages:StarLeapHubPage}"
            Route="starleap" />
```

- [ ] **Step 6: Make Cards a pushed page** — in `AppShell.xaml.cs` replace

```csharp
		Routing.RegisterRoute(nameof(StarLeapUnitsPage), typeof(StarLeapUnitsPage));
```

with

```csharp
		Routing.RegisterRoute(nameof(StarLeapUnitsPage), typeof(StarLeapUnitsPage));
		Routing.RegisterRoute(nameof(CardsPage), typeof(CardsPage));
```

In `MauiProgram.cs`: replace `		builder.Services.AddSingleton<CardsPage>();` with `		builder.Services.AddTransient<CardsPage>();`; replace `		builder.Services.AddTransient<StarLeapUnitsViewModel>();` with

```csharp
		builder.Services.AddTransient<StarLeapUnitsViewModel>();
		builder.Services.AddSingleton<StarLeapHubViewModel>();
```

and replace `		builder.Services.AddTransient<StarLeapUnitsPage>();` with

```csharp
		builder.Services.AddTransient<StarLeapUnitsPage>();
		builder.Services.AddSingleton<StarLeapHubPage>();
```

- [ ] **Step 7: Add the Card Stories row to More** — in `ViewModels/MoreViewModel.cs` replace

```csharp
    [RelayCommand]
    private async Task OpenWalkthrough() =>
        await Shell.Current.GoToAsync(nameof(WalkthroughPage));
```

with

```csharp
    [RelayCommand]
    private async Task OpenWalkthrough() =>
        await Shell.Current.GoToAsync(nameof(WalkthroughPage));

    [RelayCommand]
    private async Task OpenCards() =>
        await Shell.Current.GoToAsync(nameof(CardsPage));
```

In `Pages/MorePage.xaml`, directly after the `</Border>` that closes the **Compare** row (the one whose `TapGestureRecognizer` uses `OpenCompareCommand`), insert:

```xml

            <Border StrokeShape="RoundRectangle 10" Stroke="#30808080" Padding="16">
                <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="14">
                    <Label Text="🃏" FontSize="24" VerticalOptions="Center" />
                    <VerticalStackLayout Grid.Column="1" VerticalOptions="Center">
                        <Label Text="Card Stories gallery" FontSize="16" FontAttributes="Bold" />
                        <Label Text="Cards from the Genso Suikoden Card Stories TCG" FontSize="12" Opacity="0.6" />
                    </VerticalStackLayout>
                    <Label Grid.Column="2" Text="›" FontSize="22" Opacity="0.4" VerticalOptions="Center" />
                    <Grid.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding OpenCardsCommand}" />
                    </Grid.GestureRecognizers>
                </Grid>
            </Border>
```

- [ ] **Step 8: Keep the Cards home-screen shortcut working** — in `Platforms/Android/ShortcutRouter.cs` replace

```csharp
                    await shell.GoToAsync("//cards");
```

with

```csharp
                    await shell.GoToAsync("CardsPage");
```

- [ ] **Step 9: Build and deploy to the Legion**

```bash
dotnet build -c Debug -f net10.0-android
adb devices -l
dotnet build -t:Install -c Debug -f net10.0-android -p:AdbTarget="-s HA2J6GAS"
adb -s HA2J6GAS shell am force-stop com.codesandchips.suikodencodex
adb -s HA2J6GAS shell monkey -p com.codesandchips.suikodencodex -c android.intent.category.LAUNCHER 1
```

Expected: build succeeds; the app launches. If the Legion is not listed by `adb devices`, ask the owner to connect it.

- [ ] **Step 10: Verify on device** — read the screen as text rather than guessing tap coordinates. Save this helper as `build/ui.sh` (git-ignored) and `chmod +x build/ui.sh`:

```bash
#!/bin/bash
adb -s HA2J6GAS shell uiautomator dump /sdcard/ui.xml >/dev/null 2>&1
adb -s HA2J6GAS pull /sdcard/ui.xml build/ui.xml >/dev/null 2>&1
python3 - "$@" <<'PY'
import re, sys
x = open("build/ui.xml", encoding="utf-8").read()
pat = sys.argv[1] if len(sys.argv) > 1 else None
for n in re.findall(r"<node [^>]*>", x):
    t = (re.search(r' text="([^"]*)"', n) or [0, ""])[1]
    c = (re.search(r'content-desc="([^"]*)"', n) or [0, ""])[1]
    b = re.search(r'bounds="([^"]*)"', n).group(1)
    s = (t or c).strip()
    if s and (not pat or re.search(pat, s, re.I)):
        print(f"{b:28s} {s[:110]}")
PY
```

Use `build/ui.sh "Star Leap"` to find the tab's bounds, tap its centre with `adb -s HA2J6GAS shell input tap X Y`, and confirm each item (dismiss onboarding with "Skip" if it appears):

1. Bottom bar reads Home · Codex · Recruitment · **Star Leap** · More.
2. Hub shows "Data as of …", the source credit, **Characters — 144 units**, a Newest units strip with portraits, and About Star Leap (opens the codex overview entry).
3. Characters → list shows portraits and "Name — Title" rows; searching `フリック` returns the two Flik units; the **SSR** chip narrows the list; "Sort: A–Z" toggles to "Sort: Newest".
4. Open **Flik — Blue Flash Strike**: Japanese name `フリック  青の瞬撃`, stats table (Lv 1 / Max), skill kit (Normal, Tech A, Tech B, Special, Support, Trait, Leader), **Other versions** lists Flik — Wandering Swordfighter, **Classic codex entry ›** opens the Flik codex entry.
5. More → Explore shows **Card Stories gallery**, which opens the cards page.

Capture `adb -s HA2J6GAS exec-out screencap -p > build/hub.png` for the hub and attach it to your report.

- [ ] **Step 11: Commit**

```bash
git add ViewModels/StarLeapHubViewModel.cs Pages/StarLeapHubPage.xaml Pages/StarLeapHubPage.xaml.cs Resources/Images/tab_starleap.svg AppShell.xaml AppShell.xaml.cs MauiProgram.cs ViewModels/MoreViewModel.cs Pages/MorePage.xaml Platforms/Android/ShortcutRouter.cs
git commit -m "Add Star Leap tab and hub; move Card Stories gallery into More"
```

---

### Task 14: Credits, privacy policy, README and store-listing copy

**Files:**
- Modify: `Pages/MorePage.xaml`, `docs/privacy/index.html`, `PRIVACY.md`, `README.md`
- Create: `store-assets/listing.md`

**Interfaces:** text only.

- [ ] **Step 1: Credit the data source in the app** — in `Pages/MorePage.xaml`, replace

```xml
                    <VerticalStackLayout Spacing="3">
                        <Label Text="🔒 Your privacy" FontSize="14" FontAttributes="Bold" />
```

with

```xml
                    <VerticalStackLayout Spacing="3">
                        <Label Text="⭐ Star Leap guide" FontSize="14" FontAttributes="Bold" />
                        <Label FontSize="12" Opacity="0.75"
                               Text="Star Leap guide data and portraits are adapted from Gensopedia STAR LEAP (starleap.gensopedia.org) and shared under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 licence." />
                    </VerticalStackLayout>

                    <VerticalStackLayout Spacing="3">
                        <Label Text="🔒 Your privacy" FontSize="14" FontAttributes="Bold" />
```

and in the privacy label that follows, replace the text ending `…are stored only on your device and are never sent anywhere." />` with:

```xml
Text="Suikoden Codex collects no personal data. There is no account, no tracking, and no analytics. Your favorites, recently viewed entries, and recruitment progress are stored only on your device and are never sent anywhere. The Star Leap guide can download updated guide data from the developer's GitHub Pages site; nothing about you is sent." />
```

- [ ] **Step 2: Update the privacy policy** — in **both** `docs/privacy/index.html` and `PRIVACY.md`:
  - Effective date `25 June 2026` → the date you make this change (e.g. `26 September 2026`).
  - Replace section **3. Permissions** body with (HTML version shown; mirror in Markdown):

```html
  <p>The app uses the standard <strong>Internet / network-state</strong> permission for one purpose: the optional <strong>Star Leap guide</strong> can download updated guide data (character information and portraits) from the developer&rsquo;s GitHub Pages site (<code>kirkpatrickjunsay.github.io</code>). This download sends no personal data and no identifiers; like any web host, GitHub receives your device&rsquo;s IP address when the file is requested, as described in GitHub&rsquo;s own privacy statement. Everything else in the app works fully offline.</p>
```

  - In section **5. Third-party content**, append: `Star Leap guide data is adapted from Gensopedia STAR LEAP and shared under CC BY-NC-SA 4.0.`

- [ ] **Step 3: Update `README.md`**
  - Under `## ✨ Features`, add after the Compare bullet:
    `- **Star Leap guide** — a tab for players of *Suikoden STAR LEAP*: all playable units with Japanese and English names, level 1 → max stats, full skill kits and portraits. Works offline and can download updated guide data.`
  - Change the Card Stories bullet to start `- **Card Stories gallery** (in More) — …`.
  - Under `## 📚 Data & sources`, add: `Star Leap guide data comes from **[Gensopedia STAR LEAP](https://starleap.gensopedia.org)** (CC BY-NC-SA 4.0). The adapted data in \`docs/starleap/\` and \`Resources/Raw/starleap/\` is shared under the same licence. To refresh it, run \`./publish_starleap.sh\` on \`main\`.`
  - Under `## 🔒 Privacy`, replace "works entirely offline" with "works offline; the optional Star Leap guide can download public guide data from GitHub Pages (nothing about you is sent)".

- [ ] **Step 4: Store-listing copy for the owner to paste** — create `store-assets/listing.md`:

```markdown
# Play Store listing — Star Leap update

## Short description (≤ 80)
Offline Suikoden I–V codex + Star Leap guide: 108 Stars, units, walkthroughs

## Full description — replace the "100% OFFLINE — 100% PRIVATE" section with
★ STAR LEAP GUIDE
Look up every playable Suikoden STAR LEAP unit: Japanese and English names, level 1 to max stats, full skill kits and how to obtain them. The guide can download updated data as the game grows.

★ WORKS OFFLINE — 100% PRIVATE
Everything works without a connection; the Star Leap guide can optionally download updated guide data. Suikoden Codex collects no personal data: no account, no tracking, no analytics, no ads.

## Credits line (append to the fan-project disclaimer)
Star Leap guide data is adapted from Gensopedia STAR LEAP under CC BY-NC-SA 4.0.

## Release notes (≤ 500)
New: Star Leap guide! A new tab with every playable Suikoden STAR LEAP unit — Japanese and English names, stats, full skill kits and portraits. It works offline and can download updated data as the game grows.
The Card Stories gallery has moved to More.
```

- [ ] **Step 5: Build, deploy, check the More page**

Run: `dotnet build -t:Install -c Debug -f net10.0-android -p:AdbTarget="-s HA2J6GAS"`, relaunch, open More, and confirm `build/ui.sh "Star Leap guide|GitHub Pages"` finds both new texts.

- [ ] **Step 6: Commit**

```bash
git add Pages/MorePage.xaml docs/privacy/index.html PRIVACY.md README.md store-assets/listing.md
git commit -m "Credit Gensopedia STAR LEAP and update privacy and store copy for guide downloads"
```

---

### Task 15: Merge, first publish, end-to-end verification (owner in the loop)

Publishing pushes to the public repository and GitHub Pages — **ask the owner before Step 2 and before Step 5**.

**Files:** `docs/starleap/`, `Resources/Raw/starleap/` (written by the publish command).

- [ ] **Step 1: Full test pass on the branch**

```bash
python3 -m unittest discover -s tools/tests
dotnet test tests/SuikodenCodex.StarLeap.Tests
dotnet build -c Debug -f net10.0-android
```

Expected: 37 Python tests OK; 27 .NET tests passed; build succeeded.

- [ ] **Step 2: Merge into `main`** (owner approves)

```bash
git checkout main
git merge --ff-only feature/star-leap-guide
```

- [ ] **Step 3: First publish** (owner runs it, or approves you running it)

```bash
./publish_starleap.sh
```

Expected: change summary, prompt `Publish Star Leap guide data version 1? [y/N]`; on `y` it commits `Update Star Leap guide data (v1)` and pushes `main`.

- [ ] **Step 4: Confirm Pages serves the pack** (allow ~2 minutes)

```bash
curl -s https://kirkpatrickjunsay.github.io/Suikoden-Codex/starleap/manifest.json | python3 -c "import json,sys; m=json.load(sys.stdin); print(m['version'], len(m['files']))"
```

Expected: `1 145` (version 1; 144 portraits + `units.json`).

- [ ] **Step 5: Offline first launch** — rebuild and install (the bundle now equals published v1), then:

```bash
dotnet build -t:Install -c Debug -f net10.0-android -p:AdbTarget="-s HA2J6GAS"
adb -s HA2J6GAS shell cmd connectivity airplane-mode enable
adb -s HA2J6GAS shell am force-stop com.codesandchips.suikodencodex
adb -s HA2J6GAS shell monkey -p com.codesandchips.suikodencodex -c android.intent.category.LAUNCHER 1
```

Open the Star Leap tab. Expected: 144 units shown, status "Offline — showing saved data". Then `adb -s HA2J6GAS shell cmd connectivity airplane-mode disable` and tap **Check for updates** → "Up to date".

- [ ] **Step 6: Update pickup** (owner approves a second publish): run `./publish_starleap.sh` again (the cached run takes ≈12 s; "(no changes)" is fine) and answer `y` → version 2. After Pages updates (Step 4 command prints `2 145`), tap **Check for updates** on the Legion. Expected: status "Updated to the latest data" and "Data as of" shows today's date; relaunch the app and confirm it still shows v2 data (loaded from the cache). Corrupt and partial downloads are covered by `PackUpdaterTests`.

- [ ] **Step 7: Report** — share the hub screenshot, the two publish summaries, and any warnings with the owner. Remind them the next `./release.sh` bundles the latest pack for the Play upload, and that the store copy is in `store-assets/listing.md`.
