$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $repoRoot 'build'
$outputPath = Join-Path $buildDir 'CSharpCoreTests.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "C# compiler not found: $compiler"
}

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$sources = @(
    'tests\CSharpCoreTests.cs',
    'src\VideoToAnimationTool.CSharp\Core\PathUtils.cs',
    'src\VideoToAnimationTool.CSharp\Core\ExportOptionsValidator.cs',
    'src\VideoToAnimationTool.CSharp\Core\FfmpegHelper.cs'
) | ForEach-Object { Join-Path $repoRoot $_ }

& $compiler /nologo /target:exe /out:$outputPath $sources
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $outputPath
