#!/usr/bin/env python3
"""Fail closed if the public DJ Library workflow starts uploading binaries again."""
from pathlib import Path
import sys

workflow = Path(__file__).resolve().parents[1] / ".github/workflows/windows.yml"
source = workflow.read_text(encoding="utf-8")
lines = source.splitlines()
steps = [i for i, line in enumerate(lines) if line.strip() == "- name: Upload clean portable build"]
if len(steps) != 1 or source.count("uses: actions/upload-artifact@") != 1:
    sys.exit("FAIL: the public binary-upload steps changed; review required")
segment = "\n".join(lines[steps[0]:steps[0]+8])
if not any(line.strip() == "if: false" for line in lines[steps[0]:steps[0]+8]):
    sys.exit("FAIL: public DJ Library portable ZIP upload is not disabled")
if "uses: actions/upload-artifact@v4" not in segment:
    sys.exit("FAIL: expected upload step not found")
print("PASS: public DJ Library CI is source-build/test only; binary upload disabled")
