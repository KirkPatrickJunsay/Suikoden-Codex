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

    def test_skips_if_elseif_else_chains(self):
        locals_, returned = read_module("""
            local a = { x = 1 }
            if PLATFORM == 'ios' then
                local unused = 1
            elseif PLATFORM == 'android' then
                local unused2 = 2
            else
                local unused3 = 3
            end
            local b = { y = 2 }
            return { a = a, b = b }
        """)
        self.assertEqual(returned, {"a": {"x": 1}, "b": {"y": 2}})
        self.assertIn("a", locals_)
        self.assertIn("b", locals_)
        self.assertNotIn("unused", locals_)
        self.assertNotIn("unused2", locals_)
        self.assertNotIn("unused3", locals_)


if __name__ == "__main__":
    unittest.main()
