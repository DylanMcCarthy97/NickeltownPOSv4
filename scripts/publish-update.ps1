param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $OutputDir = "c:\Users\dylan\source\repos\NickeltownPOSV4\deploy\out",

    [string] $ProjectPath = "c:\Users\dylan\source\repos\NickeltownPOSV4\NickeltownPOSV4\NickeltownPOSV4.csproj"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path (Split-Path $ProjectPath -Parent) -Parent
$manifestPath = Join-Path $repoRoot "NickeltownPOSV4\Package.appxmanifest"

Write-Host "Setting package version to $Version ..."
[xml]$xml = Get-Content $manifestPath
$xml.Package.Identity.Version = $Version
$xml.Save($manifestPath)

Write-Host "Publishing MSIX (Release, x64) ..."
dotnet publish $ProjectPath -c Release -p:Platform=x64 /p:PublishProfile=win-x64-msix

$msixDir = Join-Path $repoRoot "NickeltownPOSV4\AppPackages"
if (-not (Test-Path $msixDir)) {
    $msixDir = Join-Path $repoRoot "NickeltownPOSV4\bin\Release\net8.0-windows10.0.19041.0\win-x64\msix-publish"
}

$msix = Get-ChildItem -Path $msixDir -Recurse -Filter "*.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) {
    throw "No .msix found under $msixDir. Create App Packages in Visual Studio (Release, x64) if needed."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$destName = "NickeltownPOSV4_${Version}_x64.msix"
$destPath = Join-Path $OutputDir $destName
Copy-Item $msix.FullName $destPath -Force

$manifest = @{
    version      = $Version
    packageUri   = $destName
    releaseNotes = "Release $Version"
    mandatory    = $false
} | ConvertTo-Json

$manifest | Set-Content (Join-Path $OutputDir "update-manifest.json") -Encoding UTF8

Write-Host ""
Write-Host "Update feed ready:"
Write-Host "  $OutputDir"
Write-Host "  update-manifest.json"
Write-Host "  $destName"
Write-Host ""
Write-Host "Point kiosks to this folder in Admin -> Pit updates."
