# Roadmap

Where Skopos is heading. This is a plan, not a promise — versions and their contents can
still move. If you would like to propose something, open an issue.

A version here means a whole release series: a feature listed under v1.4 may ship in
v1.4.0 or in a later v1.4.x release.

---

## The rule every feature has to follow

**Nothing may slow down opening a file.**

When a file opens, only what is truly needed to show it is loaded — the critical path:

1. read the file,
2. detect its encoding,
3. show the text,
4. highlight the visible part.

Everything else — outlines, error marks, previews, extra panels, optional modules — loads
**after** the file is on screen. A feature that cannot be built this way does not get
built. Startup time is measured in CI on every change, so a regression shows up
immediately.

---

## v1.1 — first public release

What exists today, released for the first time.

- Tabbed, read-only viewer with tree-sitter syntax highlighting
- **JSON** and **Markdown** (including GFM tables, task lists and frontmatter)
- Block folding, search, word wrap, light / dark / system theme
- Encoding and line-ending detection with a manual encoding switch
- Single instance, portable mode, large-file handling
- NativeAOT build, portable ZIP and Windows installer

## v1.2 — rendered Markdown and more languages

**Markdown rendering**

- Switch between the source and the rendered document
- Code blocks in the rendered view highlighted with the same tree-sitter highlighting as
  the source view
- The renderer loads only the first time a rendered Markdown view is opened, so opening any
  other file is unaffected

*Open decision:* which rendering library to use. Requirements: support for Avalonia 12 and
NativeAOT, a GPL-3.0-compatible licence, no embedded web browser — and, because of v1.4,
support for editing directly in the rendered view.

**More languages**

- A first batch of additional languages, starting with grammars that are already available
  precompiled
- Detection by shebang (`#!/usr/bin/env python`), not only by file extension
- Manual language switch in the status bar, for files whose extension is missing or
  misleading

## v1.3 — editing

Skopos stays a viewer first. Editing is a **mode you switch on**, not the default.

- A **lock** in the status bar toggles between read-only and editing, separately for each
  tab
- Default mode configurable in settings; out of the box files open read-only
- The lock stays closed for files that cannot be written (read-only attribute, missing
  permissions) and for files above the large-file limit, with an explanation in its tooltip
- Saving keeps the file's original encoding, byte order mark and line endings
- Save / Save as, and a prompt when closing a tab or the app with unsaved changes
- A clear choice when a file changes on disk while it has unsaved changes
- Live preview: the rendered Markdown view follows the source while you type
- More languages

Editing means editing text — no language intelligence in the core (see
[Out of scope](#out-of-scope) and [Optional modules](#optional-modules)).

## v1.4 — JSON and Markdown tools

**Markdown**

- **Editing in the rendered view** — change the document directly in the preview, without
  switching to the source. Changes are written back as plain Markdown, leaving the rest of
  the source untouched.
- **Office-style shortcuts** — `Ctrl+B` bold, `Ctrl+I` italic, `Ctrl+K` link and similar,
  working in both the source and the rendered view
- **Outline** — jump to any heading

**JSON**

- **Formatted view of minified JSON** — a one-line JSON file shown readable
- **JSON path in the status bar** — the path to the element under the caret, e.g.
  `$.dependencies[0].version`, with an option to copy it

**Everywhere**

- **Syntax error marks** — invalid parts of a file are underlined; while editing, marks
  appear only after a short pause in typing
- **Reopen closed tab** (`Ctrl+Shift+T`)

*Open decision:* whether the formatted JSON view is only a view, or also offers formatting
the file itself.

## v1.5 — navigating code

- **Go to symbol** for code — classes, methods, functions — built on the v1.4 outline
- **Sticky scroll** — the current heading, class or function stays pinned at the top while
  scrolling
- **Search:** regular expressions and whole-word matching

## v1.6 — binary files

- **Hex view** for files detected as binary, instead of the "binary file" notice — with
  large files handled without loading them into memory at once

---

## Optional modules

Some integrations are useful to some people and dead weight to others. These come as
**optional modules**, built into Skopos but **switched off by default**. Each user turns on
only what they want.

How modules behave:

- **Off by default.** A disabled module does nothing at all — it is not initialised and
  costs nothing at startup.
- **Never on the critical path.** An enabled module loads only after the file is on screen,
  or — where that makes sense — not until it is first used. Which of the two is up to the
  module: information that is simply shown (such as the current Git branch) loads after
  the file appears; actions load on first use.
- **External tools over bundled libraries.** Where possible a module uses a program that is
  already installed (for example `git`), so it adds nothing to the download for people who
  never enable it.
- **Failure is contained.** A module that cannot work — say Git is not installed — shows a
  notice. It never takes the viewer down.
- **Startup is measured with all modules enabled**, not only with the defaults.

Modules are part of Skopos itself. Third-party plugins remain out of scope.

Candidates, not yet scheduled:

- **Lightweight Git** — the current branch in the status bar and markers next to changed
  lines. Information, not a Git client.
- **Language server client** — a distant possibility: autocomplete and diagnostics through
  language servers the user installs and configures. Skopos would not bundle any.

The module infrastructure itself arrives together with the first module.

---

## Ideas without a version yet

Good ideas that are wanted but not yet scheduled.

- **Read from standard input** — `git show HEAD:config.json | skopos -`
- **Open at a line from the command line** — `skopos file.py:42`
- **Follow mode for logs** — like `tail -f`: new lines appear and the view stays at the end
- **Word completion while editing** — suggestions from words and symbols already in the
  document; no external process, no project
- **Bracket matching, indent guides, visible whitespace**
- **Copy with colours** — as HTML or RTF, for pasting highlighted code into documents and
  presentations
- **Custom colour themes**, possibly with import of VS Code themes
- **`.editorconfig` support** — tab width and similar per-project settings

---

## Distribution

Not tied to any version and not a change to the program itself, but it matters for how
Skopos reaches people:

- **Code signing.** Releases are currently unsigned, so Windows SmartScreen shows a warning
  on first run. Signing will be sorted out separately.
- **Package managers and platforms** — winget, Linux packages, a notarised macOS build,
  arm64 builds.

---

## Out of scope

These stay out on purpose. Each of them would make Skopos heavier, tie it to whole projects
instead of single files, and turn it into a slower version of an IDE.

This list reflects the current direction, not a permanent decision. If a way turns up to
build one of these without breaking the rule above, it may move into the roadmap — most
likely as an optional module.

- Third-party plugins
- A full Git client — committing, branching, browsing history — and diff views
- Language intelligence built into the core (anything beyond the optional language server
  module above)
- Terminal and debugger
- Opening folders as a tree / project view
- Minimap
- Search across files
