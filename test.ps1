$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet run --project .\tests\PoolDesk.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Offline tests failed' }
