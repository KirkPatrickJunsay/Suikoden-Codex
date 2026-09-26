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


class NotPngClient(FakeClient):
    def download(self, url):
        self.downloads += 1
        return b"not a png"


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

    def test_non_png_portrait_aborts_and_leaves_no_cache_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            client = NotPngClient()
            with self.assertRaises(SystemExit):
                self.run_sync(tmp, client)
            portraits_dir = os.path.join(tmp, "cache", "portraits")
            leftover = os.listdir(portraits_dir) if os.path.isdir(portraits_dir) else []
            self.assertEqual([f for f in leftover if f.endswith(".png")], [])


if __name__ == "__main__":
    unittest.main()
