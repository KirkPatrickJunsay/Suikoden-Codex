# Release Checklist — Suikoden Codex (Google Play)

## Build a signed bundle
1. Ensure `signing.props` and `suikodencodex.keystore` exist locally (both git-ignored).
2. Run:
   ```bash
   ./release.sh            # bumps the version code, builds the signed .aab
   ./release.sh 1.1        # also sets the display version (e.g. 1.1)
   ```
3. Upload the printed file: `bin/Release/net10.0-android/com.codesandchips.suikodencodex-Signed.aab`

> Every Play upload needs a **higher version code** than the last — `release.sh` handles that automatically.

## Keystore — do not lose this
- File: `suikodencodex.keystore`, alias `suikodencodex`.
- Back up the keystore file **and** its password somewhere safe (password manager + offline copy).
- Losing it means you can never update this app again.

## Play Console — first submission
- [ ] Create app → name **Suikoden Codex**, default language, app (not game—optional), free.
- [ ] **App access** — all features available without restrictions.
- [ ] **Ads** — declare **No ads**.
- [ ] **Content rating** questionnaire — complete (no objectionable content).
- [ ] **Target audience** — set age groups; not directed at children only.
- [ ] **Data safety** — declare **no data collected / no data shared** (the app is offline; see PRIVACY.md).
- [ ] **Privacy policy URL** — `https://github.com/KirkPatrickJunsay/Suikoden-Codex/blob/main/PRIVACY.md`
- [ ] **Store listing** — short & full description, app icon (512×512), feature graphic (1024×500), 2–8 phone screenshots.
- [ ] Upload the **.aab** to Internal testing first → verify install → then promote to Production.
- [ ] Accept Play **App Signing** (Google holds the app-signing key; your keystore is the *upload* key).

## Important notes
- This is a **fan project**; Suikoden content is © Konami. Be ready for possible IP review; keep the listing clearly non-commercial and crediting sources.
- Card Duel is currently hidden behind `FeatureFlags.CardDuel`; enable it in a later release.
