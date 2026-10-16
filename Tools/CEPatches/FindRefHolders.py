"""Находит файлы CE, чьи дефы держат повисшие ссылки, и проверяет их по паковой копии.

Так получается список для disabled-files.txt. Логика простая: из лога прогона берутся сообщения
"Could not resolve cross-reference", из них — дефы-источники ссылок (AmmoDef и AmmoLink), затем
ищутся файлы целевой CE, которые эти дефы объявляют. Для каждого файла печатается, в каком он
состоянии у пака: активен, отключён (.xml.bak) или отсутствует.

В список стоит брать те, что у пака отключены — значит он и сам обходится без них, а остальное
требует разбора: активный файл пак, скорее всего, правит, и тогда правку надо переносить
(PortHSKEdits.py), а не выключать файл.

Запуск:

    python FindRefHolders.py --log "<профиль>/Logs/Player.log" \\
                             --pack <Mods/CombatExtended из пака> \\
                             --target <клон CE> [--out disabled-files.txt]
"""

import argparse
import os
import re


def dangling_sources(log_path):
    """Дефы, из которых ведут неразрешённые ссылки."""
    text = open(log_path, encoding="utf-8", errors="replace").read()
    names = set(re.findall(r"to CombatExtended\.AmmoDef (\w+)", text))
    names |= set(re.findall(r"AmmoLink \((\w+) ->", text))
    return names


def owners(root, names):
    """Файл целевой CE -> какие из этих дефов он объявляет."""
    found = {}
    for dirpath, _, filenames in os.walk(root):
        if os.sep + ".git" in dirpath:
            continue
        for name in filenames:
            if not name.endswith(".xml"):
                continue
            path = os.path.join(dirpath, name)
            try:
                text = open(path, encoding="utf-8-sig").read()
            except OSError:
                continue
            hit = [d for d in names if "<defName>%s</defName>" % d in text]
            if hit:
                rel = os.path.relpath(path, root).replace(os.sep, "/")
                found[rel] = hit
    return found


def state_in_pack(pack, rel):
    path = os.path.join(pack, *rel.split("/"))
    if os.path.exists(path):
        return "активен"
    if os.path.exists(path + ".bak"):
        return "отключён .bak"
    return "отсутствует"


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--log", required=True)
    ap.add_argument("--pack", required=True)
    ap.add_argument("--target", required=True)
    ap.add_argument("--out", help="куда записать список отключаемых")
    args = ap.parse_args()

    names = dangling_sources(args.log)
    print("дефов-источников повисших ссылок: %d" % len(names))

    found = owners(args.target, names)
    print()
    print("%-52s %5s  %s" % ("файл целевой CE", "дефов", "у пака"))
    plan = []
    for rel, hit in sorted(found.items()):
        state = state_in_pack(args.pack, rel)
        print("%-52s %5d  %s" % (rel[:52], len(hit), state))
        if state.startswith("отключён"):
            plan.append(rel)

    print()
    print("к отключению: %d" % len(plan))
    if args.out:
        with open(args.out, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(plan) + "\n")
        print("записано в %s" % args.out)


if __name__ == "__main__":
    main()
