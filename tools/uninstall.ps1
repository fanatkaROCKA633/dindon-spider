# Удаление паука Диндон: остановить, убрать ярлыки и папку программы.
# Настройки и лог (%LOCALAPPDATA%\DindonSpider\settings.txt, log.txt) остаются.
$ErrorActionPreference = 'Stop'

$appDir = Join-Path $env:LOCALAPPDATA 'DindonSpider\app'
$exe = Join-Path $appDir 'DindonSpider.exe'
$name = 'Паук Диндон.lnk'

Get-Process DindonSpider -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process
Start-Sleep -Milliseconds 300

foreach ($folder in 'Desktop', 'Programs', 'Startup') {
    $link = Join-Path ([Environment]::GetFolderPath($folder)) $name
    if (Test-Path $link) {
        Remove-Item $link
    }
}

# Удаляем только папку программы, которую создал install.ps1.
if (Test-Path $appDir) {
    Remove-Item $appDir -Recurse
}

Write-Host 'Паук Диндон удалён. Настройки и лог остались в %LOCALAPPDATA%\DindonSpider.' -ForegroundColor Green
