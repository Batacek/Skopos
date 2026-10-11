# Skopos

Double-click `config.json` in your file manager and the coloured content is on screen
within two tenths of a second. No project indexing, no splash screen, no waiting.

Skopos is a **viewer**, not an editor. It opens a file, colours it properly, lets you
search it and fold blocks. Nothing more.

## Why not Notepad, and why not VS Code

|  | Notepad / `cat` | **Skopos** | VS Code / JetBrains |
|---|---|---|---|
| Startup | instant | **~175 ms** | 2–10 s |
| Syntax highlighting | no | **yes** | yes |
| Editing | primitive | **none** | full |
| Project indexing | no | **no** | yes |
| Memory | a few MB | **~50 MB** (target) | 500 MB – 2 GB |

Startup is measured on the NativeAOT build for win-x64: ~145 ms from `Main()` to the first
paint plus ~30 ms of process start. Measure it yourself: `skopos --timing file.json`.
The memory figure is the design target and has not been measured yet.

## Supported languages

| Language | Highlighting | Block folding |
|---|---|---|
| JSON (`.json`, `.jsonc`, `.webmanifest`, …) | yes | objects and arrays |
| Markdown (`.md`, `.markdown`, …) | yes, including GFM tables, task lists and frontmatter | sections and code blocks |
| everything else | plain text | — |  

More languages (Python, C, C++, YAML, TOML, XML) are planned for v1.2.

## Download

No binaries have been released yet — the first public release is planned for v1.1. Until
then, build it from source — see below.

Releases will offer a **portable ZIP** (recommended — unpack and run, nothing is installed)
and a Windows installer.

> ⚠️ **The binaries are not code-signed.** On first run Windows SmartScreen may show
> "Windows protected your PC". Click **More info → Run anyway**. This is expected for
> unsigned open-source software; signing is planned for a later release.

## Building from source

You need:

- **.NET 10 SDK**
- **a C compiler** for the markdown grammars — on Windows the *Desktop development with C++*
  workload, on Linux/macOS gcc or clang
- **git**

```bash
git clone https://github.com/Batacek/skopos
cd skopos
```

```bash
powershell -ExecutionPolicy Bypass -File grammars/build-grammars.ps1
```

On Linux/macOS use `./grammars/build-grammars.sh` instead. Details and the reasoning are in
[`grammars/README.md`](grammars/README.md).

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/Skopos -- file.json
```

### Release package

```bash
powershell -ExecutionPolicy Bypass -File packaging/build-release.ps1
```

Produces a NativeAOT build and a portable ZIP in `artifacts/`. The script also watches the
package size: the win-x64 package is currently about 44 MB unpacked and 18 MB
as a ZIP, and it grows with every language.

## Using it

No menu bar, no toolbar, no sidebar. Everything through keys and a single gear.

| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open file |
| `Ctrl+W` | Close tab |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Next / previous tab |
| `Ctrl+1`–`9` | Jump straight to a tab |
| `Ctrl+F` | Search (`Esc` closes) |
| `F3` / `Shift+F3` | Next / previous match |
| `Ctrl+G` | Go to line |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | Zoom and reset |
| `Alt+Z` | Word wrap |
| `Ctrl+Shift+[` / `]` | Fold / unfold everything |
| `Ctrl+,` | Settings |
| `F5` | Reload |

On macOS `Cmd` instead of `Ctrl`. The middle mouse button also closes a tab, and files can
be dragged from the file manager into the window.

### Status bar

`JSON · UTF-8 · LF · 17 lines` on the left, word wrap and caret position on the right.

**Clicking the encoding switches it** and the file reloads immediately. This is not
cosmetic: the heuristic for Windows-1250, ISO-8859-2 and CP852 will eventually get one
wrong, and you spot it by the mangled accented characters. When Skopos is unsure it puts a
question mark after the encoding.

## Settings

`Ctrl+,`, or by hand in JSON:

| Platform | Path |
|---|---|
| Windows | `%APPDATA%\Skopos\settings.json` |
| Linux | `~/.config/Skopos/settings.json` |
| macOS | `~/Library/Application Support/Skopos/settings.json` |

**Portable mode:** if `settings.json` or an empty `portable.txt` sits next to the
executable, everything is read and written there and the system folders are never touched.
The portable ZIP contains `portable.txt`, so it runs from a USB stick without leaving
traces on the system.

## Options

```bash
skopos --timing file.json
```

| Option | Meaning |
|---|---|
| `--timing` | print the startup breakdown to stderr |
| `--new-instance` | do not hand files to a running instance |
| `-h`, `--help` | help |

## How it works inside

```
src/Skopos              Avalonia application, window, tabs, status bar
src/Skopos.Core         file loading, encoding, settings, single instance — no UI
src/Skopos.Highlighting tree-sitter, category mapping, colourizer, folding
grammars/               grammar build scripts and .scm queries
packaging/              Inno Setup, .desktop, Info.plist
```

`Skopos.Core` deliberately knows nothing about Avalonia — it can be tested without a UI.

Highlighting runs through tree-sitter: file → parser → tree → `.scm` query → list of
(range, category) → `DocumentColorizingTransformer`. The category set is a fixed list of
fifteen entries that every capture from every language maps onto, so colours live only in
the theme. Foldable ranges fall out of the same tree essentially for free.

Markdown is parsed in **two phases** — the block grammar returns `inline` nodes whose
ranges are handed to a second parser. Without that there would be no bold, no italics and
no links.

## What Skopos does not do

This is by design, not a missing feature. Every item smuggled back in would eat into the
startup budget.

Text editing · IntelliSense and autocomplete · language servers · Git and diff · terminal
and debugger · plugins · opening folders as a tree · minimap · cross-file search.

## Licence

Skopos is free software, licensed under the **GNU General Public License, version 3 only**
(`GPL-3.0-only`). See [`LICENSE`](LICENSE) for the full text.

It comes with no warranty, to the extent permitted by applicable law.

Third-party components and their licences are listed in
[`THIRD-PARTY-LICENSES.md`](THIRD-PARTY-LICENSES.md). Contributions are accepted under the
same licence — see [`CONTRIBUTING.md`](CONTRIBUTING.md#licence-of-contributions).