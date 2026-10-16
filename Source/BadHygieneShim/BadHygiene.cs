using Verse;

// Заглушка Dubs Bad Hygiene (Dubwise.DubsBadHygiene) — один тип и два метода.
//
// HSK_OdysseyChanges.dll (Core_SK_DLCModule/Odyssey) патчит водяную сетку гигиены по имени:
// HarmonyPatch("DubsBadHygiene.MapComponent_Hygiene", "RegenWaterGrid"). Цель ищется через
// AccessTools, то есть по имени типа, а не по сборке — но если её нет, PatchAll бросает
// исключение, и вместе с ним гибнет весь Mod-класс модуля Odyssey: его собственные правки
// под DLC не применяются ни одна.
//
// Методов два, потому что на классе патча висят два атрибута HarmonyPatch:
// RegenWaterGrid и RegenDeepWaterGrid. Harmony сливает атрибуты класса в одну цель, так что
// пак на деле патчит только второй — но искать Harmony будет именно его, и одного
// RegenWaterGrid не хватило: ошибка осталась ровно та же, "Undefined target method".
// Объявлены оба: у настоящего мода оба public void без аргументов.
//
// Класс намеренно abstract. Map.FillComponents() перебирает AllSubclassesNonAbstract у
// MapComponent и создаёт каждый на каждой карте — непустой заглушке это дало бы запись
// <li Class="DubsBadHygiene.MapComponent_Hygiene"> в сейве, которую настоящий мод потом
// поднял бы с пустыми полями. Патчить метод абстрактного класса Harmony это не мешает.
namespace DubsBadHygiene
{
    public abstract class MapComponent_Hygiene : MapComponent
    {
        protected MapComponent_Hygiene(Map map) : base(map) { }

        /// <summary>Пересборка водяной сетки. Префикс пака только читает ___map.</summary>
        public void RegenWaterGrid() { }

        /// <summary>Та же сетка для глубокой воды — реальная цель слитых атрибутов.</summary>
        public void RegenDeepWaterGrid() { }
    }
}
