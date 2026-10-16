using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace PatchTransition
{
    /// <summary>
    /// Убирает из лога два вида сообщений, которые без Combat Extended неизбежны и ни на что
    /// не влияют. Оба фильтра предельно узкие, а живёт файл в папке NoCE: с установленным CE
    /// этой сборки в игре нет вовсе, так что скрывать она ничего не может.
    ///
    /// Первое — патчи Core SK к дефам самого CE. Пак держит для него отдельные папки
    /// (Core_SK/Patches/Mods_CombatExtended/..., Core_SK_DLCModule/*/Patches/CombatExtended/...)
    /// и применяет их безусловно: файлы сгенерированы PatchGenerator.py, обёртки
    /// PatchOperationFindMod и атрибута MayRequire у них нет. Без CE цель каждой операции
    /// отсутствует, xpath не находит ничего, и игра печатает по строке на операцию — в замерах
    /// 1036 строк из 1057.
    ///
    /// Подставить пустые дефы вместо них не выйдет: операции правят не сам деф, а его
    /// внутренности (.../ammoTypes/Ammo_Arrow_Plasteel, .../statBases/Bulk), и наши собственные
    /// дефы это подтверждают — они существуют, и всё равно 69 операций не находят в них узла.
    /// Поэтому прячется сообщение, а не причина: причина безвредна, операция не применилась бы
    /// ни при каком раскладе. Фильтр смотрит на sourceFile операции и молчит только о тех, чей
    /// файл лежит в CE-папке пака; прочие сбои патчей остаются видимыми.
    ///
    /// Второе — ванильная проверка forcedMiss у стволов, стреляющих взрывными снарядами CE.
    /// VerbProperties.ConfigErrors требует, чтобы forcedMissRadius стоял ровно у тех вербов,
    /// чей снаряд взрывной (CausesExplosion смотрит, происходит ли thingClass от
    /// Projectile_Explosive). Наш ProjectileCE_Explosive от него происходит намеренно — иначе
    /// explosionRadius не читает никто и ракеты прилетают без взрыва. Но forcedMissRadius у
    /// оружия пака не прописан: CE заменяет ванильный разброс своей баллистикой. Получается
    /// 86 строк о несоответствии, которое у нас сознательное.
    ///
    /// Дописать стволам forcedMissRadius было бы хуже: ванилла начала бы разбрасывать их
    /// выстрелы по площади, как миномётные. Поэтому фильтруется ровно это сообщение и только
    /// у вербов, чей снаряд имеет класс из заглушки. CausesExplosion в игре больше нигде не
    /// читается — только в этой проверке, так что скрывается чистое предупреждение.
    /// </summary>
    public static class LogNoiseFilter
    {
        private const string HarmonyId = "linya.patchtransition";

        /// <summary>Путь к файлу патча ведёт в папку, названную по CE.</summary>
        private const string CEFolderMarker = "CombatExtended";

        /// <summary>Классы заглушки лежат в этом пространстве имён.</summary>
        private const string ShimNamespace = "CombatExtended";

        /// <summary>Отличительная часть ванильного текста про forcedMiss.</summary>
        private const string ForcedMissMarker = "forcedMiss";

        // Сообщение о патче печатается только у операций, которые ни разу не сработали. Поле
        // приватное, а знать его нужно: иначе в счёт попали бы и успешные операции.
        private static readonly AccessTools.FieldRef<PatchOperation, bool> NeverSucceeded =
            AccessTools.FieldRefAccess<PatchOperation, bool>("neverSucceeded");

        private static int hiddenPatches;
        private static int hiddenVerbWarnings;

        public static void Install()
        {
            var harmony = new Harmony(HarmonyId);

            // PatchOperationSequence.Complete вызывает base.Complete, поэтому хватает базового
            harmony.Patch(
                AccessTools.Method(typeof(PatchOperation), nameof(PatchOperation.Complete)),
                new HarmonyMethod(typeof(LogNoiseFilter), nameof(SkipCEPatchFailure)));

            harmony.Patch(
                AccessTools.Method(typeof(LoadedModManager), nameof(LoadedModManager.ClearCachedPatches)),
                postfix: new HarmonyMethod(typeof(LogNoiseFilter), nameof(ReportHiddenPatches)));

            // ConfigErrors — итератор: тело при вызове не выполняется, поэтому постфикс просто
            // оборачивает возвращённую последовательность
            harmony.Patch(
                AccessTools.Method(typeof(VerbProperties), nameof(VerbProperties.ConfigErrors)),
                postfix: new HarmonyMethod(typeof(LogNoiseFilter), nameof(DropForcedMissWarning)));
        }

        private static bool SkipCEPatchFailure(PatchOperation __instance)
        {
            if (!NeverSucceeded(__instance))
                return true;

            string file = __instance.sourceFile;
            if (file == null || file.IndexOf(CEFolderMarker, StringComparison.OrdinalIgnoreCase) < 0)
                return true;

            hiddenPatches++;
            return false;
        }

        private static void DropForcedMissWarning(VerbProperties __instance, ref IEnumerable<string> __result)
        {
            var projectile = __instance.defaultProjectile;
            if (projectile == null || projectile.thingClass == null)
                return;
            if (projectile.thingClass.Namespace != ShimNamespace)
                return;

            __result = WithoutForcedMiss(__result);
        }

        private static IEnumerable<string> WithoutForcedMiss(IEnumerable<string> errors)
        {
            foreach (string error in errors)
            {
                if (error != null && error.IndexOf(ForcedMissMarker, StringComparison.Ordinal) >= 0)
                {
                    hiddenVerbWarnings++;
                    continue;
                }
                yield return error;
            }
        }

        private static void ReportHiddenPatches()
        {
            if (hiddenPatches == 0)
                return;

            Log.Message("[Patch Transition] скрыто " + hiddenPatches + " сообщений о патчах Core SK к дефам"
                + " Combat Extended: без CE их целей не существует. Это только сообщения —"
                + " сами операции не применялись бы и так.");
            hiddenPatches = 0;
        }

        /// <summary>
        /// Вербы проверяются позже патчей, поэтому счёт печатается отдельно — из класса с
        /// StaticConstructorOnStartup, который выполняется после проверки всех дефов.
        /// </summary>
        public static void ReportHiddenVerbWarnings()
        {
            if (hiddenVerbWarnings == 0)
                return;

            Log.Message("[Patch Transition] скрыто " + hiddenVerbWarnings + " предупреждений о forcedMiss"
                + " у оружия со взрывными снарядами: разброс по площади задаёт CE, а не ванилла,"
                + " поэтому несоответствие здесь ожидаемо. Взрывы при этом работают.");
            hiddenVerbWarnings = 0;
        }
    }

    /// <summary>
    /// Счёт предупреждений печатается из StaticConstructorOnStartup: вербы проверяются позже
    /// патчей, уже после ErrorCheckAllDefs.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class LogNoiseFilterReport
    {
        static LogNoiseFilterReport()
        {
            LogNoiseFilter.ReportHiddenVerbWarnings();
        }
    }
}
