using RimWorld;
using Verse;

// Заглушка Rimefeller (Dubwise.Rimefeller). Не контент мода, а ровно те три типа, на
// которые ссылаются сборки самого пака. Без них:
//
//   Temp-MechJobFix.dll — его статический конструктор берёт typeof двух рабочих заданий
//   Rimefeller, чтобы запретить колонийным мехам работать с нефтяными консолями. Типы не
//   грузятся, и конструктор падает целиком (TypeLoadException).
//
//   HSK_OdysseyChanges.dll — патчит Rimefeller.OilGrid.GenerateFields по имени, через
//   HarmonyPatch(string, string). Цели нет, Harmony.PatchAll бросает исключение, и вместе
//   с ним погибает весь Mod-класс модуля Odyssey — то есть и его собственные правки.
//
// Поэтому формы повторяют оригинал до поля: OilGrid держит public Map map, потому что
// префикс пака просит ___map, а HasJobOnThing у заданий именно переопределён — если
// унаследовать, AccessTools.Method отдаст ванильный WorkGiver_Scanner.HasJobOnThing, и
// запрет на работу мехов расползётся на все работы в игре.
namespace Rimefeller
{
    /// <summary>Сетка нефтяных полей. Пустая: без мода бурить всё равно нечего.</summary>
    public class OilGrid : IExposable
    {
        public Map map;
        public bool Deep;

        public OilGrid(Map map)
        {
            this.map = map;
        }

        public void GenerateFields() { }

        public void ExposeData() { }
    }

    public class WorkGiver_OperateResourceConsole : WorkGiver_Scanner
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => false;
    }

    public class WorkGiver_SuperviseDrilling : WorkGiver_Scanner
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => false;
    }
}
