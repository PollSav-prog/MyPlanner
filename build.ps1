# Локальная сборка установщика на Windows.
# Нужны: .NET SDK 10 (https://dotnet.microsoft.com/download) и Inno Setup 6 (https://jrsoftware.org/isdl.php)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet test tests\MyPlanner.Tests\MyPlanner.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Тесты не прошли" }

if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish src\MyPlanner\MyPlanner.csproj -c Release -r win-x64 --self-contained true -o publish
if ($LASTEXITCODE -ne 0) { throw "Публикация не удалась" }

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { throw "Inno Setup 6 не найден. Установите его и запустите скрипт снова." }
& $iscc installer\MyPlanner.iss
Write-Host "Готово: installer\Output\MyPlanner-Setup-1.0.0.exe"
