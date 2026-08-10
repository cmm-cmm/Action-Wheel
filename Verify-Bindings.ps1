<#
.SYNOPSIS
    Checks that every classic {Binding Path} in the XAML names a property that exists.

.DESCRIPTION
    The XAML compiler validates {x:Bind} but not {Binding}: a misspelled or renamed path produces an
    empty control or a dead button at runtime and nothing else. WinUI 3 offers no help either -
    DebugSettings.BindingFailed does not fire (measured: the flag reads back true, the handler
    attaches, and a deliberately broken path still raises nothing), so there is no runtime signal to
    watch for.

    This walks each XAML file, works out which type is the DataContext for each region, and checks
    the paths against the public members declared on that type. Regions are: inside a DataTemplate,
    the item type; everywhere else, the window's view model.

    Run it after renaming anything a view binds to. It is also a Build.ps1 task:
        .\Build.ps1 -Task VerifyBindings
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot

# XAML file -> which type backs which region. "" is the key for everything outside a DataTemplate;
# the other keys are DataTemplate x:Key values.
$scopes = @(
    @{
        Xaml = 'MainWindow.xaml'
        Regions = @{
            '' = @('ViewModels\MainViewModel.cs')
        }
    },
    @{
        Xaml = 'Settings\SettingsWindow.xaml'
        Regions = @{
            ''                    = @('ViewModels\SettingsViewModel*.cs')
            'ActionRowTemplate'   = @('Settings\ActionEditModel.cs')
            'GlyphChoiceTemplate' = @('Settings\GlyphChoice.cs')
            'FunctionSuggestionTemplate' = @('ActionWheel.Core\WindowsFunctions.cs')
        }
    }
)

# Public properties and fields declared in a C# file. Methods (an "(" after the name) and
# constructors are skipped - a binding path cannot reach either.
function Get-PublicMembers([string[]]$relativePaths) {
    $members = New-Object System.Collections.Generic.HashSet[string]

    foreach ($relative in $relativePaths) {
        # Wildcards, so a partial class split across more files does not silently shrink this type's
        # member list. That is exactly what happened when the ring-appearance region moved into
        # SettingsViewModel.Appearance.cs: eight perfectly good bindings were reported broken
        # because the entry here still named only two of the three files.
        $matched = @(Get-ChildItem -Path (Join-Path $root $relative) -File -ErrorAction SilentlyContinue)
        if ($matched.Count -eq 0) {
            throw "Verify-Bindings: $relative matched no file. Fix the scope map at the top of this script."
        }

        foreach ($line in ($matched | Get-Content)) {
            $match = [regex]::Match(
                $line,
                '^\s*public\s+(?:(?:static|virtual|override|sealed|required|readonly)\s+)*(?!class\b|record\b|enum\b|interface\b|struct\b|event\b)(?:[\w\.\<\>\?\[\],]+\s+)+(\w+)\s*(=>|\{|;|$)')

            if ($match.Success) {
                [void]$members.Add($match.Groups[1].Value)
            }
        }
    }

    return $members
}

# Which DataTemplate (if any) a given character offset sits inside. Templates here are not nested,
# so tracking one open template at a time is enough.
function Get-TemplateRegions([string]$xaml) {
    $regions = @()
    $pattern = '<DataTemplate\s+x:Key="(?<key>[^"]+)"'

    foreach ($open in [regex]::Matches($xaml, $pattern)) {
        $close = $xaml.IndexOf('</DataTemplate>', $open.Index)
        if ($close -lt 0) { continue }

        $regions += [pscustomobject]@{
            Key   = $open.Groups['key'].Value
            Start = $open.Index
            End   = $close
        }
    }

    return $regions
}

$problems = @()
$checked = 0

foreach ($scope in $scopes) {
    $xamlPath = Join-Path $root $scope.Xaml
    $xaml = Get-Content -LiteralPath $xamlPath -Raw
    $regions = Get-TemplateRegions $xaml

    $membersByRegion = @{}
    foreach ($key in $scope.Regions.Keys) {
        $membersByRegion[$key] = Get-PublicMembers $scope.Regions[$key]
    }

    # {Binding Foo}, {Binding Foo, Mode=OneWay}. A bare {Binding} binds the whole DataContext and
    # has no path to check.
    foreach ($binding in [regex]::Matches($xaml, '\{Binding\s+(?<path>[A-Za-z_][\w\.]*)')) {
        $path = $binding.Groups['path'].Value

        # Only the first segment can be checked against the declaring type; the rest would need the
        # property's own type, and nothing in this project binds deeper than one level.
        $head = $path.Split('.')[0]

        $regionKey = ''
        foreach ($region in $regions) {
            if ($binding.Index -gt $region.Start -and $binding.Index -lt $region.End) {
                $regionKey = $region.Key
                break
            }
        }

        if (-not $membersByRegion.ContainsKey($regionKey)) {
            $problems += "$($scope.Xaml): binding '$path' sits in template '$regionKey', which the scope map does not cover."
            continue
        }

        $checked++

        if (-not $membersByRegion[$regionKey].Contains($head)) {
            $where = if ($regionKey) { "template '$regionKey'" } else { 'the window' }
            $problems += "$($scope.Xaml): '$path' does not exist on the type backing $where."
        }
    }
}

if ($problems.Count -gt 0) {
    Write-Host ''
    foreach ($problem in $problems) {
        Write-Host "  $problem" -ForegroundColor Red
    }
    Write-Host ''
    throw "$($problems.Count) broken binding path(s)."
}

Write-Host "All $checked binding paths resolve." -ForegroundColor Green
