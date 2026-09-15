<#
.SYNOPSIS
Сливает сырые дампы покрытия PHP в отчёт.

.DESCRIPTION
Образ из coverage/Dockerfile складывает по файлу на каждый HTTP-запрос. Скрипт
объединяет их, переводит пути контейнера в пути PHP-репозитория и печатает сводку,
а также пишет lcov-файл для внешних инструментов.

Формат Xdebug: { "<файл>": { "<строка>": <статус> } }, где
   1 — строка выполнялась,
  -1 — строка исполняемая, но не выполнялась,
  -2 — недостижимый код.

.PARAMETER DataDirectory
Каталог с сырыми дампами — тот же, что передавался в COMPAT_COVERAGE_DIR.

.PARAMETER OutputFile
Куда записать lcov. По умолчанию coverage/lcov.info рядом со скриптом.

.PARAMETER Top
Сколько строк показать в сводке худших файлов.

.EXAMPLE
./coverage/report.ps1 -DataDirectory ./artifacts/coverage-data
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DataDirectory,

    [string]$OutputFile = (Join-Path $PSScriptRoot 'lcov.info'),

    [int]$Top = 15,

    # Каталог public_html PHP-репозитория. Если указан, скрипт дополнительно покажет
    # файлы, которые не подключались ни разу: в данные Xdebug они не попадают вовсе,
    # поэтому в проценте выше их нет, и картина без них выглядит лучше, чем есть.
    [string]$SourceDirectory
)

$ErrorActionPreference = 'Stop'

$dumps = Get-ChildItem -Path $DataDirectory -Filter '*.json' -File
if ($dumps.Count -eq 0)
{
    throw "В $DataDirectory нет ни одного дампа. Прогон шёл с COMPAT_COVERAGE_DIR и образом с Xdebug?"
}

Write-Host "Дампов: $($dumps.Count)"

# файл -> (строка -> был ли выполнен)
$merged = @{}

foreach ($dump in $dumps)
{
    $content = Get-Content -LiteralPath $dump.FullName -Raw
    if ([string]::IsNullOrWhiteSpace($content)) { continue }

    $data = $content | ConvertFrom-Json -AsHashtable

    foreach ($file in $data.Keys)
    {
        # Интересует только код приложения; библиотеки PHP и сам сборщик — нет.
        if (-not $file.StartsWith('/var/www/html/')) { continue }

        if (-not $merged.ContainsKey($file)) { $merged[$file] = @{} }

        foreach ($line in $data[$file].Keys)
        {
            $status = [int]$data[$file][$line]
            # -2 это недостижимый код: в знаменатель он не идёт.
            if ($status -eq -2) { continue }

            $lineNumber = [int]$line
            $wasHit = $status -eq 1

            if ($merged[$file].ContainsKey($lineNumber))
            {
                $merged[$file][$lineNumber] = $merged[$file][$lineNumber] -or $wasHit
            }
            else
            {
                $merged[$file][$lineNumber] = $wasHit
            }
        }
    }
}

$rows = foreach ($file in $merged.Keys)
{
    $lines = $merged[$file]
    $found = $lines.Count
    $hit = ($lines.Values | Where-Object { $_ }).Count

    [pscustomobject]@{
        # Путь в контейнере -> путь в PHP-репозитории.
        File    = $file -replace '^/var/www/html/', 'public_html/'
        Found   = $found
        Hit     = $hit
        Percent = if ($found -gt 0) { [math]::Round(100.0 * $hit / $found, 1) } else { 0 }
    }
}

$totalFound = ($rows | Measure-Object -Property Found -Sum).Sum
$totalHit = ($rows | Measure-Object -Property Hit -Sum).Sum
$totalPercent = if ($totalFound -gt 0) { [math]::Round(100.0 * $totalHit / $totalFound, 1) } else { 0 }

Write-Host ''
Write-Host "Покрытие строк: $totalHit из $totalFound ($totalPercent%) в $($rows.Count) файлах"
Write-Host ''
Write-Host "Наименее покрытые файлы (топ $Top):"

$rows |
    Sort-Object Percent, @{ Expression = 'Found'; Descending = $true } |
    Select-Object -First $Top |
    Format-Table @{ Label = 'Покрыто'; Expression = { '{0,5:N1}%' -f $_.Percent } },
                 @{ Label = 'Строк'; Expression = { '{0,5}' -f $_.Found } },
                 @{ Label = 'Файл'; Expression = { $_.File } } -AutoSize |
    Out-String -Width 200 |
    Write-Host

# lcov для внешних инструментов
$lcov = [System.Text.StringBuilder]::new()
foreach ($file in ($merged.Keys | Sort-Object))
{
    $relative = $file -replace '^/var/www/html/', 'public_html/'
    [void]$lcov.AppendLine("SF:$relative")

    $lines = $merged[$file]
    foreach ($lineNumber in ($lines.Keys | Sort-Object))
    {
        $count = if ($lines[$lineNumber]) { 1 } else { 0 }
        [void]$lcov.AppendLine("DA:$lineNumber,$count")
    }

    [void]$lcov.AppendLine("LF:$($lines.Count)")
    [void]$lcov.AppendLine("LH:$(($lines.Values | Where-Object { $_ }).Count)")
    [void]$lcov.AppendLine('end_of_record')
}

[System.IO.File]::WriteAllText($OutputFile, $lcov.ToString())
Write-Host "lcov записан: $OutputFile"

if ($SourceDirectory)
{
    $touched = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($file in $merged.Keys)
    {
        [void]$touched.Add(($file -replace '^/var/www/html/', ''))
    }

    $root = (Resolve-Path $SourceDirectory).Path
    $never = Get-ChildItem -Path $root -Filter '*.php' -Recurse -File |
        ForEach-Object { $_.FullName.Substring($root.Length).TrimStart([char]92, [char]47).Replace([char]92, [char]47) } |
        Where-Object { -not $touched.Contains($_) } |
        Sort-Object

    Write-Host ''
    if ($never.Count -eq 0)
    {
        Write-Host 'Все PHP-файлы хотя бы раз подключались.'
    }
    else
    {
        Write-Host "Ни разу не подключались ($($never.Count) файлов) — в проценте выше их нет:"
        $never | ForEach-Object { Write-Host "  $_" }
    }
}
