# Suikoden Codex

A free, offline fan encyclopedia and companion app for Konami's **Suikoden I–V**, built with .NET MAUI for Android.

Browse characters, monsters, items, runes, regions and factions across the whole series, track your **108 Stars of Destiny** recruitment in every game, explore an interactive world map, and follow the saga's timeline — all on-device, with no account and no ads.

> ⚠️ **Non-commercial fan project.** Suikoden and all related names, characters and imagery are © Konami. This app is made by a fan, for fans, to celebrate and help people enjoy the series. See [Attribution](#-attribution--license).

---

## ✨ Features

- **Codex** — 1,800+ entries spanning Suikoden I–V: characters, monsters, items, runes, regions, factions, wars and more, with original and translated names.
- **108 Stars of Destiny tracker** — mark recruits per game, with real Star-of-Destiny titles, recruitment order, and missable-character hints.
- **Interactive World Map** — an original, lore-accurate map of the Suikoden world; tap a nation or region to jump to its entry. Pinch / buttons to zoom.
- **Series Timeline** — major wars and events across all five games, ordered by Solar Year.
- **Compare** — view two characters or monsters side by side.
- **Card Stories gallery** — browse the cards from the *Genso Suikoden Card Stories* TCG.
- **Quality-of-life** — favorites, recently viewed, random "discover", spoiler-safe mode, dark theme, and local backup / restore of your progress.

## 📱 Screenshots

> _Screenshots go here — add images to a `screenshots/` folder and reference them, e.g._
> `![Home](screenshots/home.png)`

## 🛠️ Built with

- **.NET MAUI** (.NET 10) — single-project Android app (`net10.0-android`)
- **MVVM** via the CommunityToolkit.Mvvm
- Bundled JSON datasets + images (fully offline)

## 🚀 Getting started

**Prerequisites:** .NET 10 SDK with the MAUI workload, the Android SDK, and Python 3 (used by a small pre-build data-validation step).

```bash
# restore + build
dotnet build -f net10.0-android -c Debug

# build and run on a connected device / emulator
dotnet build -f net10.0-android -t:Run -c Debug -p:AdbTarget="-s <device-serial>"
```

## 📚 Data & sources

Entry text and card data are sourced primarily from **[Gensopedia](https://gensopedia.org)**, the community Suikoden encyclopedia, with thanks. The app credits Gensopedia and Konami in-app on entry pages.

## 🔒 Privacy

Suikoden Codex collects **no personal data** and works entirely offline. Everything you do (progress, favorites, custom data) is stored only on your device. Full policy: **[PRIVACY.md](PRIVACY.md)**.

## ⚖️ Attribution & license

- *Suikoden* (幻想水滸伝), its characters, artwork, names and all related content are **© Konami Digital Entertainment**. This project claims no ownership of them.
- Encyclopedia content is courtesy of **Gensopedia** and its contributors.
- This is a **non-commercial, fan-made** application created for educational and community purposes. It is **not affiliated with or endorsed by Konami**.
- The original application source code in this repository may be used for reference and learning. Bundled game data and imagery remain the property of their respective owners and are **not** covered by any open-source license.

If you are a rights holder and have any concerns, please reach out (see below).

## ✉️ Contact

Made by **Kirk** (Codes & Chips). Questions or concerns: open an issue on this repository.
