using RimWorld;
using Verse;
using Verse.AI;

namespace CombatExtended
{
    /// <summary>
    /// Маркер заглушки. По нему загрузчик (PatchTransition.ShimLoader) отличает эту сборку от
    /// настоящего Combat Extended: имя у них одно и то же, а сообщения в лог нужны разные.
    /// </summary>
    public static class PatchTransitionShim
    {
        public const string PackageId = "linya.patchtransition";
    }
}

namespace CombatExtended.AI
{
    /// <summary>
    /// В CE это абстрактный класс, а не интерфейс, несмотря на имя. От него наследуется
    /// SK.JobDriver_BeatFireWithTool, поэтому без него игра отвергает всю сборку
    /// FireExtinguisher целиком — вместе с огнетушителем и его работами.
    ///
    /// Поля и свойства повторяют CE один в один: сборки Core SK скомпилированы против них,
    /// и несовпадение сигнатуры роняет загрузку типа.
    /// </summary>
    public abstract class IJobDriver_Tactical : JobDriver
    {
        public int numAttacksMade;

        public virtual CompSuppressable CompSuppressable => pawn?.TryGetComp<CompSuppressable>();

        public virtual CompInventory CompInventory => pawn?.TryGetComp<CompInventory>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref numAttacksMade, "numAttacksMade", 0);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
    }
}
