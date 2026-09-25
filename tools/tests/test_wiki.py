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
