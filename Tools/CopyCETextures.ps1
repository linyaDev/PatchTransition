<#
    Копирует в NoCE/Textures те текстуры Combat Extended, на которые ссылаются дефы HSK.

    Зачем. Патроны, снаряды и иконки категорий пака прописаны путями вида
    Things/Ammo/Pistol/FMJ — файлы лежат в самом CE. Без CE дефы грузятся, но рисовать
    их нечем: в логе 102 строки про Texture2D, "No textures found" и MatFrom с null.
    Содержимое дефов при этом правильное, не хватает только картинок.

    Список путей — Tools/ce-textures.txt, он собран из лога: это ровно те пути, которые
    игра искала и не нашла. Сами файлы берутся из установленного CE (включая его папки
    HSK и DLC — там лежит часть калибров пака).

    Результат не коммитится: это чужая графика, а в поставке CE нет файла лицензии и в
    About.xml нет ни слова о правах. Для своей игры копия законна, для публичного
    репозитория — нет, поэтому NoCE/Textures в .gitignore, а воспроизводится эта папка
    запуском скрипта.

    Запуск из корня мода:
        powershell -ExecutionPolicy Bypass -File Tools/CopyCETextures.ps1
#>
param(
    [string]$CEPath,
    [string]$ModPath = (Split-Path $PSScriptRoot -Parent)
)

if (-not $CEPath) {
    foreach ($candidate in @(
        'D:\SteamLibrary\steamapps\common\RimWorld\Mods\CombatExtended',
        'D:\RimWorld_HSK\Mods\CombatExtended')) {
        if (Test-Path $candidate) { $CEPath = $candidate; break }
    }
}
if (-not $CEPath -or -not (Test-Path $CEPath)) {
    Write-Host 'Combat Extended не найден. Укажите путь: -CEPath <папка мода CE>'
    exit 1
}

$listFile = Join-Path $PSScriptRoot 'ce-textures.txt'
$paths = Get-Content $listFile | Where-Object { $_.Trim() -ne '' }
$target = Join-Path $ModPath 'NoCE\Textures'

$copied = 0
$missing = @()
# У CE несколько папок Textures: корневая и по одной в HSK и в папках DLC
$roots = @(Get-ChildItem -Path $CEPath -Directory -Recurse -Filter 'Textures')

foreach ($rel in $paths) {
    $relWin = $rel -replace '/', '\'
    $found = @()
    foreach ($root in $roots) {
        $src = Join-Path $root.FullName $relWin
        # файл <путь>.png, варианты <путь>_north.png и папка <путь>\ со стопками
        $found += Get-ChildItem -LiteralPath "$src.png" -ErrorAction SilentlyContinue
        $found += Get-ChildItem -Path "${src}_*.png" -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $src -PathType Container) {
            $found += Get-ChildItem -LiteralPath $src -Filter *.png -Recurse
        }
    }
    if ($found.Count -eq 0) { $missing += $rel; continue }

    foreach ($file in $found) {
        # путь внутри Textures сохраняем как есть — по нему деф и ищет картинку
        $inside = $file.FullName
        $cut = $inside.IndexOf('\Textures\')
        $relOut = $inside.Substring($cut + '\Textures\'.Length)
        $dest = Join-Path $target $relOut
        $destDir = Split-Path $dest -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item -LiteralPath $inside -Destination $dest -Force
        $copied++
    }
}

Write-Host "скопировано файлов: $copied, путей в списке: $($paths.Count)"
if ($missing.Count -gt 0) {
    Write-Host "не нашлось в CE ($($missing.Count)):"
    $missing | ForEach-Object { Write-Host "  $_" }
}

