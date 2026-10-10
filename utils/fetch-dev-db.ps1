<#
.SYNOPSIS
Снимает полный дамп dev-базы kogda-igra с данными в db/local/snapshot.sql.

.DESCRIPTION
Для локального разглядывания реальных страниц. Схема и справочники для тестов сюда не
относятся: они живут в PHP-репозитории (db/init/) и попадают в тесты из образа сайта,
см. db/README.md.

Полный дамп содержит персональные данные (адреса почты в users и ki_games), поэтому
db/local/ заигнорен и его нельзя добавлять в репозиторий.

.NOTES
Пароль читается из переменной окружения KOGDA_DEV_PASSWORD, чтобы не попадать
в историю команд:

    $env:KOGDA_DEV_PASSWORD = '...'
    ./utils/fetch-dev-db.ps1

pg_dump запускается в контейнере, локальная установка Postgres не нужна.

Если pg_dump ругается на server version mismatch — передать -PostgresImage с нужной версией.
#>

[CmdletBinding()]
param(
    # Версия pg_dump не может быть ниже версии сервера. Dev/prod живут в Yandex Cloud
    # на PostgreSQL 15.18; если сервер поднимут — поднять и это значение.
    [string]$PostgresImage = 'postgres:15.18'
)

$ErrorActionPreference = 'Stop'

$dbHost = 'rc1b-1omkout6a9ifyold.mdb.yandexcloud.net'
$port = 6432
$database = 'kogda-dev'
$user = 'kogda-dev'

$password = $env:KOGDA_DEV_PASSWORD
if ([string]::IsNullOrWhiteSpace($password))
{
    throw 'Не задана переменная окружения KOGDA_DEV_PASSWORD. См. комментарий в начале скрипта.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$localDir = Join-Path $repoRoot 'db/local'
$outputName = 'snapshot.sql'
$outputPath = Join-Path $localDir $outputName

New-Item -ItemType Directory -Force -Path $localDir | Out-Null

$connection = "postgresql://${user}@${dbHost}:${port}/${database}?sslmode=require"

Write-Host 'Снимаю полный дамп с данными (в репозиторий не коммитится)...'

# pg_dump пишет прямо в примонтированный каталог, а не в stdout. Через конвейер
# PowerShell вывод пропускать нельзя: он декодирует его кодировкой консоли
# ([Console]::OutputEncoding), и на не-UTF-8 консоли кириллица превращается
# в псевдографику (UTF-8, прочитанный как CP866).
docker run --rm `
    -e "PGPASSWORD=$password" `
    -v "${localDir}:/out" `
    $PostgresImage `
    pg_dump $connection --no-owner --no-privileges --encoding=UTF8 `
    --file "/out/$outputName"

if ($LASTEXITCODE -ne 0)
{
    throw "pg_dump завершился с кодом $LASTEXITCODE"
}

# Страховка: убеждаемся, что в файле не мусор. Псевдографика в русском тексте —
# верный признак, что кодировка всё-таки поехала.
$content = [System.IO.File]::ReadAllText($outputPath, [System.Text.Encoding]::UTF8)
if ($content.Contains([char]0x2568) -or $content.Contains([char]0x2564))
{
    throw "В $outputName попала испорченная кодировка (псевдографика вместо кириллицы)."
}

Write-Host "Записан $outputPath"
