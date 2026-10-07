param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ReleaseNotes = "Mise a jour LT Services Presence."
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not [version]::TryParse($Version.Trim(), [ref]$null)) {
    throw "Version invalide: $Version (ex. 1.0.1)"
}

$v = [version]$Version.Trim()
$short = if ($v.Build -eq 0 -and $v.Revision -eq 0) { "$($v.Major).$($v.Minor)" } else { $Version.Trim() }
$tag = "v$($Version.Trim())"
$setupFile = "LT_Presence_Setup_$short.exe"
$repo = "Josueluyeye-dev/LT-Pr-sence"
$downloadUrl = "https://github.com/$repo/releases/download/$tag/$setupFile"

$csproj = Join-Path $root "MelodyPresence\MelodyPresence.csproj"
$c = Get-Content $csproj -Raw
$c = $c -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
Set-Content -Path $csproj -Value $c -Encoding UTF8

$iss = Join-Path $root "installer\LTPresence.iss"
$i = Get-Content $iss -Raw
$i = $i -replace '#define MyAppVersion "[^"]+"', "#define MyAppVersion `"$Version`""
$i = $i -replace '#define MyAppVersionShort "[^"]+"', "#define MyAppVersionShort `"$short`""
Set-Content -Path $iss -Value $i -Encoding UTF8

$manifest = @{
    version      = $Version.Trim()
    downloadUrl  = $downloadUrl
    fileName     = $setupFile
    publishedAt  = (Get-Date -Format "yyyy-MM-dd")
    releaseNotes = $ReleaseNotes.Trim()
    sha256       = ""
}
$manifestPath = Join-Path $root "installer\updates\version.json"
$manifest | ConvertTo-Json -Depth 3 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host "Version $Version (short=$short) -> $setupFile"
Write-Host "Tag attendu: $tag"
