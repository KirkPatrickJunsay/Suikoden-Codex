#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

branch=$(git rev-parse --abbrev-ref HEAD)
if [ "$branch" != "main" ]; then
  echo "❌ Publish from main (current branch: $branch). GitHub Pages serves docs/ on main."
  exit 1
fi

if ! git diff --cached --quiet; then
  echo "❌ Unstaged your changes first: git has staged changes that would be published."
  exit 1
fi

python3 tools/starleap_sync.py "$@"

version=$(python3 -c "import json; print(json.load(open('build/starleap/manifest.json'))['version'])")
read -r -p "Publish Star Leap guide data version ${version}? [y/N] " answer
if [[ ! "$answer" =~ ^[Yy]$ ]]; then
  echo "Not published."
  exit 0
fi

rm -rf docs/starleap Resources/Raw/starleap
mkdir -p docs Resources/Raw
cp -R build/starleap docs/starleap
cp -R build/starleap Resources/Raw/starleap
git add docs/starleap Resources/Raw/starleap
git commit -m "Update Star Leap guide data (v${version})" -- docs/starleap Resources/Raw/starleap
git push origin main

echo ""
echo "✅ Published version ${version}."
echo "   Live within a few minutes at https://kirkpatrickjunsay.github.io/Suikoden-Codex/starleap/manifest.json"
echo "   The next ./release.sh build bundles this version."
