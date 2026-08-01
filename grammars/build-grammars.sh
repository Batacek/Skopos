#!/usr/bin/env bash
#
# Builds the native tree-sitter grammars that are not in the TreeSitter.DotNet package.
#
# TreeSitter.DotNet ships the runtime and the JSON grammar, but not markdown. Markdown is
# also split into two grammars (block and inline), so two libraries get compiled.
#
# The src/parser.c files are already generated in the repository, so neither the tree-sitter
# CLI nor Node.js is needed — a C compiler is enough.
#
# Usage:  ./build-grammars.sh [RID]
#         RID is e.g. linux-x64 (defaults to the host), osx-arm64, …

set -euo pipefail

GRAMMARS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENDOR_DIR="$GRAMMARS_DIR/vendor"

MARKDOWN_REPO="https://github.com/MDeiml/tree-sitter-markdown"
MARKDOWN_BRANCH="split_parser"
# Pinned commit, so highlighting cannot change under our hands between two builds.
MARKDOWN_COMMIT="a0a00f817d02412bd92c54d316f164d827b57b5c"

detect_rid() {
  local os arch
  case "$(uname -s)" in
    Linux)  os="linux" ;;
    Darwin) os="osx" ;;
    *) echo "Unsupported system: $(uname -s)" >&2; exit 1 ;;
  esac
  case "$(uname -m)" in
    x86_64|amd64) arch="x64" ;;
    aarch64|arm64) arch="arm64" ;;
    *) echo "Unsupported architecture: $(uname -m)" >&2; exit 1 ;;
  esac
  echo "$os-$arch"
}

RID="${1:-$(detect_rid)}"
OUTPUT_DIR="$GRAMMARS_DIR/native/$RID"

case "$RID" in
  linux-*) SOEXT="so";   LINK_FLAGS=(-shared -fPIC) ;;
  osx-*)   SOEXT="dylib"; LINK_FLAGS=(-dynamiclib -fPIC) ;;
  *) echo "Unknown RID '$RID'. On Windows use build-grammars.ps1." >&2; exit 1 ;;
esac

CC="${CC:-cc}"
command -v "$CC" >/dev/null || { echo "Could not find a C compiler ($CC). Install gcc or clang." >&2; exit 1; }
command -v git >/dev/null || { echo "Could not find git." >&2; exit 1; }

# --- sources --------------------------------------------------------------

REPO_DIR="$VENDOR_DIR/tree-sitter-markdown"
if [ ! -d "$REPO_DIR/.git" ]; then
  echo "Cloning $MARKDOWN_REPO ($MARKDOWN_BRANCH) …"
  mkdir -p "$VENDOR_DIR"
  git clone --branch "$MARKDOWN_BRANCH" "$MARKDOWN_REPO" "$REPO_DIR"
fi
git -C "$REPO_DIR" checkout --quiet "$MARKDOWN_COMMIT"

# --- compile --------------------------------------------------------------

mkdir -p "$OUTPUT_DIR"

for name in tree-sitter-markdown tree-sitter-markdown-inline; do
  src_dir="$REPO_DIR/$name/src"
  [ -f "$src_dir/parser.c" ] || { echo "Missing $src_dir/parser.c" >&2; exit 1; }

  sources=("$src_dir/parser.c")
  [ -f "$src_dir/scanner.c" ] && sources+=("$src_dir/scanner.c")

  output="$OUTPUT_DIR/lib$name.$SOEXT"
  echo "Compiling $name → $output"
  "$CC" -O2 -std=c11 -w -I"$src_dir" "${LINK_FLAGS[@]}" "${sources[@]}" -o "$output"
done

echo
echo "Done. Libraries in $OUTPUT_DIR :"
ls -lh "$OUTPUT_DIR" | tail -n +2
