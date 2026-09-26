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

    def test_nested_templates_are_removed(self):
        raw = "Strikes {{tooltip|hard|{{Icon|sword}}}} twice"
        self.assertEqual(clean_text(raw), "Strikes twice")


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

    def test_single_stats_are_ints(self):
        stat_module = {"units": {1: {"name": "Mixed", "AGI": 72.0, "HIT": "78", "DODGE": 72}}}
        stats, _ = build_stats("Mixed", {}, stat_module)
        self.assertIs(type(stats["agi"]), int)
        self.assertIs(type(stats["hit"]), int)
        self.assertIs(type(stats["dodge"]), int)
        self.assertEqual(stats["agi"], 72)
        self.assertEqual(stats["hit"], 78)
        self.assertEqual(stats["dodge"], 72)


class InfoboxTests(unittest.TestCase):
    def test_parses_params_on_one_line_and_nested_links(self):
        text = "intro {{SP character infobox\n|name= Anji |obtained= |voice= Miyuu\n|from=[[X|Y]] {{JP}}\n}} rest"
        self.assertEqual(parse_infobox(text), {"name": "Anji", "obtained": "", "voice": "Miyuu", "from": "[[X|Y]] {{JP}}"})

    def test_missing_infobox(self):
        self.assertEqual(parse_infobox("no box here"), {})


if __name__ == "__main__":
    unittest.main()
