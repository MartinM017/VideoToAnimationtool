function ConvertTo-SafeName {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Name
    )

    $safe = $Name.Trim()
    $safe = $safe -replace '[^\p{L}\p{Nd}]+', '_'
    $safe = $safe.Trim('_')

    if ([string]::IsNullOrWhiteSpace($safe)) {
        return 'untitled'
    }

    return $safe
}

function New-FrameFileName {
    param(
        [Parameter(Mandatory = $true)] [string] $CharacterName,
        [Parameter(Mandatory = $true)] [string] $ActionName,
        [Parameter(Mandatory = $true)] [int] $Index,
        [Parameter(Mandatory = $true)] [ValidateSet('png', 'jpg', 'jpeg')] [string] $Extension
    )

    $character = ConvertTo-SafeName $CharacterName
    $action = ConvertTo-SafeName $ActionName
    $normalizedExtension = $Extension.TrimStart('.').ToLowerInvariant()

    return '{0}_{1}_{2:D4}.{3}' -f $character, $action, $Index, $normalizedExtension
}

function ConvertTo-NaturalSortKey {
    param([Parameter(Mandatory = $true)] [string] $Value)

    return [regex]::Replace($Value.ToLowerInvariant(), '\d+', {
        param($match)
        $match.Value.PadLeft(12, '0')
    })
}

function Get-FrameFiles {
    param(
        [Parameter(Mandatory = $true)] [string] $FolderPath
    )

    if (-not (Test-Path -LiteralPath $FolderPath -PathType Container)) {
        return @()
    }

    $extensions = @('.png', '.jpg', '.jpeg')
    return @(
        Get-ChildItem -LiteralPath $FolderPath -File |
            Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() } |
            Sort-Object @{ Expression = { ConvertTo-NaturalSortKey $_.Name } } |
            ForEach-Object { $_.FullName }
    )
}

Export-ModuleMember -Function ConvertTo-SafeName, New-FrameFileName, Get-FrameFiles
