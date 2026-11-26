param([switch]$Installer, [string]$Compiler, [string]$CertificateThumbprint)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
function Sign-ReleaseFile([string]$Path) {
    if (-not $CertificateThumbprint) { return }
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object Thumbprint -EQ $CertificateThumbprint | Select-Object -First 1
    if (-not $cert -or -not $cert.HasPrivateKey) { throw 'Code-signing certificate with private key not found' }
    $signed = Set-AuthenticodeSignature -LiteralPath $Path -Certificate $cert -HashAlgorithm SHA256
    if ($signed.Status -ne 'Valid') { throw "Signature failed: $($signed.Status)" }
}
$publishRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts\portable'))
$allowedRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\'
if (-not $publishRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid publish path' }
if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
dotnet publish .\PoolDesk.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\portable
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Sign-ReleaseFile .\artifacts\portable\PoolDesk.exe
Copy-Item assets/PoolDesk.ico ./artifacts/portable/PoolDesk-1.1.2.ico
Copy-Item LICENSE,README.md,THIRD-PARTY-NOTICES.md .\artifacts\portable
New-Item -ItemType Directory -Force .\artifacts\portable\licenses\dotnet | Out-Null
Copy-Item third_party\licenses\* .\artifacts\portable\licenses -Recurse -Force
Copy-Item third_party\LICENSE-rosu-pp-ffi.txt .\artifacts\portable\licenses -Force
Copy-Item third_party\dotnet\* .\artifacts\portable\licenses\dotnet -Recurse -Force
Copy-Item docs .\artifacts\portable\docs -Recurse -Force
Compress-Archive -Path .\artifacts\portable\* -DestinationPath .\artifacts\PoolDesk-1.1.2-win-x64.zip -Force
if ($Installer) {
    if (-not $Compiler) { $Compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
    if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6, or pass -Compiler with the full path to ISCC.exe' }
    & $Compiler .\installer\PoolDesk.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    Sign-ReleaseFile .\artifacts\PoolDesk-1.1.2-Setup.exe
}
Get-ChildItem .\artifacts -File | Where-Object Extension -In '.zip','.exe' | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
} | Set-Content .\artifacts\SHA256SUMS.txt -Encoding ascii
