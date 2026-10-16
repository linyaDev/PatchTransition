using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CombatExtended
{
    /// <summary>Класс патрона: в ванилле просто ярлык, нужен для ссылок из дефов.</summary>
    public class AmmoCategoryDef : Def
    {
        public string labelShort;
        public bool advanced;
    }

    /// <summary>
    /// Связка «патрон — снаряд» внутри набора.
    ///
    /// В дефах она записана короткой формой, где имя узла — это патрон, а содержимое — снаряд:
    /// &lt;Ammo_ChargedLaser&gt;Bullet_Laserbeam&lt;/Ammo_ChargedLaser&gt;. Обычным путём такой список не
    /// разобрать, поэтому CE держит для него свой загрузчик — и заглушке он нужен тоже, иначе
    /// каждая связка даёт строку «List item found with name ... that is not &lt;li&gt;».
    /// </summary>
    public class AmmoLink
    {
        public AmmoDef ammo;
        public ThingDef projectile;

        /// <summary>
        /// Разбор намеренно пустой. Без CE ammoTypes никто не читает (CompProperties_AmmoUser
        /// в заглушке инертен), а разрешать ссылки значило бы добавить ошибок на патроны и
        /// снаряды CE, которых здесь нет.
        /// </summary>
        public void LoadDataFromXmlCustom(System.Xml.XmlNode xmlRoot) { }
    }

    public class AmmoSetDef : Def
    {
        public string labelShort;
        public AmmoSetDef similarTo;
        public List<AmmoLink> ammoTypes = new List<AmmoLink>();
    }

    /// <summary>
    /// Патроны остаются обычными предметами: их можно делать, возить и продавать,
    /// но ванильное оружие их не требует — стрельба идёт без расхода.
    /// </summary>
    public class AmmoDef : ThingDef
    {
        public AmmoCategoryDef ammoClass;
        public ThingDef cookOffProjectile;
        public ThingDef detonateProjectile;
        public SoundDef cookOffSound;
        public SoundDef cookOffTailSound;
        public float cookOffSpeed = 1f;
        public float cookOffFlashScale = 1f;
        public bool spawnAsSiegeAmmo = true;
        // destroyOnDrop и generateAllowChance не объявляем: они есть у ThingDef, и дубль
        // здесь только перекрывал бы ванильное поле — значение из XML уходило бы в тень
        public bool isMortarAmmo;
        public bool menuHidden;
    }

    public class AmmoThing : ThingWithComps { }

    public class CompProperties_AmmoUser : CompProperties
    {
        public int magazineSize;
        public float reloadTime = 1f;
        public bool reloadOneAtATime;
        public bool throwMote = true;
        public float loadedAmmoBulkFactor;
        public AmmoSetDef ammoSet;

        public CompProperties_AmmoUser()
        {
            compClass = typeof(CompAmmoUser);
        }
    }

    /// <summary>
    /// Пустой комп. Нужен не только дефам: код Core SK обращается к нему напрямую
    /// (например, торговец подбирает патроны к оружию), поэтому у заглушки есть
    /// Props и набор патронов — вызов находит то, что ищет, и не падает.
    /// </summary>
    public class CompAmmoUser : ThingComp
    {
        public CompProperties_AmmoUser Props => (CompProperties_AmmoUser)props;

        public bool UseAmmo => false;

        public AmmoDef CurrentAmmo => null;
    }

    public class CompProperties_AmmoListUser : CompProperties
    {
        public int magazineSize;
        public float reloadTime = 1f;
        public AmmoSetDef ammoSet;
        public List<AmmoSetDef> additionalAmmoSets = new List<AmmoSetDef>();

        public CompProperties_AmmoListUser()
        {
            compClass = typeof(CompInert);
        }
    }

    public class CompAmmoGiver : ThingComp { }

    public class CompAmmoExploder : ThingComp { }
}
