# Compila AudioSwitch con el compilador de C# que trae Windows (.NET Framework 4.x). No requiere instalar nada.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

$sources = (Get-ChildItem (Join-Path $root 'src') -Filter *.cs).FullName
& $csc /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu `
    "/out:$out\AudioSwitch.exe" "/win32manifest:$root\app.manifest" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw 'La compilación falló' }
Write-Host "OK -> $out\AudioSwitch.exe"
