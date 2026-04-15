# Sync Mermaid from *.md -> _mmd/*.mmd, then render PNG + SVG via Kroki.
# Usage (from repo root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File WebThaoCamVien/docs/sequence-diagrams/render-diagrams-kroki.ps1
# Requires: network access to https://kroki.io

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$mmdDir = Join-Path $root '_mmd'
$imgDir = Join-Path $root 'images'
$pat = '```mermaid\s*(?<m>[\s\S]*?)\s*```'
$opt = [System.Text.RegularExpressions.RegexOptions]::Singleline

New-Item -ItemType Directory -Force -Path $mmdDir | Out-Null
New-Item -ItemType Directory -Force -Path $imgDir | Out-Null

$mdFiles = Get-ChildItem $root -Filter '*.md' | Where-Object { $_.Name -ne 'README.md' } | Sort-Object Name
foreach ($f in $mdFiles) {
  $text = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8
  $match = [regex]::Match($text, $pat, $opt)
  if (-not $match.Success) {
    Write-Host "SKIP (no mermaid block): $($f.Name)"
    continue
  }
  $mermaid = $match.Groups['m'].Value.Trim()
  $outMmd = Join-Path $mmdDir ($f.BaseName + '.mmd')
  Set-Content -LiteralPath $outMmd -Value $mermaid -Encoding UTF8
}

function Invoke-Kroki {
  param(
    [string]$Uri,
    [byte[]]$BodyBytes,
    [string]$OutFile,
    [int]$MaxAttempts = 5
  )
  for ($a = 1; $a -le $MaxAttempts; $a++) {
    try {
      Invoke-WebRequest -Uri $Uri -Method Post -Body $BodyBytes `
        -ContentType 'text/plain; charset=utf-8' `
        -OutFile $OutFile -UseBasicParsing | Out-Null
      return
    }
    catch {
      if ($a -eq $MaxAttempts) { throw }
      Start-Sleep -Seconds ([Math]::Min(30, 2 * $a))
    }
  }
}

$files = Get-ChildItem $mmdDir -Filter '*.mmd' | Sort-Object Name
if ($files.Count -eq 0) { throw "No .mmd files in $mmdDir" }

$fail = @()
foreach ($f in $files) {
  $diagram = (Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8).TrimEnd()
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($diagram)
  $base = $f.BaseName
  $png = Join-Path $imgDir ($base + '.png')
  $svg = Join-Path $imgDir ($base + '.svg')
  try {
    Invoke-Kroki -Uri 'https://kroki.io/mermaid/png' -BodyBytes $bytes -OutFile $png
    Invoke-Kroki -Uri 'https://kroki.io/mermaid/svg' -BodyBytes $bytes -OutFile $svg
    Write-Host "OK: $base"
  }
  catch {
    $fail += [pscustomobject]@{ File = $f.Name; Error = $_.Exception.Message }
    Write-Host "FAIL: $base - $($_.Exception.Message)"
  }
}

if ($fail.Count -gt 0) {
  Write-Host ''
  Write-Host '--- Failures ---'
  $fail | Format-Table -AutoSize | Out-String | Write-Host
  exit 1
}
