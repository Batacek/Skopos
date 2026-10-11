# Changelog

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
versioning is [SemVer](https://semver.org/).

## [v1.1.0] 2026-10-11 _(Unreleased)_

### Added

- `LICENSE` — Skopos is licensed under the GNU General Public License, version 3 only
  (`GPL-3.0-only`)

### Changed

- `UTF.Unknown` is now used under its GPL-2.0-or-later option instead of MPL-1.1, which is
  incompatible with the GPL
- The first public release will ship unsigned; signing is deferred

## v1.0.0 — 2026-06-09

Internal milestone — tagged, not published as a release. A viewer, not an editor.

### Added

- Tabbed window over AvaloniaEdit, read-only
- Syntax highlighting through tree-sitter with a fixed set of fifteen categories
- **JSON** — highlighting and folding of objects and arrays
- **Markdown** — two-phase parse (block + inline grammar), GFM tables, task lists,
  strikethrough, YAML and TOML frontmatter; folding of sections and code blocks
- Block folding derived from the syntax tree, with a preview of the folded content
- Encoding detection: BOM → UTF-8 validation → heuristic → fallback, with a manual
  switch in the status bar that is remembered per file for the session
- Line ending detection, including a warning about mixed endings
- Binary file detection with a "Show anyway" option
- Files open shared (`FileShare.ReadWrite | FileShare.Delete`), so they never block
  a build in someone else's project
- Change watching on disk — a file reloads itself with the scroll position preserved,
  a deleted file is marked in the tab header
- Single instance over a named pipe; a further double-click only adds a tab and brings
  the window forward
- Search (`Ctrl+F`), without regex and without replace
- Word wrap by language, toggled with `Alt+Z`
- Light / dark / system theme, reacting to a system theme change at runtime
- Settings in JSON with source-generated serialization, including portable mode
- Large files: above 2 MB parsing moves to the background, above 20 MB highlighting is
  switched off with an offer to enable it (both thresholds are configurable)
- Drag and drop of files into the window
- `--timing` for a breakdown of startup
- NativeAOT build for win-x64 and a portable ZIP

### Known limitations

- Binaries are unsigned — Windows SmartScreen shows a warning on first run
- No font is bundled; a chain of system monospace fonts is used instead
- The Inno Setup installer and the Linux/macOS packages are prepared but unverified
- The README has no startup GIF 

[v1.1.0]: https://github.com/Batacek/skopos/compare/v1.1.0...HEAD