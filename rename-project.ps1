#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$NewName,

    [Alias('y')]
    [switch]$Yes,

    [switch]$Force,

    [switch]$Help
)

$ErrorActionPreference = 'Stop'

$OldKebab = 'claude-starter'
$OldSnake = 'claude_starter'

function Show-Usage {
    @"
Usage: ./rename-project.ps1 <new-name> [-Yes] [-Force]

  <new-name>   kebab-case, ^[a-z][a-z0-9-]{1,49}`$
  -Yes, -y     skip confirmation prompt
  -Force       bypass template-state safety guard
"@ | Write-Error
    exit 1
}

if ($Help -or [string]::IsNullOrWhiteSpace($NewName)) { Show-Usage }

if ($NewName -cnotmatch '^[a-z][a-z0-9-]{1,49}$') {
    Write-Error "Name must be kebab-case, 2-50 chars, start with a letter (^[a-z][a-z0-9-]{1,49}`$)"
    exit 1
}

$NewSnake = $NewName.Replace('-', '_')
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# Safety guard
if (-not $Force) {
    $dir = Split-Path -Leaf (Get-Location)
    $remote = ''
    if (Test-Path .git) {
        $remote = & git config --get remote.origin.url 2>$null
        if (-not $remote) { $remote = '' }
    }
    if ($dir -ne $OldKebab -and $remote -notlike "*$OldKebab*") {
        Write-Error "This does not look like the $OldKebab template (dir=$dir, remote=$remote). Use -Force to override."
        exit 1
    }
}

# Skip set
$SkipDirs = @('.git', 'bin', 'obj', 'node_modules', 'dist', '.angular')

function Should-Skip($path) {
    foreach ($d in $SkipDirs) {
        if ($path -match "[\\/]$([regex]::Escape($d))([\\/]|$)") { return $true }
    }
    if ($path -like '*.log') { return $true }
    return $false
}

# Collect candidate files
$files = Get-ChildItem -Recurse -File -Force | Where-Object {
    -not (Should-Skip $_.FullName)
}

# Filter to those containing a placeholder (byte-level scan, BOM-safe)
$hits = @()
foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    # Skip likely-binary: contains NUL byte
    if ($bytes -contains 0) { continue }
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($text.Contains($OldKebab) -or $text.Contains($OldSnake)) {
        $hits += $f.FullName
    }
}

Write-Host "About to rename: $OldKebab -> $NewName (snake form: $NewSnake)"
Write-Host "  $($hits.Count) files will have content replaced"
Write-Host "  csproj/sln + 2 test project dirs will be renamed"
Write-Host "  README template block will be stripped"
Write-Host "  bin/ obj/ cleaned, .git/ removed, both rename scripts self-delete"

if (-not $Yes) {
    $ans = Read-Host "Continue? [y/N]"
    if ($ans -notmatch '^[Yy]$') {
        Write-Host "Aborted."
        exit 1
    }
}

# Content replace (preserve BOM/encoding by reading full bytes, replacing as UTF-8 string, writing bytes back)
foreach ($path in $hits) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    # Detect BOM
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $body = [System.Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
    $body = $body.Replace($OldKebab, $NewName).Replace($OldSnake, $NewSnake)
    $newBody = [System.Text.Encoding]::UTF8.GetBytes($body)
    if ($hasBom) {
        $out = New-Object byte[] ($newBody.Length + 3)
        $out[0] = 0xEF; $out[1] = 0xBB; $out[2] = 0xBF
        [Array]::Copy($newBody, 0, $out, 3, $newBody.Length)
        [System.IO.File]::WriteAllBytes($path, $out)
    }
    else {
        [System.IO.File]::WriteAllBytes($path, $newBody)
    }
}

# Strip template block from README
if (Test-Path README.md) {
    $readme = Get-Content README.md -Raw
    $readme = [regex]::Replace(
        $readme,
        '(?s)<!-- TEMPLATE:START -->.*?<!-- TEMPLATE:END -->\r?\n?',
        ''
    )
    [System.IO.File]::WriteAllText((Resolve-Path README.md), $readme, (New-Object System.Text.UTF8Encoding $false))
}

# Rename files
$renames = @(
    @{ Src = 'claude-starter.csproj'; Dst = "$NewName.csproj" },
    @{ Src = 'claude-starter.sln';    Dst = "$NewName.sln" },
    @{ Src = 'tests/claude-starter.UnitTests/claude-starter.UnitTests.csproj';
       Dst = "tests/claude-starter.UnitTests/$NewName.UnitTests.csproj" },
    @{ Src = 'tests/claude-starter.IntegrationTests/claude-starter.IntegrationTests.csproj';
       Dst = "tests/claude-starter.IntegrationTests/$NewName.IntegrationTests.csproj" }
)
foreach ($r in $renames) {
    if (Test-Path $r.Src) { Move-Item -LiteralPath $r.Src -Destination $r.Dst }
}

# Rename dirs
if (Test-Path 'tests/claude-starter.UnitTests') {
    Move-Item -LiteralPath 'tests/claude-starter.UnitTests' -Destination "tests/$NewName.UnitTests"
}
if (Test-Path 'tests/claude-starter.IntegrationTests') {
    Move-Item -LiteralPath 'tests/claude-starter.IntegrationTests' -Destination "tests/$NewName.IntegrationTests"
}

# Clean build artifacts
Get-ChildItem -Recurse -Directory -Force -Include 'bin', 'obj' |
    Where-Object { $_.FullName -notmatch '[\\/]node_modules[\\/]' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Remove git history
if (Test-Path .git) { Remove-Item .git -Recurse -Force }

# Self-delete
Remove-Item -Force -ErrorAction SilentlyContinue rename-project.sh, rename-project.ps1

Write-Host "Done. Project renamed to $NewName."
Write-Host "Next steps:"
Write-Host "  git init; git add -A; git commit -m 'Initial commit'"
Write-Host "  cd ClientApp; npm install"
Write-Host "  dotnet restore $NewName.sln"
