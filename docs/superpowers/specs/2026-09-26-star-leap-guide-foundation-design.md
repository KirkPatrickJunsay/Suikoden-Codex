# Star Leap Guide — Sub-project 1: Foundation + Character Database

**Date:** 2026-09-26
**Status:** Design approved in chat; awaiting spec review
**Owner:** Kirk

## Goal

Turn Suikoden Codex into a practical guide for players of *Suikoden STAR LEAP*
(Konami/MYTHRIL, free-to-play mobile RPG, Japan release 2026-08-07, global release
unscheduled). This first sub-project builds the foundation every later guide
feature depends on — a Star Leap tab, a refreshable data pack, and a character
database with stats, skill kits and portraits.

## Roadmap (each sub-project gets its own spec → plan → build)

| # | Sub-project | Depends on |
|---|-------------|------------|
| **1** | **Foundation + Character database** (this spec) | — |
| 2 | Banners & events — current/upcoming gacha banners, featured units, countdowns in local time | 1 |
| 3 | 108 Stars tracker — Star Leap added to the Recruitment tab (47 of 108 documented today) | 1 |
| 4 | Story walkthrough — Star Leap added to the Walkthrough picker, chapter by chapter | 1 |

Later candidates (data already exists on the source wiki, not scheduled): items,
enemies, recipes, bounties, fishing.

## Decisions (made with the owner)

| Topic | Decision |
|-------|----------|
| Scope | All four guide areas, built in the order above |
| Freshness | Bundled data pack + optional download of newer packs from GitHub Pages |
| Placement | New **Star Leap** bottom tab replacing **Cards**; Card Stories gallery moves into More |
| Artwork | Small unit portraits included in the data pack (credited); owner accepts the IP/takedown risk |
| Refresh | **Manual only** — owner runs one publish command |
| Architecture | Separate Star Leap data service + pack; reuse existing UI patterns; link returning units to classic codex entries |

## Non-goals (this sub-project)

- Banners, 108 Stars tracker, story walkthrough (sub-projects 2–4)
- Favorites for units, team building, tier lists (opinion, not data)
- Voice lines and character quotes (verbatim game text)
- Live queries to the wiki from the app; automatic/scheduled refresh
- Any change to the classic codex data or I–V features beyond moving Cards into More

## Source data

**Star Leap sub-wiki:** `https://starleap.gensopedia.org` (API: `/api.php`),
licence **CC BY-NC-SA 4.0**. Verified 2026-09-26:

| Source | Content |
|--------|---------|
| Cargo table `SP_characters` (144 rows) | name, title, image, rarity (`108 Stars`/`Guest`/`SSR`/`SR`/`R`), role, element/element2, weapon/weapon2, released, season, illustration, voice, basedon, game |
| Unit page infobox `{{SP character infobox}}` | `name_jp`, `title_jp`, `obtained` (e.g. Gacha) |
| `Module:SP stat range/data` (Lua table, datamined) | HP/PATK/MATK/PDEF/MDEF as {Lv1, max}; AGI/HIT/DODGE single values; training-tree bonus; weapon type, growth, three upgrade names. Two allies currently have no weapon entry. |
| `Module:SP battle kit/data` (Lua table, generated) | Skill numbers and structure: rows, values per level, targets, hits, slots, uses, rune; trait/leader lines |
| `Module:SP battle kit/text` (Lua table, hand-maintained) | Display names, flavour descriptions, trait names/triggers; overlays `/data` |
| File `… (SP character face).png` per unit | Portrait, fetched as a 128 px thumbnail via the wiki's own thumbnailer (`prop=imageinfo&iiurlwidth=128`; today's face icons are natively 128 px) |

Excluded on purpose: `quote` field, voice-line pages.

## Architecture

```
┌──────────────── owner's Mac ────────────────┐        ┌──── GitHub Pages (main:/docs) ────┐
│ ./publish_starleap.sh                        │  push  │ /starleap/manifest.json           │
│   └ tools/starleap_sync.py ──► build/starleap├───────►│ /starleap/units.json              │
│        fetch → transform → validate → diff   │        │ /starleap/portraits/*.png         │
│   copies pack to docs/starleap/              │        └───────────────┬───────────────────┘
│   and Resources/Raw/starleap/ (bundled)      │                        │ HTTPS GET (manifest, changed files)
└──────────────────────────────────────────────┘                        ▼
┌──────────────────────────── App ─────────────────────────────────────────────────────────┐
│ lib/SuikodenCodex.StarLeap (net10.0)        │ SuikodenCodex (MAUI, net10.0-android)       │
│   Models, PackLoader, PackUpdater,          │   Services/StarLeapData  (orchestrates)     │
│   UpdatePolicy — no MAUI dependencies       │   Pages: StarLeapHub / Units / UnitDetail   │
└─────────────────────────────────────────────┴─────────────────────────────────────────────┘
```

### Repository layout changes

```
lib/SuikodenCodex.StarLeap/                 new class library (net10.0)
tests/SuikodenCodex.StarLeap.Tests/         new xUnit test project (net10.0)
tools/starleap_sync.py                      new sync/build/validate script (stdlib only)
tools/tests/test_starleap_sync.py           new unittest suite for the script
publish_starleap.sh                         new owner command
docs/starleap/                              published pack (GitHub Pages)
docs/.nojekyll                              already present; first task re-triggers the failing Pages build and diagnoses it from fresh logs
Resources/Raw/starleap/                     bundled baseline pack
build/                                      staging output, git-ignored
```

The repository root **is** the MAUI project, whose default globs would compile
`lib/**` and `tests/**` (including their `obj/` generated files). `SuikodenCodex.csproj`
gets `<Compile Remove="lib/**;tests/**" />` (plus matching `None`/`Content`
removes) and a `ProjectReference` to the library.

## Data pack format (schema 1)

Hosted at `https://kirkpatrickjunsay.github.io/Suikoden-Codex/starleap/`.

**`manifest.json`**
```json
{
  "schema": 1,
  "version": 12,
  "generatedAt": "2026-09-26T04:30:00Z",
  "source": "https://starleap.gensopedia.org",
  "license": "CC BY-NC-SA 4.0",
  "attribution": "Adapted from Gensopedia STAR LEAP",
  "files": [
    { "path": "units.json", "sha256": "…", "bytes": 412345 },
    { "path": "portraits/aegir-night-lightnings-shadow.png", "sha256": "…", "bytes": 14210 }
  ]
}
```
`version` is a monotonically increasing integer (the script reads the previously
published manifest and adds 1). `schema` changes only when the JSON shape changes
incompatibly.

**`units.json`** — array of units (example values are illustrative):
```json
{
  "id": "aegir-night-lightnings-shadow",
  "name": "Aegir", "nameJp": "エギル",
  "title": "Night Lightning's Shadow", "titleJp": "闇夜の雷影",
  "rarity": "SSR", "role": "Attack",
  "elements": ["Lightning"], "weapons": ["Sword"],
  "obtained": "Gacha",
  "origin": "Suikoden STAR LEAP", "basedOn": "Aegir",
  "released": "2026-07-08",
  "voice": "Muro Genki", "illustration": "REIDO",
  "portrait": "portraits/aegir-night-lightnings-shadow.png",
  "stats": {
    "hp": [1200, 9800], "patk": [150, 1320], "matk": [80, 700],
    "pdef": [90, 810], "mdef": [85, 760],
    "agi": 112, "hit": 95, "dodge": 10,
    "training": { "hp": 600, "patk": 80 }
  },
  "weapon": { "type": "Sword", "names": ["…", "…", "…"] },
  "kit": [
    {
      "slot": "Tech A", "name": "…", "target": "Single", "uses": "Usable x4",
      "rune": "Measures", "description": "…", "trigger": null,
      "effects": [ { "label": "Physical Power", "values": ["28", "31", "35"],
                     "tags": ["Wisdom 1"], "hits": null, "note": null } ]
    }
  ],
  "wikiUrl": "https://starleap.gensopedia.org/w/Aegir:_Night_Lightning%27s_Shadow"
}
```
`stats`, `weapon`, and `kit` may be `null`/empty when the source lacks them.
`origin` comes from `SP_characters.game` and names the **era of that version** of the
character, not whether the character is new: Flik's story versions are
`Suikoden STAR LEAP`, while Viki has one Star Leap-era and one `Suikoden` version. An
empty value becomes `null`, which the era filter treats as neither era.
Kit `slot` is one of: `Normal`, `Tech A`, `Tech B`, `Tech C`, `Special`, `Support`, `Trait`, `Leader`.
Effect `values` are strings (real data includes percentages such as `"80%"`).
All text fields are plain text — no wiki markup, HTML, or icon codes.

## Sync script — `tools/starleap_sync.py`

Python 3 standard library only. Steps:

1. **Fetch** via the wiki API with a descriptive User-Agent, batched queries
   (≤ 50 titles per request) and a short pause between batches.
2. **Read Lua data modules** with a built-in table-literal reader (strings incl.
   long brackets, numbers, booleans, nil, nested/keyed tables, comments). It parses
   top-level `local NAME = { … }` and `return { … }` statements (a returned field may
   name one of those locals) and skips other statements (e.g. the loop that builds
   `index`). If a table the sync needs is missing or unparseable, the run fails with
   a clear message.
3. **Transform** into the schema above: merge `/text` over `/data`, flatten effect
   rows into `{label, values}`, strip markup, derive stable `id` slugs from the
   unit page title.
4. **Validate** (any failure stops the run; nothing is published):
   - every unit has `id`, `name`, `rarity`, `role`, ≥1 element, `portrait`
     (`title` may be empty — some units have no epithet yet)
   - kit `slot` values are from the known set
   - rarity/role/element/weapon values outside the known sets are **warnings**, not
     errors (a live game adds new ones — e.g. the Holy and Dark elements); the app
     builds its filter chips from the data, so new values just work
   - unit count is not lower than the previously published pack's count
     (override with `--allow-shrink` when a removal is intentional)
   - every portrait file exists and is a PNG
   - no `[[`, `]]`, `{{`, `}}`, `<span`, `File:` left in any text field
   - unique ids
5. **Diff** against the published `docs/starleap/` pack and print a summary:
   units added/removed, stat/kit changes per unit, portrait changes.
6. Write the pack + manifest (checksums, next version) to `build/starleap/`.

Portraits are cached in `build/cache/portraits/<wiki sha1>.png`, so only new or changed
portraits are downloaded. Measured on the real wiki: first run ≈ 5 min (144 portraits
from a slow volunteer server), cached run ≈ 12 s. Result: 144 units, 4.8 MB. A skill a
unit references but the wiki hasn't filled in yet (one today) is kept as name-only and
reported as a warning.

## Publish command — `./publish_starleap.sh`

1. Run the sync script (fails loudly on validation errors).
2. Show the change summary; ask `Publish version N? [y/N]`.
3. On yes: copy `build/starleap/` → `docs/starleap/` and `Resources/Raw/starleap/`,
   commit both with the owner's git identity (`Update Star Leap guide data (vN)`,
   no co-author lines), and push to `main`. GitHub Pages then serves the new pack;
   the next Play release bundles it.

## Library — `lib/SuikodenCodex.StarLeap` (net10.0, no MAUI references)

| Type | Responsibility |
|------|----------------|
| `SlUnit`, `SlStats`, `SlWeapon`, `SlSkill`, `SlEffect`, `SlManifest` | Pack models (System.Text.Json) |
| `IPackFiles` | Read-only file access to a pack: `OpenReadAsync(path)` returns `null` when a file is missing; implemented by the app for the bundled pack and by `DirectoryPackFiles` for cached packs |
| `PackLoader` | Loads a pack from `IPackFiles`: parses manifest, rejects unsupported `schema`, loads units. Throws `PackException` on any problem. |
| `PackUpdater` | Given an `HttpClient` + base URL, the active manifest, and a cache root: fetch remote manifest → if `schema` unsupported return `AppUpdateRequired`; if `version` ≤ current return `UpToDate`; else download files whose sha256 differs into `staging/`, copy unchanged files from current, verify every checksum, load the staged pack with `PackLoader`, then swap `staging/` → `current/` (keeping `previous/` until the swap succeeds; roll back on failure). Returns `Updated`, `UpToDate`, `AppUpdateRequired`, or `Failed(reason)`. Never throws to callers. |
| `UpdatePolicy` | Decides whether an automatic check is due (last successful check older than 24 h); clock injected for tests |

`SupportedSchema = 1` lives in the library.

## App integration (MAUI)

**`Services/StarLeapData`** (singleton):
- On first use, loads the bundled `Resources/Raw/starleap/` pack (read through
  `FileSystem.OpenAppPackageFileAsync`) and the cached `AppDataDirectory/starleap/current/`
  pack, and uses whichever has the **higher version** — so an app update that ships
  newer bundled data wins over an older download.
- Exposes `Units`, `Manifest`, `DataAsOf`, `LastCheckStatus`, `GetUnit(id)`,
  `VersionsOf(basedOn)`, `ClassicEntryFor(unit)`, `OpenPortrait(unit)`.
- `CheckForUpdatesAsync(force)` — skips when offline (`Connectivity`), or when not
  forced and `UpdatePolicy` says not due; stores the last successful check time in
  `Preferences`; reloads units on `Updated`.
- Classic codex link: for every unit, match `basedOn` (else `name`) to a `CodexData`
  Character entry by name, preferring the unit's own game (Suikoden I for Star Leap-era
  versions) when a name is ambiguous; aliases for known spelling differences (Viki → Vicky).
  Checked against real data: 70 matches, all correct, none for new Star Leap characters.

**Shell (`AppShell.xaml`)**: tab `cards` → `starleap` (title "Star Leap"). `CardsPage`
becomes a pushed route opened from a new **Card Stories gallery** row in More → Explore.

**Pages** (follow existing page/VM conventions, DI registration in `MauiProgram.cs`):

- **StarLeapHubPage** — header with "Data as of <date> · Source: Gensopedia STAR LEAP
  (CC BY-NC-SA 4.0)", **Check for updates** button and status line; **Characters**
  tile with count; **Newest units** horizontal strip (latest `released`); **About
  Star Leap** row opening codex entry `game-suikoden_star_leap`. Automatic check
  runs when the tab appears.
- **StarLeapUnitsPage** — Codex-style list: portrait, "Name — Title", subtitle
  (rarity · element · role). Search over name, nameJp, title, titleJp. Filter chips:
  rarity, element, role, weapon, era ("Star Leap era" / "Classic-game versions").
  Sort: name / newest.
- **StarLeapUnitDetailPage** — portrait; EN + JP name/title; badges (rarity,
  element(s), role, weapon(s)); obtained, released, voice, illustration; stats table
  (Lv 1 → max, AGI/HIT/DODGE, training bonus); weapon upgrade names; kit grouped by
  slot with target, uses, description, effects ("Physical Power 28 → 31 → 35");
  **Other versions** (same `basedOn`); **Classic codex entry** link; credit footer
  with **View on Gensopedia** link. Sections with no data are hidden.

**Credits (More page)**: add "Star Leap guide data and portraits adapted from
Gensopedia STAR LEAP (starleap.gensopedia.org), shared under CC BY-NC-SA 4.0."

## Error handling

| Situation | Behaviour |
|-----------|-----------|
| Offline | No check; bundled/cached data shown; status "Offline — showing data as of …" |
| Manifest/file download fails, checksum mismatch, bad JSON | Current data kept; status "Couldn't check for updates"; no popup |
| Remote `schema` newer than supported | Current data kept; status "Update the app for the latest Star Leap data" |
| Cached pack corrupt on startup | Fall back to the bundled pack; the next successful update replaces the cache |
| Unit missing stats/weapon/kit | That section hidden |
| Bundled pack invalid | Build fails (`validate_data.py` extended to check `Resources/Raw/starleap/`: manifest checksums, units parse, portraits exist) |

## Privacy, store listing, licence

- **Privacy policy** (`docs/privacy/index.html`, `PRIVACY.md`): the Star Leap guide
  may download public guide data from the developer's GitHub Pages site; no
  identifiers or personal data are sent; GitHub, as the host, receives the device's
  IP address under its own privacy statement. Update the effective date.
- **Play Data Safety**: remains "no data collected / shared"; owner re-confirms
  wording at submission.
- **Store listing / README**: replace "100% offline" with "Works offline; the Star
  Leap guide can optionally download updates"; add a Star Leap guide bullet.
  New Star Leap screenshots after the screens exist.
- **Licence**: pack manifest carries licence + attribution; the adapted data stays
  under CC BY-NC-SA 4.0.

## Testing

- **Python (`tools/tests/`, unittest)**: Lua reader on real module snippets
  (strings, long brackets, nested keyed tables, comments, rejection of functions);
  markup stripping; effect-row flattening; validation rules (each rule has a
  failing fixture); diff summary.
- **.NET (`tests/SuikodenCodex.StarLeap.Tests`, xUnit, written test-first)**:
  `PackLoader` (valid pack, unsupported schema, missing file, bad JSON);
  `PackUpdater` with a fake HTTP handler and temp directories (up to date, update
  applied, only changed files downloaded, checksum mismatch → rollback, remote
  schema too new, network failure, crash mid-swap leaves a loadable pack);
  `UpdatePolicy` (due / not due, injected clock).
- **Build gate**: extended `validate_data.py` runs on every build.
- **On device (Legion Y700, `HA2J6GAS`)**: first launch in airplane mode shows the
  bundled pack with an "Offline" status; publish a second version and confirm the app
  picks it up (corrupt/partial downloads are covered by the `PackUpdater` tests rather
  than by corrupting the live site); Cards reachable from More;
  unit search by Japanese name; classic codex link opens the right entry.

## Risks

| Risk | Mitigation |
|------|------------|
| Portraits and datamined stats are Konami IP (current commercial game) | Owner-accepted; credited; small thumbnails only; easy to remove from the pack if asked |
| Wiki structure/modules change | Script fails validation instead of publishing; bundled/cached data unaffected |
| English names are fan translations (JP-only release) | Japanese names shown and searchable alongside English |
| GitHub Pages build currently failing | Fixed as the first task; publishing verified end-to-end |
| Pack growth | Portraits at 128 px; only changed files are re-downloaded |
