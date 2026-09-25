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

    def test_slugify_curly_apostrophe(self):
        self.assertEqual(slugify("Gremio’s Return"), "gremios-return")


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
