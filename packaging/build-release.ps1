<#
.SYNOPSIS
    Builds the portable Skopos package for Windows.

.DESCRIPTION
    1. Compiles the native grammars that are not in the TreeSitter.DotNet package.
    2. Publishes a NativeAOT build.
    3. Assembles the portable ZIP — no symbols, with portable.txt.
    4. Prints the package size. It needs watching, because it grows with every
       language added; the target is 25-40 MB per platform.

    This script does not sign anything. Releases are unsigned for now, so Windows
    SmartScreen shows a warning on first run. See the Distribution section of ROADMAP.md.

.PARAMETER Rid
    Target RID, defaults to win-x64.
#>
[CmdletBinding()]
param(
    [string]$Rid = 'win-x64',
    [switch]$SkipGrammars
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path $PSScriptRoot -Parent
$Artifacts = Join-Path $RepoRoot 'artifacts'
$PublishDir = Join-Path $Artifacts "publish\$Rid"
$StageDir = Join-Path $Artifacts "stage\$Rid"

$version = (Select-Xml -Path (Join-Path $RepoRoot 'Directory.Build.props') -XPath '//Version').Node.InnerText
$zipPath = Join-Path $Artifacts "skopos-$version-$Rid.zip"

# --- 1) grammars ----------------------------------------------------------

if (-not $SkipGrammars) {
    & (Join-Path $RepoRoot 'grammars\build-grammars.ps1') -Rid $Rid
    if ($LASTEXITCODE -ne 0) { throw 'Building the grammars failed.' }
}

# --- 2) publish -----------------------------------------------------------

# vcvars inside ILCompiler calls vswhere without a full path; without this the linker fails.
$installerDir = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path $installerDir) { $env:PATH = "$env:PATH;$installerDir" }

Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $RepoRoot 'src\Skopos\Skopos.csproj') `
    -c Release -r $Rid -p:PublishAot=true -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

# --- 3) staging -----------------------------------------------------------

Remove-Item $StageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $StageDir | Out-Null

# Symbols do not belong in the distribution. The native PDB is over 100 MB and the linker
# writes it straight into the publish output, so it gets filtered out here.
Get-ChildItem $PublishDir -Recurse -File |
    Where-Object { $_.Extension -notin '.pdb', '.lib', '.exp' } |
    ForEach-Object {
        $target = Join-Path $StageDir $_.FullName.Substring($PublishDir.Length + 1)
        New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
        Copy-Item $_.FullName $target
    }

foreach ($doc in 'README.md', 'CHANGELOG.md', 'THIRD-PARTY-LICENSES.md', 'LICENSE') {
    $source = Join-Path $RepoRoot $doc
    if (Test-Path $source) { Copy-Item $source $StageDir }
}

# An empty portable.txt next to the exe keeps settings with the application.
New-Item -ItemType File -Path (Join-Path $StageDir 'portable.txt') -Force | Out-Null

# --- 4) ZIP and size ------------------------------------------------------

Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $StageDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

$unpackedMb = [math]::Round(((Get-ChildItem $StageDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
$zipMb = [math]::Round(((Get-Item $zipPath).Length / 1MB), 1)

Write-Host ''
Write-Host "Package:     $zipPath"
Write-Host "Unpacked:    $unpackedMb MB"
Write-Host "ZIP:         $zipMb MB"
Write-Host ''
Get-ChildItem $StageDir -Recurse -File |
    Sort-Object Length -Descending | Select-Object -First 10 |
    ForEach-Object { Write-Host ("  {0,-34} {1,7:N1} MB" -f $_.Name, ($_.Length / 1MB)) }

# The target is 25-40 MB per platform. Over 60 MB means something slipped into the
# package — typically grammars we do not use.
if ($unpackedMb -gt 60) {
    Write-Warning "The package is $unpackedMb MB, above the 25-40 MB target. Check what got added."
}
