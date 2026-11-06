#!/usr/bin/env python3
"""Add (or replace) a version entry in the Jellyfin repository manifest.

Usage: update_manifest.py <manifest.json> <meta.json> <version> <changelog> <sourceUrl> <md5> <timestamp>
"""
import json
import os
import sys


def main() -> None:
    manifest_path, meta_path, version, changelog, source_url, checksum, timestamp = sys.argv[1:8]

    with open(meta_path, encoding="utf-8") as f:
        meta = json.load(f)

    manifest = []
    if os.path.exists(manifest_path):
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f)

    plugin = next((p for p in manifest if p.get("guid") == meta["guid"]), None)
    if plugin is None:
        plugin = {"guid": meta["guid"], "versions": []}
        manifest.append(plugin)

    # Plugin details always follow meta.json, so editing meta.json updates the catalogue entry.
    plugin.update({
        "name": meta["name"],
        "description": meta["description"],
        "overview": meta["overview"],
        "owner": meta["owner"],
        "category": meta["category"],
        "imageUrl": meta.get("imageUrl", ""),
    })

    entry = {
        "version": version,
        "changelog": changelog,
        "targetAbi": meta["targetAbi"],
        "sourceUrl": source_url,
        "checksum": checksum,
        "timestamp": timestamp,
    }

    # Newest first; re-releasing a version replaces its entry.
    versions = [v for v in plugin.get("versions", []) if v.get("version") != version]
    versions.insert(0, entry)
    versions.sort(key=lambda v: tuple(int(x) for x in v["version"].split(".")), reverse=True)
    plugin["versions"] = versions

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
