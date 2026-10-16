using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace CombatExtended
{
    // ===== Компоненты =====
    // Все инертны: задача — дать дефам загрузиться, механику берёт на себя ванилла.

    /// <summary>
    /// Пустой компонент для заглушек. Нужен именно свой: Verse.ThingComp абстрактный, и
    /// compClass, указывающий на него, роняет предмет при появлении на карте —
    /// "Cannot dynamically create an instance of type 'Verse.ThingComp'".
    /// </summary>
    public class CompInert : ThingComp { }

    public class CompProperties_FireModes : CompProperties
    {
        public int aimedBurstShotCount;
        public bool noSingleShot;
        public bool noSnapshot;
        public bool aiUseBurstMode;
        public string aiAimMode;

        public CompProperties_FireModes() { compClass = typeof(CompInert); }
    }

    public class CompProperties_Fragments : CompProperties
    {
        public List<ThingDefCountClass> fragments = new List<ThingDefCountClass>();
        public float fragSpeedFactor = 1f;

        public CompProperties_Fragments() { compClass = typeof(CompInert); }
    }

    /// <summary>
    /// Типы полей здесь обязаны совпадать с CE до единого: Regenerates в CE — bool, и пока
    /// он был объявлен float, движок падал на &lt;Regenerates&gt;true&lt;/Regenerates&gt; и терял деф
    /// целиком. Так ушли 16 дефов рас, включая Humanlike, механоидов и насекомых.
    /// </summary>
    public class CompProperties_ArmorDurability : CompProperties
    {
        public float Durability;
        public bool Regenerates;
        public float RegenInterval;
        public float RegenValue;
        public bool Repairable;
        public List<ThingDefCountClass> RepairIngredients;
        public int RepairTime;
        public float RepairValue;
        public bool CanOverHeal;
        public float MaxOverHeal;
        public float MinArmorValueSharp = -1f;
        public float MinArmorValueBlunt = -1f;
        public float MinArmorValueHeat = -1f;
        public float MinArmorValueElectric = -1f;
        public float MinArmorPct = 0.25f;
        // Этих двух в CE нет, но дефы HSK их пишут
        public float DurabilityMultiplier = 1f;
        public float RepairIngredientMultiplier = 1f;

        public CompProperties_ArmorDurability() { compClass = typeof(CompArmorDurability); }
    }

    public class CompArmorDurability : ThingComp { }

    /// <summary>Наследует ванильную взрывчатку, поэтому взрывы продолжают работать.</summary>
    public class CompProperties_ExplosiveCE : CompProperties_Explosive
    {
        public float fragRange;
        public bool damageAdjacentCells;
    }

    public class CompProperties_Charges : CompProperties
    {
        public List<int> charges = new List<int>();
        public List<int> chargeSpeeds = new List<int>();

        public CompProperties_Charges() { compClass = typeof(CompInert); }
    }

    public class CompProperties_Inventory : CompProperties
    {
        public CompProperties_Inventory() { compClass = typeof(CompInventory); }
    }

    public class CompInventory : ThingComp
    {
        /// <summary>
        /// В CE это поле, а не свойство, и сборки Core SK читают его как поле (ldfld) —
        /// свойство здесь дало бы MissingFieldException. Подставляем ванильный инвентарь
        /// пешки: тогда огнетушитель убирает оружие в карман, а не в никуда.
        /// </summary>
        public ThingOwner<Thing> container;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            container = (parent as Pawn)?.inventory?.innerContainer;
        }
    }

    public class CompProperties_TacticalManager : CompProperties
    {
        public CompProperties_TacticalManager() { compClass = typeof(CompInert); }
    }

    public class CompProperties_Suppressable : CompProperties
    {
        public CompProperties_Suppressable() { compClass = typeof(CompSuppressable); }
    }

    public class CompSuppressable : ThingComp { }

    /// <summary>Поля один в один как в CE: дефы ракет пишут <c>fleck</c>, <c>cutoffTickRange</c> и прочее.</summary>
    public class ProjectileFleckDataCE
    {
        public FleckDef fleck;
        public float emissionsPerTick = 1f;
        public int flecksPerEmission = 1;
        public FloatRange rotation = new FloatRange(0f, 360f);
        public FloatRange scale = new FloatRange(1f, 1f);
        public IntRange cutoffTickRange = new IntRange(-1, -1);
        public int startDelayTick = 1;
        public FloatRange originOffset = new FloatRange(0.7f, 0.7f);
    }

    public class CompProperties_ProjectileFleck : CompProperties
    {
        public List<ProjectileFleckDataCE> FleckDatas = new List<ProjectileFleckDataCE>();

        public CompProperties_ProjectileFleck() { compClass = typeof(CompInert); }
    }

    public class CompPawnGizmo : ThingComp { }

    // ===== Расширения дефов =====
    // Чистые контейнеры данных CE: без самого CE читать их некому, но объявить надо.

    public class RacePropertiesExtensionCE : DefModExtension
    {
        // Строка, а не список: в дефах пишут <bodyShape>Birdlike</bodyShape>
        public string bodyShape;
    }

    /// <summary>Вариант второго оружия у пешки: только для чтения XML.</summary>
    public class SidearmOption
    {
        public float generateChance = 1f;
        public FloatRange sidearmMoney;
        // В CE это FloatRange, хотя дефы пишут целые <min>/<max>
        public FloatRange magazineCount;
        public List<string> weaponTags = new List<string>();
        public List<string> weaponTypes = new List<string>();
    }

    public class LoadoutPropertiesExtension : DefModExtension
    {
        public FloatRange primaryMagazineCount;
        public AmmoCategoryDef forcedAmmoCategory;
        public List<SidearmOption> sidearms = new List<SidearmOption>();
        // В CE это SidearmOption, а не ссылка на деф: дефы пишут вложенный узел
        public SidearmOption forcedSidearm;
        public float shieldChance;
        public FloatRange shieldMoney;
        public List<string> shieldTags = new List<string>();
        public int minAmmoCount;
    }

    /// <summary>Имена полей с большой буквы — как в CE: дефы пака пишут DrawSize и DrawOffset.</summary>
    public class GunDrawExtension : DefModExtension
    {
        public UnityEngine.Vector2 DrawSize = UnityEngine.Vector2.one;
        public UnityEngine.Vector2 DrawOffset = UnityEngine.Vector2.zero;
        public float recoilModifier = 1f;
        public float recoilScale = -1f;
    }

    public class ApparelHediffExtension : DefModExtension
    {
        public HediffDef hediff;
    }

    // Имя класса с опечаткой — такое же, как в CE: дефы ссылаются именно на него.
    public class ApperalRenderingExtension : DefModExtension
    {
        public bool HideHair;
        public bool HideBeard;
    }

    public class ApparelDefExtension : DefModExtension { }

    public class ApparelLayerExtension : DefModExtension
    {
        public bool IsHeadwear;
    }

    public class DamageDefExtensionCE : DefModExtension
    {
        public bool isAmbientDamage;
        public bool harmOnlyOutsideLayers;
    }

    public class ThingDefExtensionCE : DefModExtension
    {
        public bool MenuHidden;
        public float MenuOrder;
    }

    public class ShieldDefExtension : DefModExtension
    {
        public List<string> shieldCoverage = new List<string>();
        public List<string> crouchCoverage = new List<string>();
    }

    /// <summary>
    /// Частичная броня: в дефах имя стата стоит на месте имени узла
    /// (&lt;ArmorRating_Sharp&gt;0.5&lt;/ArmorRating_Sharp&gt;), поэтому CE разбирает такой список
    /// сам, и заглушке тоже нужен свой разбор — иначе каждая запись даёт XML-ошибку.
    /// </summary>
    public class PartialArmorExt : DefModExtension
    {
        public List<ApparelPartialStat> stats;
    }

    public class ApparelPartialStat
    {
        public StatDef stat;
        public List<BodyPartDef> parts;
        public float statValue = 1f;
        public bool isStatValueStatic;

        /// <summary>
        /// Намеренно пустой: значения CE здесь никто не читает, а движку достаточно того,
        /// что разбор узла взял на себя класс и не споткнулся о незнакомое поле.
        /// </summary>
        public void LoadDataFromXmlCustom(System.Xml.XmlNode xmlRoot) { }
    }

    public class BodyPartExploderExt : DefModExtension { }

    public class ModExtension_AutoLoaderGraphics : DefModExtension
    {
        public GraphicData fullGraphic;
        public GraphicData emptyGraphic;
        public SoundDef reloadCompleteSound;
        public SoundDef reloadingSustainer;
        // Строки, как в CE: ссылками на дефы движок пытался бы их разрешить
        public List<string> allowedTurrets = new List<string>();
    }

    // ===== Предметы, здания, прочее =====

    public class Apparel_Shield : Apparel { }

    // Filth, а не ThingWithComps: в CE это грязь (FilthPrometheum), и код грязи
    // приводит её к Filth — с чужой базой деф сломался бы уже при появлении на карте
    public class IncendiaryFuel : Filth { }

    public class Building_AutoLoaderCE : Building { }

    public class Building_TurretGunCE : Building_TurretGun { }

    // Наследует ванильный JobGiver, как и в CE: заглушка, возвращавшая null, означала,
    // что пешки вообще не садятся за турели
    public class JobGiver_ManTurretsNearSelfCE : JobGiver_ManTurretsNearSelf { }

    public class ITab_Inventory : ITab_Pawn_Gear { }

    public class PawnRenderNode_Apparel : Verse.PawnRenderNode_Apparel
    {
        public PawnRenderNode_Apparel(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }
    }

    public class PawnRenderNodeWorker_Drafted : PawnRenderNodeWorker { }

    public class StatPart_QualityConditional : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val) { }

        public override string ExplanationPart(StatRequest req) => null;
    }

    public class GunPatcherPresetDef : Def { }

    public class ShellingResponseDef : Def { }
}
