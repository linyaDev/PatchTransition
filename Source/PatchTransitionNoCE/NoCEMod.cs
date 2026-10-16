using System;
using System.IO;
using System.Reflection;
using Verse;

namespace PatchTransition
{
    /// <summary>
    /// Точка входа папки NoCE: голос заглушки Combat Extended и её фильтр сообщений.
    ///
    /// Почему заглушка вообще нужна. Дефы Core SK ссылаются на классы CE прямо в XML
    /// (Class="CombatExtended.VerbPropertiesCE" и тому подобное). Если типа нет, деф не
    /// грузится целиком — а таких дефов больше двух сотен файлов. Значит тип должен
    /// существовать; заглушки наследуют ближайший ванильный класс, поэтому содержимое
    /// не просто грузится, а работает на ванильной механике.
    ///
    /// Грузит заглушку сама игра: CombatExtended.dll лежит рядом, в NoCE/Assemblies, а папка
    /// подключается только при отсутствии CE (LoadFolders.xml). Так она попадает в игру на
    /// этапе LoadModContent — до того, как игра проверит сборки Core SK, ссылающиеся на типы
    /// CE. Ручная загрузка из Mod-конструктора этот момент не успевает: конструкторы
    /// выполняются позже, в CreateModClasses, когда Compatibility_CombatExtended и
    /// FireExtinguisher уже отвергнуты целиком.
    ///
    /// Поэтому страховки «догрузить заглушку, если папка не подключилась» здесь нет, и быть
    /// не может: этот класс живёт в той же папке, так что при её отсутствии не загрузится и
    /// он сам. А спасать в такой ситуации уже нечего — см. выше. Остаётся диагностика, и
    /// место ей именно тут: сообщение нужно ровно тогда, когда CE нет.
    /// </summary>
    public class NoCEMod : Mod
    {
        private const string ShimAssemblyName = "CombatExtended";
        private const string ShimMarkerType = "CombatExtended.PatchTransitionShim";

        private static bool done;

        public NoCEMod(ModContentPack content) : base(content)
        {
            if (done)
                return;
            done = true;

            try
            {
                CheckShim(content);
            }
            catch (Exception e)
            {
                Log.Error("[Patch Transition] не удалось проверить заглушку: " + e);
            }
        }

        private static void CheckShim(ModContentPack content)
        {
            Assembly loaded = FindLoadedAssembly(ShimAssemblyName);

            if (loaded == null)
            {
                // Папка подключилась (иначе нас бы здесь не было), а сборки в ней нет.
                Log.Error("[Patch Transition] папка NoCE подключена, но заглушка не загружена."
                    + " Ожидался файл " + Path.Combine(content.RootDir, "NoCE", "Assemblies",
                        ShimAssemblyName + ".dll") + ". Без неё дефы Core SK, называющие типы CE,"
                    + " не загрузятся.");
                return;
            }

            if (loaded.GetType(ShimMarkerType) == null)
            {
                // Сюда попасть можно только если CE установлен под другим packageId: условие
                // IfModNotActive="CETeam.CombatExtended" его не поймало, а сборка настоящая.
                Log.Warning("[Patch Transition] загружен настоящий Combat Extended, хотя папка NoCE"
                    + " подключена: проверка по packageId CETeam.CombatExtended его не нашла."
                    + " Заглушка не используется, фильтр сообщений не ставится.");
                return;
            }

            Log.Message("[Patch Transition] Combat Extended не найден, подставлена заглушка ("
                + loaded.GetTypes().Length + " типов). Контент Core SK будет работать"
                + " на ванильной механике.");

            try
            {
                LogNoiseFilter.Install();
            }
            catch (Exception e)
            {
                Log.Warning("[Patch Transition] фильтр сообщений не встал: " + e.Message);
            }
        }

        /// <summary>
        /// Ищем по имени сборки, а не по packageId: у стимовских подписок к packageId
        /// дописывается _steam, и проверка по идентификатору промахивается.
        /// </summary>
        private static Assembly FindLoadedAssembly(string assemblyName)
        {
            foreach (var mod in LoadedModManager.RunningMods)
            {
                var list = mod.assemblies?.loadedAssemblies;
                if (list == null)
                    continue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].GetName().Name == assemblyName)
                        return list[i];
                }
            }
            return null;
        }
    }
}
