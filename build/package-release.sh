#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$ROOT_DIR"

CONFIGURATION=${CONFIGURATION:-Release}
PROJECT="src/EntityEspActPlugin.Act/EntityEspActPlugin.Act.csproj"
OUTPUT_DIR="artifacts/release"
STAGING_DIR="artifacts/package/EntityEspActPlugin"
DLL_PATH="src/EntityEspActPlugin.Act/bin/$CONFIGURATION/net48/EntityEspActPlugin.Act.dll"

VERSION=$(python - <<'PY'
import re
from pathlib import Path
text = Path('Directory.Build.props').read_text(encoding='utf-8')
match = re.search(r'<Version>([^<]+)</Version>', text)
if not match:
    raise SystemExit('Directory.Build.props does not contain <Version>.')
print(match.group(1).strip())
PY
)

if [[ -z "$VERSION" ]]; then
  echo "Version is empty." >&2
  exit 1
fi

if [[ "${GITHUB_REF_TYPE:-}" == "tag" && "${GITHUB_REF_NAME:-}" != "v$VERSION" ]]; then
  echo "Tag ${GITHUB_REF_NAME} does not match Directory.Build.props version v$VERSION." >&2
  exit 1
fi

dotnet build "$PROJECT" -c "$CONFIGURATION" --no-restore

test -f "$DLL_PATH" || { echo "Missing plugin DLL: $DLL_PATH" >&2; exit 1; }

rm -rf artifacts/package "$OUTPUT_DIR"
mkdir -p "$STAGING_DIR" "$OUTPUT_DIR"
cp "$DLL_PATH" "$STAGING_DIR/"
cp README.md "$STAGING_DIR/"
cp docs/configuration-guide.md "$STAGING_DIR/"

ARCHIVE_NAME="EntityEspActPlugin-v$VERSION.zip"
python - "$STAGING_DIR" "$OUTPUT_DIR/$ARCHIVE_NAME" <<'PY'
import os
import sys
import zipfile
from pathlib import Path

src = Path(sys.argv[1])
dst = Path(sys.argv[2])
with zipfile.ZipFile(dst, 'w', compression=zipfile.ZIP_DEFLATED) as zf:
    for path in sorted(src.rglob('*')):
        if path.is_file():
            zf.write(path, path.relative_to(src.parent).as_posix())
PY

echo "$VERSION" > "$OUTPUT_DIR/version.txt"
echo "Created $OUTPUT_DIR/$ARCHIVE_NAME"
