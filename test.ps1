$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /platform:x64 /out:Tests21.exe /main:Tests21 /win32icon:PoolDesk.ico /win32manifest:app.manifest /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll Tests21.cs Window.cs Pool.cs Theme.cs AssemblyInfo.cs Native.cs Ratings.cs Language.cs DarkForm.cs NoticeSound.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    Copy-Item PoolDesk.exe.config Tests21.exe.config
    & .\Tests21.exe --live
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} finally { Pop-Location }

