#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/_load_env.sh"

MOD_BASENAME="${MOD_BASENAME:-RoutingHelper}"
DIST_DIR="${PROJECT_ROOT}/dist/${MOD_BASENAME}"
SRC_DLL="${DIST_DIR}/${MOD_BASENAME}.dll"
SRC_PCK="${DIST_DIR}/${MOD_BASENAME}.pck"

if [[ -z "${STS2_INSTALL_DIR:-}" ]]; then
  echo "STS2_INSTALL_DIR is not set. Create ${PROJECT_ROOT}/.env from .env.example first." >&2
  exit 1
fi

GAME_MOD_DIR="${1:-${STS2_INSTALL_DIR}/mods/${MOD_BASENAME}}"
DST_DLL="${GAME_MOD_DIR}/${MOD_BASENAME}.dll"
DST_PCK="${GAME_MOD_DIR}/${MOD_BASENAME}.pck"

if [[ ! -f "${SRC_DLL}" ]]; then
  echo "Missing ${SRC_DLL}. Run scripts/bash/build_and_stage.sh first." >&2
  exit 1
fi

mkdir -p "${GAME_MOD_DIR}"
if ! cp -f "${SRC_DLL}" "${DST_DLL}"; then
  echo "Failed to copy ${MOD_BASENAME}.dll. It is likely locked by a running game process." >&2
  exit 1
fi

if [[ -f "${SRC_PCK}" ]]; then
  if ! cp -f "${SRC_PCK}" "${DST_PCK}"; then
    echo "Failed to copy ${MOD_BASENAME}.pck. Close Slay the Spire 2 and rerun this script." >&2
    exit 1
  fi
else
  echo "Warning: ${MOD_BASENAME}.pck not found in dist; DLL only was installed."
fi

if ! cmp -s "${SRC_DLL}" "${DST_DLL}"; then
  echo "ERROR: Copied DLL does not match source after install." >&2
  exit 1
fi

if [[ -f "${SRC_PCK}" ]] && ! cmp -s "${SRC_PCK}" "${DST_PCK}"; then
  echo "ERROR: Copied PCK does not match source after install." >&2
  exit 1
fi

echo "Installed to: ${GAME_MOD_DIR}"
ls -la "${GAME_MOD_DIR}"

if command -v sha256sum >/dev/null 2>&1; then
  echo "DLL SHA256:"
  sha256sum "${SRC_DLL}" "${DST_DLL}"
  if [[ -f "${SRC_PCK}" ]]; then
    echo "PCK SHA256:"
    sha256sum "${SRC_PCK}" "${DST_PCK}"
  fi
fi

if command -v strings >/dev/null 2>&1 && command -v rg >/dev/null 2>&1; then
  marker_line="$(strings -el "${DST_DLL}" | rg -m1 "Mod bootstrap starting\\. build=" || true)"
  if [[ -n "${marker_line}" ]]; then
    echo "Installed DLL marker: ${marker_line}"
  fi
fi
