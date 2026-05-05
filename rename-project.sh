#!/usr/bin/env bash
set -euo pipefail

OLD_KEBAB="claude-starter"
OLD_SNAKE="claude_starter"
SKIP_CONFIRM=0
FORCE=0
NEW=""

usage() {
  cat >&2 <<EOF
Usage: $0 <new-name> [--yes] [--force]

  <new-name>   kebab-case, ^[a-z][a-z0-9-]{1,49}$
  --yes, -y    skip confirmation prompt
  --force      bypass template-state safety guard
EOF
  exit 1
}

for arg in "$@"; do
  case "$arg" in
    -y|--yes) SKIP_CONFIRM=1 ;;
    --force)  FORCE=1 ;;
    -h|--help) usage ;;
    -*) echo "Unknown flag: $arg" >&2; usage ;;
    *)  if [[ -z "$NEW" ]]; then NEW="$arg"; else usage; fi ;;
  esac
done

[[ -z "$NEW" ]] && usage

if ! [[ "$NEW" =~ ^[a-z][a-z0-9-]{1,49}$ ]]; then
  echo "Error: name must be kebab-case, 2-50 chars, start with a letter (^[a-z][a-z0-9-]{1,49}\$)" >&2
  exit 1
fi

NEW_SNAKE="${NEW//-/_}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Safety guard
if [[ "$FORCE" -eq 0 ]]; then
  dir="$(basename "$PWD")"
  remote=""
  [[ -d .git ]] && remote="$(git config --get remote.origin.url 2>/dev/null || true)"
  if [[ "$dir" != "$OLD_KEBAB" && "$remote" != *"$OLD_KEBAB"* ]]; then
    echo "Error: this does not look like the $OLD_KEBAB template (dir=$dir, remote=$remote)." >&2
    echo "Use --force to override." >&2
    exit 1
  fi
fi

# Collect files containing placeholders, skipping irrelevant trees
mapfile -t FILES < <(find . \
  \( -path ./.git -o -path ./bin -o -path ./obj \
     -o -path ./node_modules -o -path ./ClientApp/node_modules \
     -o -path ./ClientApp/dist -o -path ./ClientApp/.angular \
     -o -name '*.log' \) -prune \
  -o -type f -print)

HITS=()
for f in "${FILES[@]}"; do
  if LC_ALL=C grep -lI -e "$OLD_KEBAB" -e "$OLD_SNAKE" "$f" >/dev/null 2>&1; then
    HITS+=("$f")
  fi
done

echo "About to rename: $OLD_KEBAB -> $NEW (snake form: $NEW_SNAKE)"
echo "  ${#HITS[@]} files will have content replaced"
echo "  csproj/sln + 2 test project dirs will be renamed"
echo "  README template block will be stripped"
echo "  bin/ obj/ cleaned, .git/ removed, both rename scripts self-delete"
if [[ "$SKIP_CONFIRM" -eq 0 ]]; then
  read -r -p "Continue? [y/N] " ans
  [[ "$ans" =~ ^[Yy]$ ]] || { echo "Aborted."; exit 1; }
fi

# Content replace (sed -i.bak works on both GNU and BSD sed; preserves UTF-8 BOM since byte-level)
for f in "${HITS[@]}"; do
  sed -i.bak -e "s/${OLD_KEBAB}/${NEW}/g" -e "s/${OLD_SNAKE}/${NEW_SNAKE}/g" "$f"
  rm -f "${f}.bak"
done

# Strip template block from README
if [[ -f README.md ]]; then
  sed -i.bak '/<!-- TEMPLATE:START -->/,/<!-- TEMPLATE:END -->/d' README.md
  rm -f README.md.bak
fi

# Rename files (before dirs, so paths still resolve)
[[ -f "claude-starter.csproj" ]] && mv "claude-starter.csproj" "${NEW}.csproj"
[[ -f "claude-starter.sln" ]]    && mv "claude-starter.sln"    "${NEW}.sln"
[[ -f "tests/claude-starter.UnitTests/claude-starter.UnitTests.csproj" ]] && \
  mv "tests/claude-starter.UnitTests/claude-starter.UnitTests.csproj" \
     "tests/claude-starter.UnitTests/${NEW}.UnitTests.csproj"
[[ -f "tests/claude-starter.IntegrationTests/claude-starter.IntegrationTests.csproj" ]] && \
  mv "tests/claude-starter.IntegrationTests/claude-starter.IntegrationTests.csproj" \
     "tests/claude-starter.IntegrationTests/${NEW}.IntegrationTests.csproj"

# Rename dirs
[[ -d "tests/claude-starter.UnitTests" ]] && \
  mv "tests/claude-starter.UnitTests" "tests/${NEW}.UnitTests"
[[ -d "tests/claude-starter.IntegrationTests" ]] && \
  mv "tests/claude-starter.IntegrationTests" "tests/${NEW}.IntegrationTests"

# Clean build artifacts (old assembly names baked in)
find . -type d \( -name bin -o -name obj \) \
  -not -path './ClientApp/node_modules/*' -not -path './node_modules/*' \
  -exec rm -rf {} + 2>/dev/null || true

# Remove git history
rm -rf .git

# Self-delete
rm -f rename-project.sh rename-project.ps1

echo "Done. Project renamed to ${NEW}."
echo "Next steps:"
echo "  git init && git add -A && git commit -m 'Initial commit'"
echo "  cd ClientApp && npm install"
echo "  dotnet restore ${NEW}.sln"
