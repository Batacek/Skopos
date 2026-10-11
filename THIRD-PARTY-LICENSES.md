# Third-party licences

Skopos distributes other people's code in binary form. This file records whose, under what
licence, and what follows from that. It is kept up to date continuously, not just before a
release.

**State:** current as of 11 October 2026, for v1.0.0 (internal milestone, not released).

---

## Summary for the impatient

Skopos itself is licensed under the **GNU General Public License, version 3 only**
(`GPL-3.0-only`) — see [`LICENSE`](LICENSE).

Every component below is compatible with that. All of them are MIT or BSD, with one
exception: **UTF.Unknown is tri-licensed, and Skopos uses it under its GPL-2.0-or-later
option** — see [UTF.Unknown](#utfunknown) below.

---

## Managed code (NuGet)

| Component | Version | Licence | Author / source |
|---|---|---|---|
| Avalonia | 12.0.4 | MIT | [avaloniaui.net](https://avaloniaui.net) |
| Avalonia.AvaloniaEdit | 12.0.0 | MIT | [AvaloniaUI/AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) |
| TreeSitter.DotNet | 1.3.0 | MIT | [mariusgreuel/tree-sitter-dotnet-bindings](https://github.com/mariusgreuel/tree-sitter-dotnet-bindings) |
| SkiaSharp | 3.119.4 | MIT | Microsoft / Mono |
| HarfBuzzSharp | 8.3.1.3 | MIT | Microsoft / Mono |
| MicroCom.Runtime | 0.11.4 | MIT | Avalonia |
| UTF.Unknown | 2.6.0 | MPL-1.1 / GPL-2.0-or-later / LGPL-2.1-or-later — **used under GPL-2.0-or-later** | [CharsetDetector/UTF-unknown](https://github.com/CharsetDetector/UTF-unknown) |

## Native libraries in the package

| File | Origin | Licence |
|---|---|---|
| `tree-sitter.dll` / `.so` / `.dylib` | [tree-sitter](https://github.com/tree-sitter/tree-sitter), Max Brunsfeld | MIT |
| `tree-sitter-json.*` | [tree-sitter-json](https://github.com/tree-sitter/tree-sitter-json) | MIT |
| `tree-sitter-markdown.*` | [MDeiml/tree-sitter-markdown](https://github.com/MDeiml/tree-sitter-markdown), Copyright (c) 2021 Matthias Deiml | MIT |
| `tree-sitter-markdown-inline.*` | same, `split_parser` branch | MIT |
| `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`, `av_libglesv2.dll` | Avalonia / Skia / HarfBuzz | MIT (Skia: BSD-3-Clause for the upstream C++) |

The tree-sitter runtime and the JSON grammar arrive precompiled inside the TreeSitter.DotNet
package. Skopos compiles the markdown grammars itself from source — see
[`grammars/build-grammars.ps1`](grammars/build-grammars.ps1); the exact commit is pinned in
that script.

## Highlight queries (`.scm`)

The files in [`grammars/queries/`](grammars/queries/) are **original**, written for Skopos,
and fall under the Skopos licence. They are not taken from Neovim or Helix, so no
third-party licence applies to them.

Node names (`atx_heading`, `fenced_code_block`, `pipe_table_header`, …) are part of the
grammar's public interface — they come from its `node-types.json` and are not protected
content.

> Should a third-party `.scm` file or grammar ever be adopted, it has to be listed here
> **separately for each file**, and its licence has to be **compatible with GPL-3.0** —
> see the list in [`CONTRIBUTING.md`](CONTRIBUTING.md#licences-of-borrowed-code).
> Query licences tend to be a mix of MIT/Apache and are sometimes unclear.

---

## Compatibility with GPL-3.0-only

| Licence | Components | Compatible |
|---|---|---|
| MIT | Avalonia, AvaloniaEdit, TreeSitter.DotNet, SkiaSharp, HarfBuzzSharp, MicroCom, tree-sitter, all grammars | ✅ |
| BSD-3-Clause | Skia (upstream C++) | ✅ |
| GPL-2.0-or-later | UTF.Unknown (chosen option) | ✅ — "or later" permits version 3 |

## UTF.Unknown

`UTF.Unknown` is a port of the Mozilla Universal Charset Detector. It is used in
[`TextEncodingDetector`](src/Skopos.Core/Files/TextEncodingDetector.cs) as step 3 of
encoding detection — the heuristic for 8-bit encodings.

Upstream offers it under **any one** of three licences: MPL-1.1, GPL-2.0-or-later or
LGPL-2.1-or-later. Skopos uses the **GPL-2.0-or-later** option, deliberately:

- **MPL-1.1 is incompatible with the GPL.** Taking the library under MPL-1.1 would make
  it impossible to distribute Skopos as a GPL-3.0 program.
- **GPL-2.0-or-later is compatible**, because "or later" allows it to be used under
  GPL-3.0, the licence of Skopos.

Under NativeAOT the library is not shipped as a separate DLL — it is compiled straight into
`skopos.exe`. With the GPL option that is unproblematic: the result is a single GPL-3.0
work. The library is used unmodified.

## Corresponding source

GPL-3.0 requires the complete corresponding source to be available for every binary
distributed.

- **Skopos itself:** the repository at the tag of that version.
- **Dependencies:** the versions pinned in `Directory.Packages.props` and the grammar commit
  pinned in `grammars/build-grammars.*`, from the upstream repositories linked above.
  GPL-3.0 §6(d) allows the source to be offered on a third-party server, provided clear
  directions to it accompany the binary — this file is those directions.

That permission comes with an obligation to keep the source available. **If an upstream
repository ever disappears, its source for the pinned version has to be mirrored** before
the next release.