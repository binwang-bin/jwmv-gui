#Requires -Version 5.1
<#
.SYNOPSIS
  Publish jwmv CLI + GUI and build a Windows setup.exe (Inno Setup).

.EXAMPLE
  .\pack.ps1
  .\pack.ps1 -Version 0.1.0 -Configuration Release
#>
[CmdletBinding()]
param(
    [string] $Version = "0.1.0",
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [switch] $SkipPublish,
    [switch] $SkipInstaller
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Get-Location
}

$Artifacts = Join-Path $Root "artifacts"
$Payload = Join-Path $Artifacts "payload\$Runtime"
$InnoScript = Join-Path $Root "installer\jwmv.iss"
$IconIco = Join-Path $Root "icon.ico"
$IconPng = Join-Path $Root "icon.png"

function Write-Step([string] $Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Ensure-IconIco {
    if (Test-Path $IconIco) {
        return
    }
    if (-not (Test-Path $IconPng)) {
        throw "Missing icon.png / icon.ico at repo root."
    }

    Write-Step "Generating icon.ico from icon.png"
    Add-Type -AssemblyName System.Drawing
    $png = [System.Drawing.Image]::FromFile($IconPng)
    try {
        $bitmap = New-Object System.Drawing.Bitmap $png
        try {
            $iconHandle = $bitmap.GetHicon()
            $icon = [System.Drawing.Icon]::FromHandle($iconHandle)
            $fs = [System.IO.File]::Create($IconIco)
            try {
                $icon.Save($fs)
            }
            finally {
                $fs.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $png.Dispose()
    }
}

function Find-Iscc {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:LocalProgramFiles}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    return $null
}

function Install-InnoSetup {
    Write-Step "Inno Setup 6 not found — installing via winget"
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "Inno Setup 6 is required. Install it from https://jrsoftware.org/isinfo.php then re-run .\pack.ps1"
    }

    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements | Out-Host
    }
    finally {
        $ErrorActionPreference = $previousEap
    }

    $iscc = Find-Iscc
    if (-not $iscc) {
        throw "Inno Setup installed but ISCC.exe was not found. Re-open the terminal and run .\pack.ps1 again."
    }
    return $iscc
}

function Publish-Apps {
    Write-Step "Cleaning payload directory"
    if (Test-Path $Payload) {
        Remove-Item -Recurse -Force $Payload
    }
    New-Item -ItemType Directory -Force -Path $Payload | Out-Null

    $commonArgs = @(
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:Version=$Version",
        "-p:AssemblyVersion=$Version.0",
        "-p:FileVersion=$Version.0",
        "-o", $Payload
    )

    Write-Step "Publishing unified host (jwmv.exe = GUI + CLI)"
    $guiArgs = $commonArgs + @("-p:ApplicationIcon=$IconIco")
    & dotnet publish (Join-Path $Root "src\Jwmv.Gui\Jwmv.Gui.csproj") @guiArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $publishedGui = Join-Path $Payload "jwmv-gui.exe"
    $hostExe = Join-Path $Payload "jwmv.exe"
    if (-not (Test-Path $publishedGui)) {
        throw "Missing published file: $publishedGui"
    }

    if (Test-Path $hostExe) {
        Remove-Item -Force $hostExe
    }
    Rename-Item -Path $publishedGui -NewName "jwmv.exe"

    # Remove any leftover console-only CLI from older packs.
    Get-ChildItem -Path $Payload -Filter "Jwmv.Cli.*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

    Write-Host "Payload ready: $Payload (single jwmv.exe)"
}

function Build-Installer([string] $IsccPath) {
    Write-Step "Compiling installer with Inno Setup"
    New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null

    $payloadForIss = $Payload
    $outputForIss = $Artifacts
    & $IsccPath `
        "/DMyAppVersion=$Version" `
        "/DMyAppPayload=$payloadForIss" `
        "/DMyAppOutputDir=$outputForIss" `
        $InnoScript

    if ($LASTEXITCODE -ne 0) {
        throw "ISCC failed with exit code $LASTEXITCODE"
    }

    $setup = Join-Path $Artifacts "jwmv-setup-$Version-$Runtime.exe"
    if (-not (Test-Path $setup)) {
        # Inno may normalize runtime differently; pick newest matching setup.
        $setup = Get-ChildItem -Path $Artifacts -Filter "jwmv-setup-*.exe" |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }

    if (-not $setup -or -not (Test-Path $setup)) {
        throw "Installer exe was not produced under $Artifacts"
    }

    $hash = (Get-FileHash -Path $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path ($setup + ".sha256") -Value "$hash *$(Split-Path $setup -Leaf)" -NoNewline
    Write-Host ""
    Write-Host "Installer: $setup" -ForegroundColor Green
    Write-Host "SHA256:    $hash"
    Write-Host "Default install location: %LOCALAPPDATA%\Programs\jwmv"
    Write-Host "The wizard lets the user change the destination folder."
}

Ensure-IconIco

if (-not $SkipPublish) {
    Publish-Apps
}
elseif (-not (Test-Path (Join-Path $Payload "jwmv.exe"))) {
    throw "SkipPublish was set but payload\jwmv.exe is missing. Run without -SkipPublish first."
}

if (-not $SkipInstaller) {
    $iscc = Find-Iscc
    if (-not $iscc) {
        $iscc = Install-InnoSetup
    }
    else {
        Write-Step "Using Inno Setup: $iscc"
    }
    Build-Installer -IsccPath $iscc
}

Write-Step "Done"
