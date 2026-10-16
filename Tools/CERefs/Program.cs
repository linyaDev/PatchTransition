// CERefs — что сборки пака просят у Combat Extended.
//
// AuditShim.py сверяет поля заглушки с настоящим CE, но не отвечает на другой вопрос: какие типы CE
// вообще нужны *коду* пака. По логу это не видно — отсутствующий тип роняет не деф, а целый класс
// патчей, и причина уходит внутрь TypeLoadException где-нибудь в TargetMethods.
//
// Здесь читается таблица TypeRef/MemberRef в сборках: каждое имя из пространства CombatExtended
// выводится вместе с подписью, по которой его зовут. Разница с типами заглушки и есть worklist —
// именно так нашлись PatchOperationMakeGunCECompatible, CE_DamageDefOf, DefUtility, GenExplosionCE
// и Utilities.GenClosest, из-за первого из которых не применялся ни один патч Core SK.
//
// Запуск (путь или папка, можно несколько):
//
//     dotnet run --project Tools/CERefs -- "<Mods>/Core_SK" "<Mods>/Core_SK_CoreModule" //         "<Mods>/Core_SK_DLCModule" "<Mods>/SurvivalToolsLite"
//
// Сверка с заглушкой:
//
//     dotnet run --project Tools/CERefs -- "<Mods>/Core_SK" | cut -f1 | sort -u > wanted.txt
//     ilspycmd -l c NoCE/Assemblies/CombatExtended.dll | sed 's/^Class //' //         | grep ^CombatExtended | sort > shim.txt
//     comm -23 wanted.txt shim.txt
//
// Вывод: имя типа, подпись члена, файл — через табуляцию.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var found = new SortedSet<string>();
foreach (var path in args)
foreach (var file in Directory.Exists(path) ? Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories) : new[] { path })
{
    try
    {
        using var fs = File.OpenRead(file);
        using var pe = new PEReader(fs);
        if (!pe.HasMetadata) continue;
        var md = pe.GetMetadataReader();
        var provider = new P();
        foreach (var h in md.MemberReferences)
        {
            var mr = md.GetMemberReference(h);
            if (mr.Parent.Kind != HandleKind.TypeReference) continue;
            var tr = md.GetTypeReference((TypeReferenceHandle)mr.Parent);
            string ns = md.GetString(tr.Namespace), tn = md.GetString(tr.Name);
            if (ns != "CombatExtended" && !ns.StartsWith("CombatExtended.")) continue;
            string member = md.GetString(mr.Name);
            string sig;
            if (mr.GetKind() == MemberReferenceKind.Method)
            {
                var m = mr.DecodeMethodSignature(provider, null);
                sig = m.ReturnType + " " + member + "(" + string.Join(", ", m.ParameterTypes) + ")";
            }
            else sig = mr.DecodeFieldSignature(provider, null) + " " + member;
            found.Add(ns + "." + tn + "\t" + sig + "\t" + Path.GetFileName(file));
        }
    }
    catch (Exception e) { Console.Error.WriteLine(file + ": " + e.Message); }
}
foreach (var f in found) Console.WriteLine(f);

class P : ISignatureTypeProvider<string, object>
{
    public string GetArrayType(string e, ArrayShape s) => e + "[]";
    public string GetByReferenceType(string e) => e + "&";
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
    public string GetGenericMethodParameter(object g, int i) => "!!" + i;
    public string GetGenericTypeParameter(object g, int i) => "!" + i;
    public string GetModifiedType(string m, string u, bool isRequired) => u;
    public string GetPinnedType(string e) => e;
    public string GetPointerType(string e) => e + "*";
    public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString();
    public string GetSZArrayType(string e) => e + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte rawKind) => r.GetString(r.GetTypeDefinition(h).Name);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte rawKind) => r.GetString(r.GetTypeReference(h).Name);
    public string GetTypeFromSpecification(MetadataReader r, object g, TypeSpecificationHandle h, byte rawKind) => "spec";
}

