"""Verify the frozen source snapshot without reading build output or local secrets."""

import hashlib
import json
from pathlib import Path


def main():
    root = Path(__file__).resolve().parents[2]
    archive = root / "archive" / "proof-sample"
    manifest = json.loads((archive / "SNAPSHOT.json").read_text())
    paths = set()
    failures = []

    for entry in manifest["files"]:
        relative = Path(entry["path"])
        if relative.is_absolute() or ".." in relative.parts or relative in paths:
            failures.append(f"Invalid or duplicate snapshot path: {relative}")
            continue
        paths.add(relative)
        source = archive / relative
        if not source.is_file() or source.is_symlink():
            failures.append(f"Missing or non-regular snapshot file: {relative}")
            continue
        data = source.read_bytes()
        if (
            len(data) != entry["bytes"]
            or hashlib.sha256(data).hexdigest() != entry["sha256"]
        ):
            failures.append(f"Changed snapshot file: {relative}")

    if failures:
        raise SystemExit("\n".join(failures))
    print(f"Verified {len(paths)} original files from {manifest['source_commit']}.")


if __name__ == "__main__":
    main()
