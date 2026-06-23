$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$coreRoot = Join-Path $repoRoot 'src\VideoToAnimationTool\Core'

Import-Module -Name (Join-Path $coreRoot 'PathUtils.psm1') -Force -Global
Import-Module -Name (Join-Path $coreRoot 'Validation.psm1') -Force -Global
Import-Module -Name (Join-Path $coreRoot 'Ffmpeg.psm1') -Force -Global

foreach ($requiredCommand in @('ConvertTo-SafeName', 'Test-FrameExportOptions', 'New-FfmpegFrameExportArguments')) {
    if (-not (Get-Command $requiredCommand -ErrorAction SilentlyContinue)) {
        throw "Required test command was not imported: $requiredCommand"
    }
}

$script:Failures = 0

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)] $Actual,
        [Parameter(Mandatory = $true)] $Expected,
        [Parameter(Mandatory = $true)] [string] $Name
    )

    if ($Actual -ne $Expected) {
        $script:Failures++
        Write-Host "FAIL: $Name" -ForegroundColor Red
        Write-Host "  Expected: $Expected" -ForegroundColor Red
        Write-Host "  Actual:   $Actual" -ForegroundColor Red
    }
    else {
        Write-Host "PASS: $Name" -ForegroundColor Green
    }
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)] [bool] $Condition,
        [Parameter(Mandatory = $true)] [string] $Name
    )

    if (-not $Condition) {
        $script:Failures++
        Write-Host "FAIL: $Name" -ForegroundColor Red
    }
    else {
        Write-Host "PASS: $Name" -ForegroundColor Green
    }
}

function Assert-False {
    param(
        [Parameter(Mandatory = $true)] [bool] $Condition,
        [Parameter(Mandatory = $true)] [string] $Name
    )

    Assert-True -Condition (-not $Condition) -Name $Name
}

function New-TestFile {
    param([Parameter(Mandatory = $true)] [string] $Path)
    New-Item -ItemType File -Path $Path -Force | Out-Null
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('vta-core-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    Assert-Equal -Actual (ConvertTo-SafeName 'Hero Girl!*') -Expected 'Hero_Girl' -Name 'sanitizes display names for filenames'
    Assert-Equal -Actual (ConvertTo-SafeName '   ') -Expected 'untitled' -Name 'uses fallback for empty safe names'

    Assert-Equal `
        -Actual (New-FrameFileName -CharacterName 'Hero' -ActionName 'Run' -Index 7 -Extension 'png') `
        -Expected 'Hero_Run_0007.png' `
        -Name 'builds zero-padded frame filenames'

    $videoPath = Join-Path $tempRoot 'source.mp4'
    $outputPath = Join-Path $tempRoot 'out'
    New-TestFile $videoPath
    New-Item -ItemType Directory -Path $outputPath | Out-Null

    $valid = Test-FrameExportOptions `
        -InputPath $videoPath `
        -OutputFolder $outputPath `
        -StartSeconds 0 `
        -EndSeconds 2.5 `
        -Fps 12 `
        -Format 'png'

    Assert-True -Condition $valid.IsValid -Name 'accepts valid export options'

    $invalidFps = Test-FrameExportOptions `
        -InputPath $videoPath `
        -OutputFolder $outputPath `
        -StartSeconds 0 `
        -EndSeconds 2.5 `
        -Fps 0 `
        -Format 'png'

    Assert-False -Condition $invalidFps.IsValid -Name 'rejects zero FPS'
    Assert-True -Condition ($invalidFps.Errors -contains 'FPS must be between 1 and 120.') -Name 'returns clear FPS validation error'

    $frameFolder = Join-Path $tempRoot 'frames'
    New-Item -ItemType Directory -Path $frameFolder | Out-Null
    New-TestFile (Join-Path $frameFolder 'walk_10.png')
    New-TestFile (Join-Path $frameFolder 'walk_1.png')
    New-TestFile (Join-Path $frameFolder 'walk_2.png')
    New-TestFile (Join-Path $frameFolder 'notes.txt')

    $frames = Get-FrameFiles -FolderPath $frameFolder
    Assert-Equal -Actual ([System.IO.Path]::GetFileName($frames[0])) -Expected 'walk_1.png' -Name 'natural sort first frame'
    Assert-Equal -Actual ([System.IO.Path]::GetFileName($frames[1])) -Expected 'walk_2.png' -Name 'natural sort second frame'
    Assert-Equal -Actual ([System.IO.Path]::GetFileName($frames[2])) -Expected 'walk_10.png' -Name 'natural sort tenth frame'

    $args = New-FfmpegFrameExportArguments `
        -InputPath 'C:\clips\hero run.mp4' `
        -OutputFolder 'C:\frames' `
        -CharacterName 'Hero' `
        -ActionName 'Run' `
        -StartSeconds 1.25 `
        -EndSeconds 3.5 `
        -Fps 12 `
        -Format 'png'

    Assert-True -Condition ($args -contains '-ss') -Name 'FFmpeg args include start seek flag'
    Assert-True -Condition ($args -contains '-to') -Name 'FFmpeg args include end flag'
    Assert-True -Condition ($args -contains '-i') -Name 'FFmpeg args include input flag'
    Assert-True -Condition ($args -contains '-vf') -Name 'FFmpeg args include filter flag'
    Assert-True -Condition ($args -contains 'fps=12') -Name 'FFmpeg args include FPS filter'
    Assert-Equal -Actual $args[-1] -Expected 'C:\frames\Hero_Run_%04d.png' -Name 'FFmpeg args end with output pattern'
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

if ($script:Failures -gt 0) {
    Write-Host "$script:Failures test(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'All core tests passed.' -ForegroundColor Green
