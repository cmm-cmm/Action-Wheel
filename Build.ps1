<#
.SYNOPSIS
    The one build script for Action Wheel: restore, build, publish, package, clean.

.DESCRIPTION
    Replaces Build-Publish.ps1 and Build-Quick.bat, which disagreed with each other and with the
    README about which flags a publish needs.

    Nothing here is interactive - no prompts, no "press any key" - so the same commands run on a
    developer's machine and on a CI runner. Every task exits non-zero on failure.

.PARAMETER Task
    Restore         - restore NuGet packages only
    Debug           - build the Debug configuration (the default)
    Release         - build the Release configuration
    Publish         - self-contained build (folder, not single-file - see the csproj's Publish-only
                      PropertyGroup for why) in bin\Publish\win-<platform>
    Installer       - Publish, then build the Inno Setup installer from ActionWheel-Setup.iss
    VerifyBindings  - check every classic {Binding} path against the type behind it
    Clean           - delete bin\ and obj\
    All             - Restore, VerifyBindings, Debug, Release, Publish

.PARAMETER Platform
    x64 (default), x86 or ARM64. Sets both the MSBuild platform and the runtime identifier.

.EXAMPLE
    .\Build.ps1
    .\Build.ps1 -Task Publish
    .\Build.ps1 -Task Publish -Platform ARM64
#>

[CmdletBinding()]
param(
    [ValidateSet('Restore', 'Debug', 'Release', 'Publish', 'Installer', 'VerifyBindings', 'Clean', 'All')]
    [string]$Task = 'Debug',

    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64',

    [string]$Output
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'Action Wheel.csproj'
$core = Join-Path $root 'ActionWheel.Core\ActionWheel.Core.csproj'
$rid = 'win-' + $Platform.ToLowerInvariant()

if (-not $Output) {
    $Output = Join-Path $root "bin\Publish\$rid"
}

function Write-Step([string]$text) {
    Write-Host ''
    Write-Host "==> $text" -ForegroundColor Cyan
}

# A running copy holds "Action Wheel.exe" open and the build fails deep inside MSBuild with
# MSB3026/MSB3027 - a copy error that says nothing about the app being open. Say it up front.
function Assert-NotRunning {
    $running = Get-Process -Name 'Action Wheel' -ErrorAction SilentlyContinue
    if ($running) {
        throw "Action Wheel is running (PID $($running.Id -join ', ')). Quit it from the tray icon, then build again."
    }
}

function Invoke-Dotnet([string[]]$dotnetArgs) {
    Write-Host "dotnet $($dotnetArgs -join ' ')" -ForegroundColor DarkGray
    & dotnet @dotnetArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($dotnetArgs[0]) failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Restore {
    Write-Step 'Restoring packages'
    Invoke-Dotnet @('restore', $project, '-r', $rid, "-p:Platform=$Platform")
}

function Invoke-Build([string]$configuration) {
    Write-Step "Building $configuration | $Platform"
    Assert-NotRunning

    # The core library is built as a project reference, but building it on its own first turns a
    # business-logic compile error into a two-line message instead of one buried under XAML output.
    Invoke-Dotnet @('build', $core, '-c', $configuration, '--nologo')
    Invoke-Dotnet @('build', $project, '-c', $configuration, '-r', $rid, "-p:Platform=$Platform", '--nologo')
}

function Invoke-Publish {
    Write-Step "Publishing Release | $rid"
    Assert-NotRunning

    if (Test-Path $Output) {
        Write-Host "Clearing $Output" -ForegroundColor DarkGray
        Remove-Item -LiteralPath $Output -Recurse -Force
    }

    # PublishTrimmed stays false on purpose: the settings window's row templates use classic
    # {Binding}, which resolves properties by reflection. Trimming would leave the rows blank.
    #
    # No -p:PublishSingleFile here - see the csproj's Publish-only PropertyGroup. This has to stay
    # a folder, not a single exe.
    Invoke-Dotnet @(
        'publish', $project,
        '-c', 'Release',
        '-r', $rid,
        "-p:Platform=$Platform",
        '--self-contained', 'true',
        '-p:PublishReadyToRun=true',
        '-p:PublishTrimmed=false',
        '-o', $Output,
        '--nologo'
    )

    $exe = Join-Path $Output 'Action Wheel.exe'
    if (Test-Path $exe) {
        $size = [math]::Round(
            ((Get-ChildItem -LiteralPath $Output -Recurse -File | Measure-Object -Property Length -Sum).Sum) / 1MB, 1)
        Write-Host ''
        Write-Host "Action Wheel.exe + dependencies  $size MB total" -ForegroundColor Green
        Write-Host $Output -ForegroundColor Green
    }
}

function Invoke-Installer {
    Invoke-Publish

    Write-Step 'Building the installer'

    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) {
        throw 'Inno Setup 6 was not found. Install it, or run -Task Publish for the portable exe.'
    }

    & $iscc (Join-Path $root 'ActionWheel-Setup.iss')
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC failed with exit code $LASTEXITCODE."
    }
}

function Invoke-VerifyBindings {
    Write-Step 'Verifying binding paths'
    & (Join-Path $root 'Verify-Bindings.ps1')
}

function Invoke-Clean {
    Write-Step 'Cleaning'
    Assert-NotRunning

    foreach ($directory in @('bin', 'obj', 'ActionWheel.Core\bin', 'ActionWheel.Core\obj')) {
        $path = Join-Path $root $directory
        if (Test-Path $path) {
            Write-Host "Removing $directory" -ForegroundColor DarkGray
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

switch ($Task) {
    'Restore' { Invoke-Restore }
    'Debug' { Invoke-Build 'Debug' }
    'Release' { Invoke-Build 'Release' }
    'Publish' { Invoke-Publish }
    'Installer' { Invoke-Installer }
    'VerifyBindings' { Invoke-VerifyBindings }
    'Clean' { Invoke-Clean }
    'All' {
        Invoke-Restore
        Invoke-VerifyBindings
        Invoke-Build 'Debug'
        Invoke-Build 'Release'
        Invoke-Publish
    }
}

Write-Host ''
Write-Host "$Task completed." -ForegroundColor Green
