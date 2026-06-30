$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $repoRoot 'build'
$outputPath = Join-Path $buildDir 'VideoToAnimationTool.exe'
$rootOutputPath = Join-Path $repoRoot 'VideoToAnimationTool.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "C# compiler not found: $compiler"
}

function Get-AssemblyPath {
    param(
        [Parameter(Mandatory = $true)] [string] $Name,
        [string] $PreferredSegment
    )

    $matches = @(Get-ChildItem 'C:\Windows\Microsoft.NET\assembly' -Recurse -Filter $Name -ErrorAction SilentlyContinue)
    if ($PreferredSegment) {
        $preferred = @($matches | Where-Object { $_.FullName -like "*$PreferredSegment*" })
        if ($preferred.Count -gt 0) {
            return $preferred[0].FullName
        }
    }

    if ($matches.Count -eq 0) {
        throw "Assembly not found in GAC: $Name"
    }

    return $matches[0].FullName
}

$presentationFramework = Get-AssemblyPath -Name 'PresentationFramework.dll'
$presentationCore = Get-AssemblyPath -Name 'PresentationCore.dll' -PreferredSegment 'GAC_64'
$windowsBase = Get-AssemblyPath -Name 'WindowsBase.dll'
$systemXaml = Get-AssemblyPath -Name 'System.Xaml.dll'

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$sources = @()
$sources += Get-ChildItem -Path (Join-Path $repoRoot 'src\VideoToAnimationTool.Core') -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName }
$sources += Get-ChildItem -Path (Join-Path $repoRoot 'src\VideoToAnimationTool.Desktop') -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName }
$sources += Join-Path $repoRoot 'VideoToAnimationToolDesktop.cs'

& $compiler `
    /nologo `
    /target:winexe `
    /out:$outputPath `
    /reference:$presentationFramework `
    /reference:$presentationCore `
    /reference:$windowsBase `
    /reference:$systemXaml `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Built $outputPath"
Copy-Item -LiteralPath $outputPath -Destination $rootOutputPath -Force
Write-Host "Updated $rootOutputPath"
