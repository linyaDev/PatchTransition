using System.Collections.Generic;
using System.Linq;
using System.Xml;
using RimWorld;
using UnityEngine;
using Verse;

// Типы CE, которые нужны не дефам, а сборкам пака: Core_SK.dll ссылается на них напрямую.
//
// Нашлись они не по логу, а сверкой: Tools/ceref читает таблицу TypeRef/MemberRef в сборках пака
// и выдаёт все имена из пространства CombatExtended вместе с подписями, по которым их зовут.
// Разница с содержимым заглушки и есть список ниже — пять типов, и каждый дорого стоил:
// SK.CEPatchPatch просит PatchOperationMakeGunCECompatible в TargetMethods, тип не грузится,
// Harmony.PatchAll падает на этом классе, и ни один патч Core SK не применяется.
//
// Остальные четыре до вызова просто не доходили — PatchAll умирал раньше. Поэтому они тоже здесь:
// иначе та же история повторилась бы по одному типу за прогон.
namespace CombatExtended
{
    /// <summary>
    /// DefOf CE. Паку нужен один его тип урона — Electrical; он объявлен в NoCE/Defs/CEMisc.xml,
    /// поэтому ссылка разрешается как обычно.
    /// </summary>
    [DefOf]
    public static class CE_DamageDefOf
    {
        public static DamageDef Electrical;

        static CE_DamageDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CE_DamageDefOf));
        }
    }

    /// <summary>
    /// У CE это набор расширений над ThingDef с предрассчитанными массивами. Паку нужен один
    /// метод, и считаем его на месте: оружие считается площадным, если его верб стреляет
    /// снарядом с радиусом взрыва. Это ванильное понятие того же самого.
    /// </summary>
    public static class DefUtility
    {
        public static bool IsAOEWeapon(this ThingDef def)
        {
            if (def?.Verbs == null)
                return false;

            for (int i = 0; i < def.Verbs.Count; i++)
            {
                ThingDef projectile = def.Verbs[i].defaultProjectile;
                if (projectile?.projectile != null && projectile.projectile.explosionRadius > 0f)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Взрыв CE. Первые 36 параметров совпадают с ванильным GenExplosion.DoExplosion один в один —
    /// CE дописал свои пять в конец, — поэтому вызов просто передаётся ванилле.
    ///
    /// Из хвоста учитываются два, у которых есть ванильный смысл: scaleFactor CE применяет к урону,
    /// радиусу и пробитию (повторяем), а destroyAfterwards с explosionParentToDestroy уничтожает
    /// источник взрыва — без этого предмет остался бы висеть. height, falloffCurve и собственный
    /// ExplosionCE воспроизвести нечем: это баллистика CE.
    ///
    /// Подпись обязана совпадать до параметра: Core_SK.dll скомпилирован против неё.
    /// </summary>
    public static class GenExplosionCE
    {
        public static void DoExplosion(IntVec3 center, Map map, float radius, DamageDef damType,
            Thing instigator, int damAmount = -1, float armorPenetration = -1f,
            SoundDef explosionSound = null, ThingDef weapon = null, ThingDef projectile = null,
            Thing intendedTarget = null, ThingDef postExplosionSpawnThingDef = null,
            float postExplosionSpawnChance = 0f, int postExplosionSpawnThingCount = 1,
            GasType? postExplosionGasType = null, float? postExplosionGasRadiusOverride = null,
            int postExplosionGasAmount = 255, bool applyDamageToExplosionCellsNeighbors = false,
            ThingDef preExplosionSpawnThingDef = null, float preExplosionSpawnChance = 0f,
            int preExplosionSpawnThingCount = 1, float chanceToStartFire = 0f,
            bool damageFalloff = false, float? direction = null, List<Thing> ignoredThings = null,
            FloatRange? affectedAngle = null, bool doVisualEffects = true, float propagationSpeed = 1f,
            float excludeRadius = 0f, bool doSoundEffects = true,
            ThingDef postExplosionSpawnThingDefWater = null, float screenShakeFactor = 1f,
            SimpleCurve flammabilityChanceCurve = null, List<IntVec3> overrideCells = null,
            ThingDef postExplosionSpawnSingleThingDef = null,
            ThingDef preExplosionSpawnSingleThingDef = null, float height = 0f,
            float scaleFactor = 1f, bool destroyAfterwards = false,
            ThingWithComps explosionParentToDestroy = null, SimpleCurve falloffCurve = null)
        {
            if (map == null)
                return;

            scaleFactor = scaleFactor <= 0f ? 1f : Mathf.Clamp(scaleFactor, 0.1f, 10f);
            if (damAmount >= 0)
                damAmount = Mathf.RoundToInt(damAmount * scaleFactor);
            if (armorPenetration >= 0f)
                armorPenetration *= scaleFactor;
            radius *= scaleFactor;

            GenExplosion.DoExplosion(center, map, radius, damType, instigator, damAmount,
                armorPenetration, explosionSound, weapon, projectile, intendedTarget,
                postExplosionSpawnThingDef, postExplosionSpawnChance, postExplosionSpawnThingCount,
                postExplosionGasType, postExplosionGasRadiusOverride, postExplosionGasAmount,
                applyDamageToExplosionCellsNeighbors, preExplosionSpawnThingDef,
                preExplosionSpawnChance, preExplosionSpawnThingCount, chanceToStartFire,
                damageFalloff, direction, ignoredThings, affectedAngle, doVisualEffects,
                propagationSpeed, excludeRadius, doSoundEffects, postExplosionSpawnThingDefWater,
                screenShakeFactor, flammabilityChanceCurve, overrideCells,
                postExplosionSpawnSingleThingDef, preExplosionSpawnSingleThingDef);

            if (destroyAfterwards && explosionParentToDestroy != null
                && !explosionParentToDestroy.Destroyed)
            {
                explosionParentToDestroy.Destroy();
            }
        }
    }

    /// <summary>
    /// Операция патча CE, которая переписывает ванильные стволы под его механику. В XML пака её
    /// нет ни разу — тип нужен коду: SK.CEPatchPatch берёт его в TargetMethods и патчит
    /// AddOrReplaceCostList с AddOrReplaceResearchPrereq, чтобы запретить правку costList и
    /// researchPrerequisite из чужих файлов.
    ///
    /// Поэтому операция объявлена целиком, но ничего не делает: без CE переписывать ствол под его
    /// механику бессмысленно. ApplyWorker отдаёт true, иначе ванилла сочтёт операцию провалившейся
    /// и напишет об этом. Поля перечислены все, какие есть у CE, — чтобы XML разбирался, если
    /// операция всё же где-то встретится.
    /// </summary>
    public class PatchOperationMakeGunCECompatible : PatchOperation
    {
        public string defName;
        public string texPath;
        public bool isWeaponPlatform;
        public bool AllowWithRunAndGun = true;
        public XmlContainer statBases;
        public XmlContainer Properties;
        public XmlContainer AmmoUser;
        public XmlContainer FireModes;
        public XmlContainer weaponTags;
        public XmlContainer weaponClasses;
        public XmlContainer costList;
        public XmlContainer researchPrerequisite;
        public XmlContainer attachmentLinks;
        public XmlContainer defaultGraphicParts;

        // В 1.6 ApplyWorker объявлен protected (у CE в декомпиляции public — он собран против
        // другой версии). Берём модификатор ванильный, иначе не компилируется.
        protected override bool ApplyWorker(XmlDocument xml) => true;

        private void AddOrReplaceCostList(XmlDocument xml, XmlNode xmlNode) { }

        private void AddOrReplaceResearchPrereq(XmlDocument xml, XmlNode xmlNode) { }
    }
}

namespace CombatExtended.Utilities
{
    /// <summary>
    /// У CE это свой трекер вещей с индексом по клеткам. Паку нужен один метод, и он выражается
    /// ванильными средствами: пешки карты с проверкой расстояния.
    /// </summary>
    public static class GenClosest
    {
        public static IEnumerable<Pawn> PawnsInRange(this IntVec3 cell, Map map, float range)
        {
            if (map?.mapPawns == null)
                return Enumerable.Empty<Pawn>();

            return map.mapPawns.AllPawnsSpawned.Where(pawn => pawn.Position.InHorDistOf(cell, range));
        }
    }
}
