#!/usr/bin/env python3
"""Checks a HorseTack release zip: layout, manifest version/UpdateKeys, no config.json/deps.json,
every PNG byte-identical to the repo's own assets/, and no file matching any third-party file
(Elle's Cuter Horses reference copy + /workspace/codex-thirdparty) by SHA-256 or by decoded pixels.

usage: zip_check.py <zip> [--expect-updatekey Nexus:<id>]
"""
import sys, os, io, json, re, hashlib, zipfile
from PIL import Image

REPO = os.environ.get("HT_REPO", "/workspace/codex-horsetack/stardew-horse-tack")
THIRD = ["/workspace/codex-horsetack/elle", "/workspace/codex-thirdparty"]

def sha(b): return hashlib.sha256(b).hexdigest()
def pix(b):
    try:
        im = Image.open(io.BytesIO(b)).convert("RGBA")
        return sha(im.tobytes() + repr(im.size).encode())
    except Exception:
        return None

def main():
    zpath = sys.argv[1]
    expect = sys.argv[sys.argv.index("--expect-updatekey") + 1] if "--expect-updatekey" in sys.argv else None
    ok = True
    def check(cond, msg):
        nonlocal ok
        print(("PASS " if cond else "FAIL ") + msg)
        ok &= bool(cond)

    third_sha, third_pix, nthird = set(), set(), 0
    for root in THIRD:
        for dp, _, fs in os.walk(root):
            for f in fs:
                b = open(os.path.join(dp, f), "rb").read()
                third_sha.add(sha(b)); nthird += 1
                if f.lower().endswith(".png"):
                    p = pix(b)
                    if p: third_pix.add(p)
    repo_png = {}
    for dp, _, fs in os.walk(os.path.join(REPO, "assets")):
        for f in fs:
            if f.endswith(".png"):
                rel = os.path.relpath(os.path.join(dp, f), REPO).replace(os.sep, "/")
                repo_png[rel] = sha(open(os.path.join(dp, f), "rb").read())

    z = zipfile.ZipFile(zpath)
    names = [n for n in z.namelist() if not n.endswith("/")]
    print(f"zip: {zpath}\nsha256: {sha(open(zpath,'rb').read())}\nentries: {len(names)}")
    check(all(n.startswith("HorseTack/") and "\\" not in n for n in names), "single top-level HorseTack/ folder, forward-slash names")
    check(not any(n.lower().endswith(("config.json", ".deps.json", ".xnb")) for n in names), "no config.json / deps.json / xnb")
    m = json.loads(re.sub(r"^\s*//.*$", "", z.read("HorseTack/manifest.json").decode("utf-8-sig"), flags=re.M))
    check(m.get("Version") == "1.4.0", f"manifest Version = {m.get('Version')}")
    check(m.get("UniqueID") == "MrGlim.HorseTack", f"manifest UniqueID = {m.get('UniqueID')}")
    if expect:
        check(m.get("UpdateKeys") == [expect], f"manifest UpdateKeys = {m.get('UpdateKeys')} (expected [{expect}])")
    else:
        check(m.get("UpdateKeys") in ([], None), f"manifest UpdateKeys = {m.get('UpdateKeys')} (expected empty before the Nexus ID exists)")
    pngs = [n for n in names if n.endswith(".png")]
    check(len(pngs) == len(repo_png), f"{len(pngs)} PNGs in zip, {len(repo_png)} in repo assets/")
    mism = [n for n in pngs if repo_png.get(n[len("HorseTack/"):]) != sha(z.read(n))]
    check(not mism, "every PNG is byte-identical to the repo's own assets/" + (f" (mismatch: {mism[:5]})" if mism else ""))
    hits = [n for n in names if sha(z.read(n)) in third_sha or (n.endswith(".png") and pix(z.read(n)) in third_pix)]
    check(not hits, f"no file matches any of {nthird} third-party reference files (sha256 or pixels)" + (f" HITS: {hits}" if hits else ""))
    for req in ["HorseTack/HorseTack.dll", "HorseTack/manifest.json", "HorseTack/README.txt", "HorseTack/i18n/default.json",
                "HorseTack/assets/collections.json", "HorseTack/assets/README.txt"]:
        check(req in names, f"contains {req}")
    check("HorseTack/README-TESTERS.txt" not in names, "no leftover README-TESTERS.txt (the release readme ships as README.txt)")
    if "HorseTack/README.txt" in names:
        rd = z.read("HorseTack/README.txt").decode("utf-8-sig")
        check("1.4.0" in rd and "test build" not in rd.lower(), "README.txt is the public 1.4.0 readme (no 'test build' wording)")
    print("RESULT:", "PASS" if ok else "FAIL")
    sys.exit(0 if ok else 1)

main()
