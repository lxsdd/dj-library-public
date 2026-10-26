#!/usr/bin/env python3
"""DJ Library source-only publication gate, with immutable common-source pin.

This is a CI audit, not permission to distribute compiled applications.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
scripts = root / "scripts"
expected_commit = "4ea764a2e586ebe3c4274634666f77b3370dc246"
expected_blob = "ff55f22ac0aafc500e8ee5235e253d0bb4ad8d69"
pin = json.loads((scripts / "SHARED_PUBLICATION_GUARD.pin.json").read_text(encoding="utf-8"))
if (pin.get("source_repo") != "lxsdd/dev-infrastructure-public"
        or pin.get("source_commit") != expected_commit
        or pin.get("source_path") != "shared/ci/publication_guard.py"
        or pin.get("source_git_blob_sha1") != expected_blob):
    sys.exit("FAIL_DJ_LIBRARY_SHARED_PUBLICATION_PIN_DRIFT")

canonical = (scripts / "_shared_publication_guard.py").read_bytes().replace(b"\r\n", b"\n")
actual_blob = hashlib.sha1(
    ("blob " + str(len(canonical)) + "\0").encode("ascii") + canonical
).hexdigest()
if actual_blob != expected_blob:
    sys.exit("FAIL_DJ_LIBRARY_SHARED_PUBLICATION_SOURCE_DRIFT")

from _shared_publication_guard import audit_repository, scan_workflows

policy = {
    "version": 1,
    "expected_artifact_actions": {"windows.yml": 1},
    "required_disabled_steps": {
        "windows.yml": [
            "Package app without user data",
            "Upload clean portable build",
        ],
    },
    "allowed_workflows": ["windows.yml", "public-pr-head-ci-recovery.yml"],
}
failures = audit_repository(root, policy)
if failures:
    sys.exit("FAIL_DJ_LIBRARY_PUBLICATION_SURFACE:\n- " + "\n- ".join(failures))
print("PASS_DJ_LIBRARY_CANONICAL_PUBLICATION_GUARD")

folder = root / ".github" / "workflows"
sources = {p.name: p.read_text(encoding="utf-8")
           for p in folder.iterdir() if p.suffix.lower() in (".yml", ".yaml")}
windows = sources["windows.yml"]


def mutate_and_expect(label: str, changed: dict[str, str], expected: str) -> None:
    violations = scan_workflows(
        changed,
        policy["expected_artifact_actions"],
        required_disabled_steps=policy["required_disabled_steps"],
        allowed_workflows=policy["allowed_workflows"],
    )
    if not any(expected in error for error in violations):
        sys.exit(f"FAIL_DJ_LIBRARY_MUTATION_{label}: {violations}")
    print(f"PASS_DJ_LIBRARY_MUTATION_{label}")


def replace_once(label: str, old: str, new: str) -> str:
    if windows.count(old) != 1:
        sys.exit(f"FAIL_DJ_LIBRARY_MUTATION_ANCHOR_{label}")
    return windows.replace(old, new, 1)


mutate_and_expect(
    "ENABLE_UPLOAD",
    {**sources, "windows.yml": replace_once(
        "ENABLE_UPLOAD",
        "- name: Upload clean portable build\n        if: false",
        "- name: Upload clean portable build\n        if: true",
    )},
    "enabled artifact upload",
)
mutate_and_expect(
    "ENABLE_PACKAGE",
    {**sources, "windows.yml": replace_once(
        "ENABLE_PACKAGE",
        "- name: Package app without user data\n        if: false",
        "- name: Package app without user data\n        if: true",
    )},
    "required disabled step",
)
mutate_and_expect(
    "QUOTED_UPLOAD",
    {**sources, "extra-quoted.yml": (
        "jobs:\n  x:\n    runs-on: ubuntu-latest\n    steps:\n"
        "      - uses: 'actions/upload-artifact@v7'\n"
    )},
    "enabled artifact upload",
)
mutate_and_expect(
    "QUOTED_RELEASE",
    {**sources, "extra-release.yml": (
        "jobs:\n  x:\n    runs-on: ubuntu-latest\n    steps:\n"
        '      - uses: "softprops/action-gh-release@v2"\n'
    )},
    "enabled release publication",
)
mutate_and_expect(
    "ALIAS_ACTION",
    {**sources, "extra-alias.yml": (
        "jobs:\n  x:\n    runs-on: ubuntu-latest\n    steps:\n"
        "      - uses: *unsafe_action\n"
    )},
    "YAML action alias/anchor",
)
mutate_and_expect(
    "UNREVIEWED_WORKFLOW",
    {**sources, "unexpected.yml": "name: unexpected\non: push\njobs: {}\n"},
    "Unreviewed, missing or renamed workflow",
)
print("PASS_DJ_LIBRARY_PUBLICATION_MUTATIONS_6_OF_6")

# Additional mutation regressions for the exact hardened canonical guard.
# Keep canonical steps and disabled package/upload gates byte-for-byte.
noncanonical_uses = (
    "      - uses: >-\n          actions/upload-artifact@v4\n",
    "      - uses: |-\n          actions/upload-artifact@v4\n",
    "      - 'uses': actions/upload-artifact@v4\n",
    '      - "uses": actions/upload-artifact@v4\n',
    "      - uses : actions/upload-artifact@v4\n",
    "      - uses: !!str actions/upload-artifact@v4\n",
    "      - {name: Publish, uses: actions/upload-artifact@v4}\n",
)
for index, variant in enumerate(noncanonical_uses, 1):
    # Place the injected action inside an existing enabled step job. The
    # existing disabled upload remains to prove the exact-count gate alone
    # cannot detect the new action.
    anchor = "      # Do not publish Windows product binaries from this source-only Draft."
    if windows.count(anchor) != 1:
        sys.exit("FAIL_DJ_LIBRARY_NONCANONICAL_INSERTION_ANCHOR")
    altered = windows.replace(anchor, variant + anchor, 1)
    mutate_and_expect(
        f"NONCANONICAL_YAML_{index}",
        {**sources, "windows.yml": altered},
        "noncanonical YAML action key/scalar",
    )
print("PASS_DJ_LIBRARY_PUBLICATION_MUTATIONS_13_OF_13")
