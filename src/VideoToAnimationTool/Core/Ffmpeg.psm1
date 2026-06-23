$moduleRoot = Split-Path -Parent $PSScriptRoot
Import-Module -Name (Join-Path $PSScriptRoot 'PathUtils.psm1') -Force -Global

function Find-FfmpegExecutable {
    $localCandidates = @(
        (Join-Path $moduleRoot 'tools\ffmpeg\ffmpeg.exe'),
        (Join-Path (Split-Path -Parent $moduleRoot) 'tools\ffmpeg\ffmpeg.exe')
    )

    foreach ($candidate in $localCandidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    $command = Get-Command 'ffmpeg.exe' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    return $null
}

function New-FfmpegFrameExportArguments {
    param(
        [Parameter(Mandatory = $true)] [string] $InputPath,
        [Parameter(Mandatory = $true)] [string] $OutputFolder,
        [Parameter(Mandatory = $true)] [string] $CharacterName,
        [Parameter(Mandatory = $true)] [string] $ActionName,
        [Parameter(Mandatory = $true)] [double] $StartSeconds,
        [Parameter(Mandatory = $true)] [double] $EndSeconds,
        [Parameter(Mandatory = $true)] [int] $Fps,
        [Parameter(Mandatory = $true)] [ValidateSet('png', 'jpg', 'jpeg')] [string] $Format
    )

    $character = ConvertTo-SafeName $CharacterName
    $action = ConvertTo-SafeName $ActionName
    $extension = $Format.ToLowerInvariant()
    $outputPattern = Join-Path $OutputFolder ('{0}_{1}_%04d.{2}' -f $character, $action, $extension)

    return @(
        '-hide_banner',
        '-y',
        '-ss',
        ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.###}', $StartSeconds)),
        '-to',
        ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.###}', $EndSeconds)),
        '-i',
        $InputPath,
        '-vf',
        ('fps={0}' -f $Fps),
        $outputPattern
    )
}

Export-ModuleMember -Function Find-FfmpegExecutable, New-FfmpegFrameExportArguments
