$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $repoRoot 'TYBIM/obj/wall-region-tests'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$testExe = Join-Path $outputDir 'WallRegionGeometryTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /out:$testExe (Join-Path $repoRoot 'TYBIM/AutoBuild/WallRegionGeometry.cs') (Join-Path $PSScriptRoot 'WallRegionGeometryTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Geometry test compilation failed' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Geometry regression failed' }
