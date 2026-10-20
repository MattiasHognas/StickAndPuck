#!/usr/bin/env python3
"""Wraps the published macOS executable in StickAndPuck.app so Finder starts it without a Terminal.

Usage: python3 -I tools/macos_app.py <publish dir> [version] [--zip out.zip]

Makes <publish dir>/StickAndPuck.app (Contents/MacOS/StickAndPuck, Contents/Info.plist, Contents/PkgInfo),
checks its structure and Info.plist, and with --zip packs it into a ZIP that keeps the executable bit.
The bundle is unsigned beyond the ad-hoc signature the .NET SDK gives the executable.
"""

import os
import plistlib
import shutil
import stat
import sys
import zipfile
from pathlib import Path

NAME = "StickAndPuck"
BUNDLE_ID = "io.github.mattiashognas.stickandpuck"
MIN_MACOS = "14.0"  # .NET 11's oldest supported macOS


def info(version: str) -> dict:
    return {
        "CFBundleDevelopmentRegion": "en",
        "CFBundleDisplayName": "Stick & Puck",
        "CFBundleExecutable": NAME,
        "CFBundleIdentifier": BUNDLE_ID,
        "CFBundleInfoDictionaryVersion": "6.0",
        "CFBundleName": NAME,
        "CFBundlePackageType": "APPL",
        "CFBundleShortVersionString": version,
        "CFBundleVersion": version,
        "LSApplicationCategoryType": "public.app-category.sports-games",
        "LSMinimumSystemVersion": MIN_MACOS,
        "NSHighResolutionCapable": True,
    }


def build(publish: Path, version: str) -> Path:
    exe = publish / NAME
    if not exe.is_file():
        sys.exit(f"macos_app: {exe} is missing; publish osx-arm64 first")
    app = publish / f"{NAME}.app"
    shutil.rmtree(app, ignore_errors=True)
    macos = app / "Contents" / "MacOS"
    macos.mkdir(parents=True)
    shutil.copy2(exe, macos / NAME)
    os.chmod(macos / NAME, 0o755)
    with open(app / "Contents" / "Info.plist", "wb") as f:
        plistlib.dump(info(version), f)
    (app / "Contents" / "PkgInfo").write_text("APPL????")
    return app


def check(app: Path, version: str) -> None:
    exe = app / "Contents" / "MacOS" / NAME
    assert exe.is_file(), "the bundle has no executable"
    assert exe.stat().st_mode & stat.S_IXUSR, "the bundle's executable is not executable"
    with open(exe, "rb") as f:
        magic = f.read(4)
    # A Mach-O 64-bit executable (little-endian 0xfeedfacf).
    assert magic == b"\xcf\xfa\xed\xfe", f"the bundle's executable is not Mach-O 64 ({magic.hex()})"
    with open(app / "Contents" / "Info.plist", "rb") as f:
        plist = plistlib.load(f)
    assert plist == info(version), "Info.plist does not hold the expected keys"
    assert (app / "Contents" / "PkgInfo").read_text() == "APPL????", "PkgInfo is wrong"


def pack(app: Path, out: Path) -> None:
    out.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for path in sorted([app, *app.rglob("*")]):
            entry = zipfile.ZipInfo.from_file(path, path.relative_to(app.parent))
            entry.create_system = 3  # Unix, so Archive Utility keeps the modes
            if path.is_dir():
                z.writestr(entry, b"")
            else:
                entry.compress_type = zipfile.ZIP_DEFLATED
                z.writestr(entry, path.read_bytes())
    with zipfile.ZipFile(out) as z:
        mode = z.getinfo(f"{NAME}.app/Contents/MacOS/{NAME}").external_attr >> 16
        assert mode & stat.S_IXUSR, "the zipped executable lost its executable bit"
        assert f"{NAME}.app/Contents/Info.plist" in z.namelist(), "the ZIP has no Info.plist"


def main() -> None:
    args = sys.argv[1:]
    out = None
    if "--zip" in args:
        i = args.index("--zip")
        out = Path(args[i + 1])
        del args[i : i + 2]
    if not args:
        sys.exit(__doc__)
    publish = Path(args[0])
    version = args[1] if len(args) > 1 and args[1] else "0.0.0"
    app = build(publish, version)
    check(app, version)
    print(f"macOS app: {app}")
    if out is not None:
        pack(app, out)
        print(f"macOS app ZIP: {out}")


if __name__ == "__main__":
    main()
