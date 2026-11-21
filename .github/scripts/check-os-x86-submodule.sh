#!/usr/bin/env bash
# The contracts of Os-x86 (OsX86.Interface, Ix86) come from the submodule external/Os-x86
# (question 320). It is never modified from this repository, and the Kazo OS it pins in its own
# submodule must be the one this repository pins: BoxedWine and Os-x86 run in the same Kazo.
set -euo pipefail

submodule="external/Os-x86"
interface="$submodule/src/OsX86.Interface/OsX86.Interface.csproj"
if [[ ! -f "$interface" ]]; then
  echo "::error::$interface not found. Run: git submodule update --init --recursive"
  exit 1
fi

if [[ -n "$(git -C "$submodule" status --porcelain --ignore-submodules=dirty)" ]]; then
  echo "::error::$submodule has local changes. Os-x86 is not modified from this repository."
  git -C "$submodule" status --short
  exit 1
fi

ours="$(git -C external/KazoOs rev-parse HEAD)"
theirs="$(git -C "$submodule/external/KazoOs" rev-parse HEAD)"
if [[ "$ours" != "$theirs" ]]; then
  echo "::error::external/KazoOs is at $ours but Os-x86 pins Kazo OS at $theirs: move both to the same tag."
  exit 1
fi
echo "Os-x86 submodule: $(git -C "$submodule" rev-parse --short HEAD), clean, same Kazo OS ($ours)."
