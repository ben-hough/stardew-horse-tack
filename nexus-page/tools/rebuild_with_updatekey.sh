#!/usr/bin/env bash
# Build a HorseTack release zip with the Nexus UpdateKey, on the box. The version comes from the repo manifest (1.4.1 now).
#   usage: rebuild_with_updatekey.sh <NexusModId> [--commit]
# - makes sure "UpdateKeys": [ "Nexus:<id>" ] is in the repo manifest (the Version is left as it is)
# - Release build (must be 0 warnings) + AssetScanCheck
# - stages HorseTack/ (dll, pdb, manifest, i18n, assets, README.txt from docs/README.txt; no deps.json/config.json)
# - writes /workspace/codex-horsetack/share/HorseTack-<version>.zip (+ .sha256)
# - runs zip_check.py --expect-updatekey Nexus:<id> (expects the manifest version) and prints the SHA-256 for the PC install script
# - --commit: commit + push only manifest.json if the tree is otherwise clean (release commits with code changes are made by hand)
set -euo pipefail
ID="${1:-}"; COMMIT="${2:-}"
[[ "$ID" =~ ^[0-9]+$ ]] || { echo "usage: $0 <NexusModId> [--commit]"; exit 2; }
W="${HT_WORK:-/workspace/codex-horsetack}"; R=$W/stardew-horse-tack; HERE="$(cd "$(dirname "$0")" && pwd)"
export PATH=$PATH:/home/box/.dotnet DOTNET_ROOT=/home/box/.dotnet

cd "$R"
python3 - "$ID" <<'PY'
import json, re, sys
p = "manifest.json"; s = open(p, encoding="utf-8").read(); key = f"Nexus:{sys.argv[1]}"
s2 = re.sub(r'"UpdateKeys"\s*:\s*\[[^\]]*\]', f'"UpdateKeys": [ "{key}" ]', s)
m = json.loads(s2)
assert re.fullmatch(r"\d+\.\d+\.\d+", m["Version"]), m["Version"]
assert m["UpdateKeys"] == [key], m["UpdateKeys"]
if s2 != s:
    open(p, "w", encoding="utf-8", newline="\n").write(s2)
print("manifest:", m["Version"], m["UpdateKeys"])
PY
V=$(python3 -c "import json; print(json.load(open('manifest.json', encoding='utf-8-sig'))['Version'])")

log=$(dotnet build -c Release -p:GamePath=$W/refs --no-incremental 2>&1) || { echo "$log"; exit 1; }
echo "$log" | grep -E "Warn|Error|error" | tail -3
echo "$log" | grep -qE "^\s*0 Warning\(s\)" || { echo "build has warnings"; exit 1; }
echo "$log" | grep -qE "^\s*0 Error\(s\)" || { echo "build has errors"; exit 1; }
(cd tests/AssetScanCheck && dotnet run -c Release -p:GamePath=$W/refs 2>&1 | tail -1) | tee /dev/stderr | grep -q PASS

rm -rf "$W/pkg/HorseTack"; mkdir -p "$W/pkg/HorseTack"
cp -r bin/Release/HorseTack.dll bin/Release/HorseTack.pdb bin/Release/manifest.json bin/Release/i18n bin/Release/assets "$W/pkg/HorseTack/"
cp docs/README.txt "$W/pkg/HorseTack/README.txt"
rm -f "$W/pkg/HorseTack/config.json" "$W/pkg/HorseTack/HorseTack.deps.json"

mkdir -p "$W/share"; Z=$W/share/HorseTack-$V.zip
python3 - "$W/pkg" "$Z" <<'PY'
import os, sys, zipfile
src, out = sys.argv[1], sys.argv[2]
tmp = out + ".tmp"
with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as z:
    for dp, ds, fs in os.walk(os.path.join(src, "HorseTack")):
        ds.sort()
        rel = os.path.relpath(dp, src).replace(os.sep, "/")
        z.write(dp, rel + "/")
        for f in sorted(fs):
            z.write(os.path.join(dp, f), rel + "/" + f)
os.replace(tmp, out)
PY
HT_REPO="$R" python3 "$HERE/zip_check.py" "$Z" --expect-updatekey "Nexus:$ID"
SHA=$(sha256sum "$Z" | cut -d' ' -f1); echo "$SHA  HorseTack-$V.zip" > "$Z.sha256"

if [ "$COMMIT" = "--commit" ]; then
  if [ -z "$(git status --porcelain | grep -v ' manifest.json$')" ]; then
    git add manifest.json && git commit -q -m "Set Nexus UpdateKey (Nexus:$ID), version $V" && git push -q && git log --oneline -1
  else
    echo "not committing: working tree has other changes"; git status --short
  fi
fi
cat <<MSG

Box zip ready: $Z
SHA-256: $SHA
Next (PC):
  1. CopyFromBox $Z -> C:\\Users\\Glim\\codex-stage\\HorseTack-$V.zip
  2. powershell -ExecutionPolicy Bypass -File E:\\Codex-Mods\\stardew\\HorseTack\\nexus-page\\tools\\install-horsetack.ps1 -NexusId $ID -Sha256 $SHA -Version $V
MSG
