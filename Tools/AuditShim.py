"""
Сверяет поля заглушек с настоящими классами Combat Extended.

Зачем. Если поле заглушки объявлено не тем типом, что в CE, движок падает прямо
посреди разбора дефа и теряет его целиком — без единого слова про CE в сообщении.
Так Regenerates, объявленный float вместо bool, съел 16 дефов рас: в логе была
только строка "Exception parsing System.Single from 'true'".

Запуск (нужны декомпилированные исходники CE в D:\\Mods\\.source):

    python Tools/AuditShim.py

Что печатает:

    TYPE   — тип поля не совпадает с CE. Разбираться обязательно: часть различий
             сделана нарочно (string вместо енума или дефа CE, чтобы движок не
             пытался его разрешить), остальные — ошибки.
    EXTRA  — поля нет в CE. Либо наше (мост к ванилле), либо приватное/свойство
             в CE, либо писалось дефами HSK, которых у CE не бывает.
"""
import pathlib
import re

SHIM = pathlib.Path(__file__).resolve().parent.parent / "Source" / "CombatExtendedShim"
CE = pathlib.Path(r"D:\Mods\.source\CombatExtended")

FIELD = re.compile(r"^\s*public\s+(?!class|abstract|static|override|virtual)([\w\.<>\[\]]+)\s+(\w+)\s*(?:=[^;]+)?;")
CLASS = re.compile(r"^\s*public\s+(?:abstract\s+|static\s+)?class\s+(\w+)\s*(?::\s*([\w\.<>]+))?")

# Имена типов в декомпиляции и в исходнике пишутся по-разному
ALIASES = {"Single": "float", "Boolean": "bool", "Int32": "int", "String": "string",
           "UnityEngine.Vector2": "Vector2", "UnityEngine.Vector3": "Vector3"}


def fields_by_class(paths):
    """{класс: {поле: тип}} плюс {класс: базовый класс} отдельным словарём."""
    out = {}
    bases = {}
    for path in paths:
        current = None
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for line in text.splitlines():
            found = CLASS.match(line)
            if found:
                current = found.group(1)
                out.setdefault(current, {})
                bases[current] = found.group(2)
                continue
            found = FIELD.match(line)
            if found and current:
                out[current][found.group(2)] = found.group(1)
    return out, bases


def main():
    if not CE.exists():
        print(f"нет исходников CE: {CE}")
        return
    shim, shim_bases = fields_by_class(SHIM.glob("*.cs"))
    real, real_bases = fields_by_class(CE.rglob("*.cs"))

    unknown = []
    for cls, fields in sorted(shim.items()):
        if cls not in real:
            unknown.append(cls)
            continue
        for name, declared in fields.items():
            actual = real[cls].get(name)
            if actual is None:
                print(f"EXTRA  {cls}.{name} ({declared})")
            elif ALIASES.get(actual, actual) != ALIASES.get(declared, declared):
                print(f"TYPE   {cls}.{name}: шим {declared} != CE {actual}")

    print()
    print("классов нет в исходниках CE (свои, переименованные или вложенные):")
    print("  " + ", ".join(unknown))

    # Базы у заглушек нарочно другие — ванильные вместо классов CE. Смотреть надо не на
    # сам факт расхождения, а на то, та ли это ванильная база: IncendiaryFuel в CE —
    # это Filth, и с ThingWithComps деф ломался бы при появлении на карте.
    print()
    print("базовые классы (ванильные вместо CE — так и задумано, но проверь родство):")
    for cls in sorted(shim):
        if cls in real_bases and shim_bases.get(cls) != real_bases.get(cls):
            print(f"  {cls}: шим {shim_bases.get(cls)} != CE {real_bases.get(cls)}")


if __name__ == "__main__":
    main()
