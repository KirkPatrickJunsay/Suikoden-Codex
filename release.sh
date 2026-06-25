#!/usr/bin/env bash
# Build a signed Play Store .aab. Auto-increments the Android version code.
#
#   ./release.sh            -> bump version code, keep display version, build
#   ./release.sh 1.1        -> bump version code AND set display version to 1.1
#
# Signing comes from the Release config in the .csproj + the git-ignored signing.props.
set -euo pipefail
cd "$(dirname "$0")"

CSPROJ="SuikodenCodex.csproj"

if [ ! -f signing.props ]; then
  echo "❌ signing.props not found — release builds need the keystore passwords. Aborting."
  exit 1
fi

# bump version code (ApplicationVersion)
cur=$(grep -oE '<ApplicationVersion>[0-9]+</ApplicationVersion>' "$CSPROJ" | grep -oE '[0-9]+')
next=$((cur + 1))
sed -i '' "s#<ApplicationVersion>${cur}</ApplicationVersion>#<ApplicationVersion>${next}</ApplicationVersion>#" "$CSPROJ"

# optional display version (e.g. 1.1)
if [ "${1:-}" != "" ]; then
  sed -i '' "s#<ApplicationDisplayVersion>[^<]*</ApplicationDisplayVersion>#<ApplicationDisplayVersion>${1}</ApplicationDisplayVersion>#" "$CSPROJ"
fi
disp=$(grep -oE '<ApplicationDisplayVersion>[^<]*</ApplicationDisplayVersion>' "$CSPROJ" | sed -E 's/<[^>]+>//g')

echo "🏗  Building release  v${disp}  (versionCode ${next}) …"
dotnet publish -f net10.0-android -c Release

aab=$(find bin/Release -name "*-Signed.aab" | head -1)
echo ""
echo "✅ Signed AAB ready to upload:"
echo "   ${aab}"
echo "   version ${disp} (code ${next})"
