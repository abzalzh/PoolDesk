$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /out:PoolDesk.exe /win32icon:PoolDesk.ico /win32manifest:app.manifest /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll Window.cs Pool.cs Theme.cs AssemblyInfo.cs Native.cs Ratings.cs Language.cs DarkForm.cs NoticeSound.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
} finally { Pop-Location }

