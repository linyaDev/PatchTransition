// MemberDump — поверхность сборки одной строкой на член, для сравнения двух версий.
//
// Выводит "Тип::Метод/числоПараметров<TAB>размерIL" и "Тип::#Поле<TAB>-", отсортированно. Дифф
// двух таких дампов отвечает на вопрос, который по самой dll не виден: что в сборке появилось,
// что исчезло и у каких методов изменилось тело. Выпотрошенный метод узнаётся по размеру — была
// сотня байт, стало 7 (один ret).
//
// Так проверялась догадка, что команда HSK вырезала часть кода Combat Extended. Оказалось
// наоборот (см. "Паковая копия CE" в CLAUDE.md), и это стоило одного запуска:
//
//     dotnet run --project Tools/MemberDump -- "<апстрим>/Assemblies/CombatExtended.dll" > up.txt
//     dotnet run --project Tools/MemberDump -- "<пак>/Assemblies/CombatExtended.dll"     > pk.txt
//     cut -f1 up.txt | sort -u > up_m.txt; cut -f1 pk.txt | sort -u > pk_m.txt
//     comm -23 up_m.txt pk_m.txt   # есть в апстриме, нет в паке
//     comm -13 up_m.txt pk_m.txt   # наоборот
//
// Важно: размеры IL сравнимы только у сборок одной ревизии. Паковая CE собрана почти на год
// позже релиза с тем же modVersion, поэтому там различались тела 2824 методов из 3369 — это
// пересборка с другого исходника, а не правки.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using var fs = File.OpenRead(args[0]);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();
var lines = new List<string>();
foreach (var th in md.TypeDefinitions)
{
    var td = md.GetTypeDefinition(th);
    string ns = md.GetString(td.Namespace), tn = md.GetString(td.Name);
    string full = (ns.Length > 0 ? ns + "." : "") + tn;
    if (full.Contains("<") || full.StartsWith("<")) continue;   // компилятором сгенерированное
    foreach (var mh in td.GetMethods())
    {
        var m = md.GetMethodDefinition(mh);
        string name = md.GetString(m.Name);
        int pcount = m.GetParameters().Count;
        int il = 0;
        if (m.RelativeVirtualAddress != 0)
        {
            try { il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILContent().Length; } catch { il = -1; }
        }
        lines.Add($"{full}::{name}/{pcount}\t{il}");
    }
    foreach (var fh in td.GetFields())
    {
        var f = md.GetFieldDefinition(fh);
        lines.Add($"{full}::#{md.GetString(f.Name)}\t-");
    }
}
lines.Sort(StringComparer.Ordinal);
foreach (var l in lines) Console.WriteLine(l);
