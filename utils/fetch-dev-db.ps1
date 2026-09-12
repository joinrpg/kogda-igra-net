<#
.SYNOPSIS
Снимает схему и справочные данные с dev-базы kogda-igra.

.DESCRIPTION
Источник истины по схеме БД — живая dev-база PHP-версии, а не исторические MySQL-дампы
в PHP-репозитории. Скрипт забирает оттуда:

  db/schema/schema.sql          — структура (pg_dump --schema-only), коммитится;
  db/schema/reference-data.sql  — только справочники, коммитится;
  db/local/snapshot.sql         — полный дамп с данными, НЕ коммитится (см. -WithData).

Полный дамп содержит персональные данные (адреса почты в users и ki_games), поэтому
db/local/ заигнорен и его нельзя добавлять в репозиторий.

.PARAMETER WithData
Дополнительно снять полный дамп с данными в db/local/snapshot.sql — для локального
разглядывания реальных страниц. По умолчанию не снимается.

.NOTES
Пароль читается из переменной окружения KOGDA_DEV_PASSWORD, чтобы не попадать
в историю команд:

    $env:KOGDA_DEV_PASSWORD = '...'
    ./utils/fetch-dev-db.ps1

pg_dump запускается в контейнере, локальная установка Postgres не нужна.
#>

[CmdletBinding()]
param(
    [switch]$WithData
)

$ErrorActionPreference = 'Stop'

$dbHost = 'rc1b-1omkout6a9ifyold.mdb.yandexcloud.net'
$port = 6432
$database = 'kogda-dev'
$user = 'kogda-dev'

# Справочники: наполняются вручную и нужны сайту, чтобы вообще отрисовать страницу.
# Всё остальное — пользовательский контент, в репозиторий не едет.
$referenceTables = @(
    'ki_regions',
    'ki_sub_regions',
    'ki_game_types',
    'ki_status',
    'ki_update_types',
    'privs'
)

$password = $env:KOGDA_DEV_PASSWORD
if ([string]::IsNullOrWhiteSpace($password))
{
    throw 'Не задана переменная окружения KOGDA_DEV_PASSWORD. См. комментарий в начале скрипта.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$schemaDir = Join-Path $repoRoot 'db/schema'
$localDir = Join-Path $repoRoot 'db/local'

# Версия образа должна быть не ниже версии сервера, иначе pg_dump откажется работать.
$image = 'postgres:14.2'

function Invoke-PgDump([string[]]$DumpArguments, [string]$OutputPath)
{
    $connection = "postgresql://${user}@${dbHost}:${port}/${database}?sslmode=require"

    docker run --rm `
        -e "PGPASSWORD=$password" `
        $image `
        pg_dump $connection --no-owner --no-privileges $DumpArguments |
        Set-Content -Path $OutputPath -Encoding utf8

    if ($LASTEXITCODE -ne 0)
    {
        throw "pg_dump завершился с кодом $LASTEXITCODE"
    }

    Write-Host "Записан $OutputPath"
}

New-Item -ItemType Directory -Force -Path $schemaDir | Out-Null

Write-Host 'Снимаю схему...'
Invoke-PgDump @('--schema-only') (Join-Path $schemaDir 'schema.sql')

Write-Host 'Снимаю справочные данные...'
$tableArguments = $referenceTables | ForEach-Object { '--table'; $_ }
Invoke-PgDump (@('--data-only') + $tableArguments) (Join-Path $schemaDir 'reference-data.sql')

if ($WithData)
{
    New-Item -ItemType Directory -Force -Path $localDir | Out-Null
    Write-Host 'Снимаю полный дамп с данными (в репозиторий не коммитится)...'
    Invoke-PgDump @() (Join-Path $localDir 'snapshot.sql')
}

Write-Host 'Готово. Проверить применимость: dotnet test'
