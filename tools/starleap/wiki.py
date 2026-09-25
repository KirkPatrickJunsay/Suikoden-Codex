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
