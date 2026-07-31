<#
.SYNOPSIS
    Builds the native tree-sitter grammars that are not in the TreeSitter.DotNet package.

.DESCRIPTION
    TreeSitter.DotNet ships the runtime and the JSON grammar for every platform, but not
    markdown. Markdown is also split into two grammars (block and inline), so two libraries
    get compiled: tree-sitter-markdown and tree-sitter-markdown-inline.

    The sources are cloned at a pinned commit into grammars/vendor and compiled with MSVC
    (required workload: "Desktop development with C++"). Output goes to grammars/native/<RID>.

    The src/parser.c files are already generated in the repository, so neither the tree-sitter
    CLI nor Node.js is needed. The extensions (GFM, task lists, tables, YAML/TOML frontmatter)
    are baked into them in the default set — see grammars/README.md.

.PARAMETER Rid
    Target RID. Defaults to win-x64.

.PARAMETER Force
    Rebuild even when the libraries already exist.
#>
[CmdletBinding()]
param(
    [string]$Rid = 'win-x64',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$GrammarsDir = $PSScriptRoot
$VendorDir = Join-Path $GrammarsDir 'vendor'
$OutputDir = Join-Path $GrammarsDir "native\$Rid"

# Pinned commit, so highlighting cannot change under our hands between two builds.
$MarkdownRepo = 'https://github.com/MDeiml/tree-sitter-markdown'
$MarkdownBranch = 'split_parser'
$MarkdownCommit = 'a0a00f817d02412bd92c54d316f164d827b57b5c'

$targets = @(
    @{ Name = 'tree-sitter-markdown';        SubDir = 'tree-sitter-markdown' }
    @{ Name = 'tree-sitter-markdown-inline'; SubDir = 'tree-sitter-markdown-inline' }
)

if (-not $Force) {
    $missing = $targets | Where-Object { -not (Test-Path (Join-Path $OutputDir "$($_.Name).dll")) }
    if (-not $missing) {
        Write-Host "Grammars for $Rid already exist in $OutputDir (run with -Force to rebuild)."
        exit 0
    }
}

# --- sources --------------------------------------------------------------

$repoDir = Join-Path $VendorDir 'tree-sitter-markdown'
if (-not (Test-Path (Join-Path $repoDir '.git'))) {
    Write-Host "Cloning $MarkdownRepo ($MarkdownBranch) ..."
    New-Item -ItemType Directory -Force -Path $VendorDir | Out-Null
    git clone --branch $MarkdownBranch $MarkdownRepo $repoDir
    if ($LASTEXITCODE -ne 0) { throw 'git clone failed.' }
}

git -C $repoDir checkout --quiet $MarkdownCommit
if ($LASTEXITCODE -ne 0) { throw "Could not check out commit $MarkdownCommit." }

# --- compiler -------------------------------------------------------------

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw 'Could not find vswhere.exe - install Visual Studio or Build Tools with the "Desktop development with C++" workload.'
}

$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) {
    throw 'Could not find MSVC. Install the "Desktop development with C++" workload.'
}

$vcvarsName = switch ($Rid) {
    'win-x64'   { 'vcvars64.bat' }
    'win-arm64' { 'vcvarsamd64_arm64.bat' }
    default     { throw "Unknown RID '$Rid'. Supported: win-x64, win-arm64." }
}

$vcvars = Join-Path $vsPath "VC\Auxiliary\Build\$vcvarsName"
if (-not (Test-Path $vcvars)) { throw "Could not find $vcvars." }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# --- compile --------------------------------------------------------------

foreach ($target in $targets) {
    $srcDir = Join-Path $repoDir "$($target.SubDir)\src"
    if (-not (Test-Path (Join-Path $srcDir 'parser.c'))) {
        throw "Missing $srcDir\parser.c - the repository probably has no generated parser."
    }

    $objDir = Join-Path $OutputDir "obj\$($target.Name)"
    New-Item -ItemType Directory -Force -Path $objDir | Out-Null

    $dll = Join-Path $OutputDir "$($target.Name).dll"
    Write-Host "Compiling $($target.Name) -> $dll"

    # cl has to run from the environment vcvars sets up, hence going through cmd.
    $sources = @("$srcDir\parser.c")
    if (Test-Path "$srcDir\scanner.c") { $sources += "$srcDir\scanner.c" }

    $clArgs = '/nologo /LD /O2 /W0 /I"{0}" {1} /Fe:"{2}"' -f $srcDir, (($sources | ForEach-Object { "`"$_`"" }) -join ' '), $dll
    # vcvars calls vswhere without a full path, so we have to hand it one via PATH or it chokes.
    $installerDir = Split-Path $vswhere -Parent
    $command = 'set "PATH=%PATH%;{0}" && call "{1}" >nul && cd /d "{2}" && cl {3}' -f $installerDir, $vcvars, $objDir, $clArgs

    & cmd.exe /c $command
    if ($LASTEXITCODE -ne 0) { throw "Compiling $($target.Name) failed." }
}

Remove-Item (Join-Path $OutputDir 'obj') -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $OutputDir -Include '*.lib', '*.exp' -File | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host "Done. Libraries in $OutputDir :"
Get-ChildItem $OutputDir -Filter '*.dll' | ForEach-Object { Write-Host ("  {0}  {1:N0} kB" -f $_.Name, ($_.Length / 1kb)) }
