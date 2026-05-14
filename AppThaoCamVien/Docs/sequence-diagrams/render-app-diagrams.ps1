# Render Mermaid diagrams via Kroki for AppThaoCamVien
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File AppThaoCamVien/Docs/sequence-diagrams/render-app-diagrams.ps1
# Or render specific file:
#   powershell -NoProfile -ExecutionPolicy Bypass -File AppThaoCamVien/Docs/sequence-diagrams/render-app-diagrams.ps1 -Files "11-app-qr-queue-flow"

param(
    [string[]]$Files = @()
)

$ErrorActionPreference = 'Stop'
$root = "D:\ThaoCamVien\ThaoCamVien\AppThaoCamVien\Docs\sequence-diagrams"
$mmdDir = Join-Path $root '_mmd'
$imgDir = Join-Path $root 'images'

New-Item -ItemType Directory -Force -Path $imgDir | Out-Null

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

if ($Files.Count -gt 0) {
    $files = Get-ChildItem $mmdDir -Filter '*.mmd' | Where-Object { $Files -contains $_.Name } | Sort-Object Name
}
else {
    $files = Get-ChildItem $mmdDir -Filter '*.mmd' | Sort-Object Name
}

if ($files.Count -eq 0) { throw "No .mmd files found in $mmdDir" }

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
        Write-Host "OK: $base" -ForegroundColor Green
    }
    catch {
        $fail += [pscustomobject]@{ File = $f.Name; Error = $_.Exception.Message }
        Write-Host "FAIL: $base - $($_.Exception.Message)" -ForegroundColor Red
    }
}

if ($fail.Count -gt 0) {
    Write-Host ''
    Write-Host '--- Failures ---'
    $fail | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}
else {
    Write-Host ''
    Write-Host "Rendered $($files.Count) diagram(s) successfully!" -ForegroundColor Cyan
}
