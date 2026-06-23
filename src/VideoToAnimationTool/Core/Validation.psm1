function Test-FrameExportOptions {
    param(
        [Parameter(Mandatory = $true)] [string] $InputPath,
        [Parameter(Mandatory = $true)] [string] $OutputFolder,
        [Parameter(Mandatory = $true)] [double] $StartSeconds,
        [Parameter(Mandatory = $true)] [double] $EndSeconds,
        [Parameter(Mandatory = $true)] [int] $Fps,
        [Parameter(Mandatory = $true)] [string] $Format
    )

    $errors = New-Object System.Collections.Generic.List[string]

    if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
        $errors.Add('Input video does not exist.')
    }

    if (-not (Test-Path -LiteralPath $OutputFolder -PathType Container)) {
        $errors.Add('Output folder does not exist.')
    }

    if ($StartSeconds -lt 0) {
        $errors.Add('Start time cannot be negative.')
    }

    if ($EndSeconds -le $StartSeconds) {
        $errors.Add('End time must be greater than start time.')
    }

    if ($Fps -lt 1 -or $Fps -gt 120) {
        $errors.Add('FPS must be between 1 and 120.')
    }

    if (@('png', 'jpg', 'jpeg') -notcontains $Format.ToLowerInvariant()) {
        $errors.Add('Output format must be png, jpg, or jpeg.')
    }

    [pscustomobject]@{
        IsValid = $errors.Count -eq 0
        Errors = @($errors)
    }
}

Export-ModuleMember -Function Test-FrameExportOptions
