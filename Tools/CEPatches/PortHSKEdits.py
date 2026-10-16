"""Переносит правки Hardcore SK к Combat Extended на другую версию CE.

Зачем. Пак не работает с нетронутой CE: он поставляется со своей копией, где 222 файла дефов
переименованы в .xml.bak, а 28 отредактированы, и его сгенерированные патчи написаны именно под
такую форму. Узлы, которые эти патчи заменяют (например statBases/MarketValue у патронов), есть
только в паковой копии — ни в апстримной 1.6, ни в 1.7 их нет. То есть это правки HSK внутри CE,
и переносить их нужно так же, внутри CE.

Что делает:

    1. Сравнивает апстримную CE той же версии, что у пака, с паковой копией, и собирает список
       файлов, различающихся по существу (пробелы, BOM и переводы в счёт не идут).
    2. Для каждого такого файла берёт дифф апстрим → пак и накладывает его на файл целевой CE.
       Так переносится вся правка, а не только тот узел, по которому промахнулся патч.
    3. Переименовывает в .xml.bak файлы из disabled-files.txt — это способ самого пака выключать
       контент CE: RimWorld читает только .xml.
    4. То же для deleted-files.txt: эти файлы пак из своей копии удаляет совсем (патчи CE к ванилле,
       которые он заменяет своими). Мы их не удаляем, а отключаем — результат тот же, а видно лучше.
    5. Копирует файлы из added-files.txt — дефы и патчи, которые пак добавляет в CE от себя. Без
       этого шага отключение по deleted-files может вынести контент: upstream-овский
       14.5x114mmSoviet.xml пак удаляет, но взамен кладёт свой 14.5x114mm.xml, и без него 35 его
       патчей к этим патронам промахиваются.
    6. Копирует текстуры, которые есть в паковой копии и отсутствуют в апстримной: часть арта пак
       добавляет в CE сам, и его дефы на этот арт ссылаются.
    7. Убирает из LoadFolders.xml записи ModPatches. Пак поставляется с CE без этой папки вовсе,
       а в клоне она есть и грузится для установленных модов — принося ссылки на контент, который
       пак отключает (ModPatches/Androids выдавал в сценарии отключённый патрон). Файлы остаются
       на месте, достаточно того, что игра их не подключает.

Списки хранятся файлами рядом, а не выводятся на ходу: так видно, что именно отключается, и можно
отключить лишнее вручную. Отключать нужно весь список, а не выборку по логу — файлы патронов
объявляют общие наборы (AmmoSet_Rifle и ещё пять), на которые десятки других наборов CE смотрят
через similarTo, так что частичное урезание создаёт новые повисшие ссылки вместо старых.
FindRefHolders.py помогает разобрать такие ссылки по логу прогона.

Запуск:

    python PortHSKEdits.py --upstream <CE того же номера, что у пака> \\
                           --pack     <Mods/CombatExtended из пака> \\
                           --target   <клон CE, куда переносим>

Пути папок DLC сдвигаются автоматически: в 1.7 CE переложила Biotech/Patches/X в
Biotech/Patches/Biotech/X, и сравнение по прямым путям эти папки пропускало — вместе с файлом
RangedIndustrial_Biotech.xml, который давал четыре ошибки из последних пятнадцати.

Папки Languages, ModPatches, About и LoadFolders.xml пропускаются: переводы не нужны, патчи к
сторонним модам к делу не относятся, а About с LoadFolders привязаны к версии игры.
"""

import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile

CR = chr(13)
LF = chr(10)


def read_list(path):
    """Строки списка: без пустых, без комментариев, без возвратов каретки."""
    raw = open(path, encoding="utf-8").read().replace(CR, "")
    return [l.strip() for l in raw.split(LF) if l.strip() and not l.startswith("#")]


SKIP_PREFIX = ("Languages/", "ModPatches/", "About/")
SKIP_FILES = ("LoadFolders.xml",)


DLC = ("Biotech", "Royalty", "Ideology", "Anomaly", "Odyssey")


def dlc_shifted(rel):
    """Путь 1.6 -> путь 1.7: CE переложила патчи DLC на уровень глубже,
    Biotech/Patches/X стало Biotech/Patches/Biotech/X. Без этого сравнение по
    путям пропускает папки DLC целиком."""
    parts = rel.split("/")
    if len(parts) > 2 and parts[0] in DLC and parts[1] == "Patches" and parts[2] != parts[0]:
        return "/".join([parts[0], "Patches", parts[0]] + parts[2:])
    return rel


def resolve(root, rel):
    """Путь в целевой CE: как есть, иначе со сдвигом DLC."""
    direct = os.path.join(root, *rel.split("/"))
    if os.path.exists(direct) or os.path.exists(direct + ".bak"):
        return direct
    shifted = rel if dlc_shifted(rel) == rel else dlc_shifted(rel)
    return os.path.join(root, *shifted.split("/"))


def rel_xml_files(root):
    for dirpath, _, names in os.walk(root):
        if os.sep + ".git" in dirpath:
            continue
        for name in names:
            if not name.endswith(".xml"):
                continue
            rel = os.path.relpath(os.path.join(dirpath, name), root).replace(os.sep, "/")
            if rel.startswith(SKIP_PREFIX) or rel in SKIP_FILES:
                continue
            yield rel


def normalised(path):
    """Содержимое без BOM и с единым пробелом — чтобы не считать правкой перевод строк."""
    data = open(path, "rb").read()
    if data.startswith(b"\xef\xbb\xbf"):
        data = data[3:]
    return re.sub(rb"\s+", b" ", data).strip()


def to_lf(src, dst):
    data = open(src, "rb").read()
    had_bom = data.startswith(b"\xef\xbb\xbf")
    if had_bom:
        data = data[3:]
    open(dst, "wb").write(data.replace(b"\r\n", b"\n"))
    return dst, had_bom


def edited_files(upstream, pack):
    """Файлы, которые пак правит по существу."""
    out = []
    for rel in rel_xml_files(upstream):
        up = os.path.join(upstream, *rel.split("/"))
        pk = os.path.join(pack, *rel.split("/"))
        if not os.path.exists(pk):
            continue  # у пака отключён или удалён — это другой случай
        try:
            if normalised(up) != normalised(pk):
                out.append(rel)
        except OSError:
            pass
    return sorted(out)


def port(upstream, pack, target, rels, work):
    ok = failed = 0
    for rel in rels:
        parts = rel.split("/")
        up = os.path.join(upstream, *parts)
        pk = os.path.join(pack, *parts)
        tg = resolve(target, rel)
        if not os.path.exists(tg):
            print("  %-56s нет в целевой CE" % rel[:56])
            failed += 1
            continue

        a, _ = to_lf(up, os.path.join(work, "a"))
        b, _ = to_lf(pk, os.path.join(work, "b"))
        c, had_bom = to_lf(tg, os.path.join(work, "c"))
        diff = subprocess.run(["diff", "-u", a, b], capture_output=True, text=True).stdout
        patch_file = os.path.join(work, "p.patch")
        open(patch_file, "w", newline="").write(diff)

        result = subprocess.run(
            ["patch", "--forward", "--fuzz=3", "-s", c, patch_file],
            capture_output=True, text=True)
        noise = (result.stdout + result.stderr).strip()
        if result.returncode != 0:
            print("  %-56s НЕ ПРИМЕНИЛОСЬ: %s" % (rel[:56], noise.replace("\n", " | ")[:70]))
            failed += 1
            continue

        data = open(c, "rb").read()
        if had_bom and not data.startswith(b"\xef\xbb\xbf"):
            data = b"\xef\xbb\xbf" + data   # BOM убрала нормализация, возвращаем
        open(tg, "wb").write(data)
        ok += 1
        print("  %-56s перенесено%s" % (rel[:56], " (с запасом)" if noise else ""))
    return ok, failed


def disable(target, list_file):
    done = already = missing = 0
    for rel in read_list(list_file):
        path = resolve(target, rel)
        if os.path.exists(path):
            os.replace(path, path + ".bak")
            done += 1
        elif os.path.exists(path + ".bak"):
            already += 1
        else:
            print("  %-56s нет такого файла" % rel[:56])
            missing += 1
    return done, already, missing




def added_textures(pack, upstream):
    """Арт, который пак добавил в свою копию CE: есть у него, нет в апстриме."""
    def pngs(root):
        base = os.path.join(root, "Textures")
        out = set()
        for dirpath, _, names in os.walk(base):
            for name in names:
                if name.lower().endswith(".png"):
                    out.add(os.path.relpath(os.path.join(dirpath, name), base).replace(os.sep, "/"))
        return out
    return sorted(pngs(pack) - pngs(upstream))


def copy_added(pack, target, list_file):
    """Файлы, которых в апстримной CE нет: пак их добавляет от себя."""
    copied = 0
    for rel in read_list(list_file):
        src = os.path.join(pack, *rel.split("/"))
        dst = resolve(target, rel)
        if not os.path.exists(src):
            print("  %-56s нет в паковой копии" % rel[:56])
            continue
        if os.path.exists(dst):
            continue
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        copied += 1
    return copied




def copy_textures(pack, target, rels):
    copied = 0
    for rel in rels:
        src = os.path.join(pack, "Textures", *rel.split("/"))
        dst = os.path.join(target, "Textures", *rel.split("/"))
        if os.path.exists(dst):
            continue
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        copied += 1
    return copied


def strip_mod_patches(target):
    """Записи ModPatches в LoadFolders: пак этой папки не поставляет вовсе."""
    path = os.path.join(target, "LoadFolders.xml")
    if not os.path.exists(path):
        return 0
    data = open(path, encoding="utf-8-sig").read()
    kept = [l for l in data.split(LF) if "ModPatches" not in l]
    dropped = len(data.split(LF)) - len(kept)
    if dropped:
        open(path, "w", encoding="utf-8-sig", newline="").write(LF.join(kept))
    return dropped


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--upstream", required=True, help="апстримная CE того же номера, что у пака")
    ap.add_argument("--pack", required=True, help="Mods/CombatExtended из Hardcore SK")
    ap.add_argument("--target", required=True, help="CE, в которую переносим")
    ap.add_argument("--disable-list", default=os.path.join(here, "disabled-files.txt"))
    ap.add_argument("--deleted-list", default=os.path.join(here, "deleted-files.txt"))
    ap.add_argument("--added-list", default=os.path.join(here, "added-files.txt"))
    ap.add_argument("--dry-run", action="store_true", help="только показать, что было бы сделано")
    args = ap.parse_args()

    for name, path in (("upstream", args.upstream), ("pack", args.pack), ("target", args.target)):
        if not os.path.isdir(path):
            sys.exit("нет каталога %s: %s" % (name, path))

    rels = edited_files(args.upstream, args.pack)
    added = added_textures(args.pack, args.upstream)
    print("файлов, которые пак правит по существу: %d" % len(rels))
    if args.dry_run:
        for rel in rels:
            print("  ", rel)
        def count(path):
            return len(read_list(path))
        print("отключить по disabled-files: %d" % count(args.disable_list))
        print("отключить по deleted-files:  %d" % count(args.deleted_list))
        print("скопировать файлов пака: %d" % count(args.added_list))
        print("скопировать текстур: %d" % len(added))
        return

    added = added_textures(args.pack, args.upstream)

    work = tempfile.mkdtemp(prefix="ceport")
    try:
        ok, failed = port(args.upstream, args.pack, args.target, rels, work)
    finally:
        shutil.rmtree(work, ignore_errors=True)

    print()
    off1 = disable(args.target, args.disable_list)
    off2 = disable(args.target, args.deleted_list)
    files = copy_added(args.pack, args.target, args.added_list)
    copied = copy_textures(args.pack, args.target, added)
    modpatches = strip_mod_patches(args.target)

    print()
    print("перенесено %d, не вышло %d" % (ok, failed))
    print("отключено по disabled-files: %d (уже было %d, не найдено %d)" % off1)
    print("отключено по deleted-files:  %d (уже было %d, не найдено %d)" % off2)
    print("скопировано файлов пака: %d" % files)
    print("скопировано текстур: %d" % copied)
    print("убрано записей ModPatches из LoadFolders: %d" % modpatches)


if __name__ == "__main__":
    main()
