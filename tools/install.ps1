# Установка паука Диндон: собрать из исходников в папку программы, сделать ярлыки, запустить.
# Обычно запускается через install.cmd в корне проекта.
param(
    [switch]$Autostart,    # запускать вместе с Windows, не спрашивая
    [switch]$NoAutostart,  # не запускать вместе с Windows, не спрашивая
    [switch]$NoLaunch      # не запускать паука после установки
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $env:LOCALAPPDATA 'DindonSpider\app'
$exe = Join-Path $appDir 'DindonSpider.exe'
$name = 'Паук Диндон.lnk'

Write-Host 'Паук Диндон — установка' -ForegroundColor Cyan

# 1. Нужен .NET 8 SDK: из него собирается exe. Свежий терминал может ещё не знать про dotnet — ищем и по пути установки.
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet -and (Test-Path "$env:ProgramFiles\dotnet\dotnet.exe")) { $dotnet = "$env:ProgramFiles\dotnet\dotnet.exe" }
$hasSdk = $dotnet -and ((& $dotnet --list-sdks 2>$null) -match '^8\.')
if (-not $hasSdk) {
    Write-Host 'Не найден .NET 8 SDK. Установите его командой:' -ForegroundColor Yellow
    Write-Host '    winget install Microsoft.DotNet.SDK.8'
    Write-Host 'и запустите install.cmd ещё раз.'
    exit 1
}

# 2. Если паук из папки программы уже запущен — останавливаем, иначе exe не перезаписать.
Get-Process DindonSpider -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process
Start-Sleep -Milliseconds 300

# 3. Сборка одного exe прямо в папку программы.
Write-Host 'Собираю паука (первый раз может занять пару минут)...'
& $dotnet publish (Join-Path $root 'src\SpiderWidget') -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -o $appDir --nologo -v quiet
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) {
    Write-Host 'Сборка не удалась — подробности выше.' -ForegroundColor Red
    exit 1
}

# 4. Ярлыки с иконкой паука: рабочий стол и меню Пуск; автозагрузка — по желанию.
function New-SpiderShortcut([string]$path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $appDir
    $link.IconLocation = "$exe,0"
    $link.Description = 'Пиксельный паук на рабочем столе'
    $link.Save()
}

New-SpiderShortcut (Join-Path ([Environment]::GetFolderPath('Desktop')) $name)
New-SpiderShortcut (Join-Path ([Environment]::GetFolderPath('Programs')) $name)

if (-not $Autostart -and -not $NoAutostart) {
    $answer = Read-Host 'Запускать паука вместе с Windows? (д/н)'
    $Autostart = $answer -match '^(д|да|y|yes)$'
}

$startupLink = Join-Path ([Environment]::GetFolderPath('Startup')) $name
if ($Autostart) {
    New-SpiderShortcut $startupLink
} elseif (Test-Path $startupLink) {
    Remove-Item $startupLink
}

Write-Host "Готово. Паук установлен в $appDir" -ForegroundColor Green
Write-Host 'Ярлык «Паук Диндон» — на рабочем столе и в меню Пуск.'
if ($Autostart) { Write-Host 'Паук будет запускаться вместе с Windows.' }

# 5. Запуск.
if (-not $NoLaunch) {
    Start-Process $exe
}
