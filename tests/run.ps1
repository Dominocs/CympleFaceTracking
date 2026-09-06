param(
    [string]$CympleSource = (Join-Path $PSScriptRoot '..\..\Cymple')
)

$ErrorActionPreference = 'Stop'
& dotnet build (Join-Path $PSScriptRoot '..\CympleFaceTracking.csproj') -c Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet run --project (Join-Path $PSScriptRoot 'CympleFaceTracking.RegressionTests.csproj') -c Release -- $CympleSource
exit $LASTEXITCODE
