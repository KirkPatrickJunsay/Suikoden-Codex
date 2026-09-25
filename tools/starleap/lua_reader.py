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
