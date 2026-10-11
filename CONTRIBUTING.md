# Contributing

Thanks for the interest. A few things worth knowing up front — it saves time on both sides.

## What Skopos is, and what it is not

Skopos is a **viewer**, not an editor. Its one argument is that it opens a file with proper
syntax highlighting faster than anything that does the same — well under 200 ms from a
double-click to coloured text.

Some things are left out on purpose, not for lack of time: text editing, IntelliSense,
language servers, git, a terminal, plugins, opening folders as a tree. The full list is in
the README under [What Skopos does not do](README.md#what-skopos-does-not-do).

If you want to add a feature, **open a proposal issue first** and describe what it costs at
startup. A pull request for something out of scope will be closed, however well it is
written.

## Licence of contributions

Skopos is licensed under the **GNU General Public License, version 3 only**
(`GPL-3.0-only`) — see [`LICENSE`](LICENSE).

By submitting a contribution (a pull request, a patch, or code in an issue) you agree that
it is licensed under GPL-3.0-only, the same licence as the rest of the project, and that you
have the right to license it that way.

## Setting up

You need:

- **.NET 10 SDK**
- **a C compiler** — on Windows the *Desktop development with C++* workload of Visual
  Studio, on Linux/macOS gcc or clang
- **git**

```bash
git clone https://github.com/Batacek/skopos
cd skopos
```

The markdown grammars are compiled from source and are **not in the repository**
(`grammars/native/` is gitignored). Build them first:

```bash
powershell -ExecutionPolicy Bypass -File grammars/build-grammars.ps1
```

On Linux/macOS use `./grammars/build-grammars.sh`. Then:

```bash
dotnet build
dotnet test
dotnet run --project src/Skopos -- file.json
```

Without the grammars the build still succeeds, but emits warning `SKOPOS001`, markdown
renders as plain text and the markdown tests fail.

## Keeping startup fast

If you touch anything that runs before the first window appears, measure before and after:

```bash
skopos --timing file.json
```

How the startup path is built, and what to keep that way:

- **Nothing blocks the first paint.** Files passed on the command line open immediately;
  everything else — loading settings, applying the theme, the single-instance listener —
  is deferred until the window has opened. Until settings load, defaults are used.
- **Grammars load on demand**, per language, the first time a file needs them.
- **System fonts are enumerated only when the settings window opens.** It is slow.
- **No splash screen.**

## NativeAOT constraints

Release builds are compiled with NativeAOT. Reflection-based code compiles fine in a
normal `dotnet build` and then fails in the published binary, so these are rules, not
preferences:

- **No DI container and no MVVM framework.** Services are created with plain `new`;
  code-behind is fine.
- **Compiled bindings everywhere.**
- **Every `Window` and `UserControl` needs a public parameterless constructor.** Pass
  arguments through a method instead — see `GoToLineWindow.Initialize()`.
- **JSON goes through the source-generated context** (`SettingsJsonContext`), never
  through reflection-based serialization.

## Adding a language

A new language means all of the following:

1. The grammar — either one already shipped in the `TreeSitter.DotNet` package, or built
   from source like the markdown grammars.
2. A `.scm` highlight query in `grammars/queries/`, mapping captures onto the fixed set of
   highlight categories.
3. An entry in `LanguageRegistry`, including which node types fold.
4. Tests for both highlighting and folding.
5. The `SkoposTrimPublishOutput` filter in `src/Skopos/Skopos.csproj` updated. It drops
   unused grammars from the package; a grammar it does not know about **silently never
   reaches the release build**, and nothing fails.
6. An entry in [`THIRD-PARTY-LICENSES.md`](THIRD-PARTY-LICENSES.md) for the grammar, and
   a separate one for every borrowed `.scm` file — under a compatible licence, see below.

## Licences of borrowed code

Anything taken from elsewhere — a grammar, a `.scm` query, a NuGet package, a snippet —
must be under a licence **compatible with GPL-3.0**, because it ends up in a GPL-3.0
program. Grammar and query licences tend to be a mix and are sometimes unclear, so check
before you copy.

| | Licences |
|---|---|
| ✅ Compatible | MIT, BSD (2- and 3-clause), ISC, Zlib, Apache-2.0, LGPL (any version), GPL-2.0-or-later, GPL-3.0 |
| ⚠️ Check first | MPL-2.0 — compatible unless the file is marked "Incompatible With Secondary Licenses"; dual- or tri-licensed code — pick the GPL-compatible option and say so in `THIRD-PARTY-LICENSES.md` |
| ❌ Not compatible | GPL-2.0-only, MPL-1.1 alone, EPL-1.0, CDDL, anything with extra usage restrictions, code with no licence at all |

When in doubt, ask in the issue before writing code around it.

## Tests that must stay as they are

A few tests look odd but guard real bugs. Do not remove or "simplify" them:

- **Czech text in the test data** (`SyntaxAnalyzerTests`, `TextEncodingDetectorTests`,
  `TextFileLoaderTests`) is the thing under test. It proves that highlighting offsets are
  UTF-16 units rather than UTF-8 bytes, and that the 8-bit encoding heuristic works.
  Replacing it with ASCII would make those tests pass for the wrong reason.
- **`Diacritics_DoNotShiftOffsets`** — without it, a wrong offset unit would mis-colour
  every file with accented characters and nothing else would notice.
- **`Markdown_SectionFoldStopsBeforeTheNextHeading`** — guards a fixed bug where folding a
  section swallowed the following heading.
- **`FormatSize_IsReadable`** pins the `cs-CZ` culture on purpose, so the result does not
  depend on the machine running it.

## Pitfalls on Windows

**Windows PowerShell 5.1 corrupts UTF-8 files.** It reads a `.ps1` without a BOM as ANSI,
and `Get-Content -Raw` → `Set-Content` silently turns `…` into `â€¦`. That once broke the
fold previews (`{…}`). Never round-trip source files through PowerShell 5.1, and keep the
UTF-8 BOM on `grammars/build-grammars.ps1` and `packaging/build-release.ps1`.

**"No precompiled XAML found" means a stale build, not a broken project.** An interrupted
build can leave behind an assembly whose XAML was never compiled, and later builds skip the
step because they consider it up to date. Delete `bin/` and `obj/`, or run
`dotnet build --no-incremental`.

## Style

- Code, comments, user-facing text and commit messages are in English.
- A comment explains *why*, in full. A comment that repeats what the code says does not
  belong in the repository.
  are not part of the repository. You can ignore those references; do not add new ones.
- Error states are not swept under the rug: a file that will not open, a missing grammar or
  broken settings must never take the viewer down — but they must not pretend everything is
  fine either.
- Commit messages use the form `area: lowercase subject`, e.g.
  `highlighting: fold fenced code blocks in lists`.

## Before you open a pull request

- `dotnet test` passes, with the grammars built.
- Anything on the startup path is measured with `--timing`.
- There is a line under `[Unreleased]` in `CHANGELOG.md`.

## Reporting bugs

Say what you opened, what you expected and what happened. For problems with accented
characters attach the file, or at least what the status bar reports as the encoding — the
heuristic gets Windows-1250, ISO-8859-2 and CP852 wrong now and then, and that is the
first clue.