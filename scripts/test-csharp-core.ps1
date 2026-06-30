$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $repoRoot 'build'
$outputPath = Join-Path $buildDir 'CSharpCoreTests.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw "C# compiler not found: $compiler"
}

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$sources = @()
$sources += Join-Path $repoRoot 'tests\CSharpCoreTests.cs'
$sources += Get-ChildItem -Path (Join-Path $repoRoot 'src\VideoToAnimationTool.Core') -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName }

& $compiler /nologo /target:exe /out:$outputPath $sources
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $outputPath
