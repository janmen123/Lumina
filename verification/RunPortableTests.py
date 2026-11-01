"""Compatibility launcher for current Windows production regression checks.
The old source-extraction/.NET 8 harness targeted removed GlassBoard modules.
Use --binary to select a candidate Lumina.exe. No third-party packages required.
"""
from pathlib import Path
import argparse
import os
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--binary", type=Path)
args = parser.parse_args()
if os.name != "nt":
    parser.exit(2, "Current production tests require Windows/.NET Framework. Run verification/RunRegression.ps1 on Windows.\n")
command = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(Path(__file__).with_name("RunRegression.ps1"))]
if args.binary:
    command += ["-Binary", str(args.binary.resolve())]
raise SystemExit(subprocess.call(command))
