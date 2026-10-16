using System.Collections.Generic;
using RimWorld;
using Verse;

// Заглушки типов Combat Extended. Логики CE здесь нет и быть не должно: задача —
// чтобы дефы Core SK, написанные под CE, загрузились и работали на ванильной механике.
// Каждый тип наследует ближайший ванильный аналог, CE-поля объявлены, но не используются:
// движок их прочитает из XML и промолчит, вместо того чтобы ронять деф целиком.
namespace CombatExtended
{
    /// <summary>Ванильный Tool плюс пробитие CE (ванилла его не читает).</summary>
    public class ToolCE : Tool
    {
        public float armorPenetrationBlunt;
        public float armorPenetrationSharp;
        public Gender restrictedGender = Gender.None;
    }

    /// <summary>
    /// Ванильные verbProperties. Важные для стрельбы поля (warmupTime, range,
    /// defaultProjectile, burstShotCount) лежат в базовом классе, поэтому оружие
    /// стреляет само собой, по числам самого HSK.
    /// </summary>
    public class VerbPropertiesCE : VerbProperties
    {
        public float recoilAmount;
        public float indirectFirePenalty;
        public float circularError;
        public int ticksToTruePosition;
        public int pelletCount = 1;
        public float spreadMult = 1f;
        public bool ejectsCasings;
        public bool ignorePartialLoSBlocker;
        public bool interruptibleBurst = true;
        public float meleeArmorPenetrationBlunt;
        public float meleeArmorPenetrationSharp;
        // meleeDamageDef не объявляем: оно есть у VerbProperties, и дубль перекрыл бы его
        // Строкой, а не перечислением: значения задаёт CE, нам важно лишь прочитать без ошибки
        public string recoilPattern;
    }

    public class Verb_ShootCE : Verb_Shoot { }

    public class Verb_ShootCEOneUse : Verb_ShootCE { }

    public class Verb_ShootMortarCE : Verb_ShootCE { }

    public class Verb_ShootFlareCE : Verb_ShootMortarCE { }

    public class Verb_LaunchProjectileStaticCE : Verb_LaunchProjectile { }

    public class Verb_MeleeAttackCE : Verb_MeleeAttackDamage { }

    /// <summary>Довесок урона у CE-снаряда: в ванилле не применяется.</summary>
    public class SecondaryDamage
    {
        public DamageDef def;
        public int amount;
        public float chance = 1f;
    }

    /// <summary>
    /// Ванильные свойства снаряда. damageAmountBase, damageDef, speed и explosionRadius
    /// наследуются, так что урон и взрывы работают; поля брони CE просто игнорируются.
    /// </summary>
    public class ProjectilePropertiesCE : ProjectileProperties
    {
        public float armorPenetrationBlunt;
        public float armorPenetrationSharp;
        public float suppressionFactor = 1f;
        public float dangerFactor = 1f;
        public float airborneSuppressionFactor = 1f;
        public float gravityFactor = 1f;
        public float spreadMult = 1f;
        public int pelletCount = 1;
        public bool dropsCasings;
        public string casingMoteDefname;
        public string casingFilthDefname;
        // ai_IsIncendiary, applyDamageToExplosionCellsNeighbors и explosionDamageFalloff
        // есть у ProjectileProperties: дубль перекрывал бы ванильное поле, и значения из
        // XML не доходили бы до движка. Теперь зажигательность и спад урона работают.
        public List<SecondaryDamage> secondaryDamage = new List<SecondaryDamage>();
    }

    public class ProjectileCE : Bullet
    {
        /// <summary>
        /// Форма Impact из CE: снаряды Core SK (лазер, плазма, яд, зажигалка) переопределяют
        /// именно её. Ванилла вызывает Impact(Thing, bool), поэтому перенаправляем — иначе
        /// их логика не выполнится, а сами классы вообще не загрузятся: сборка
        /// Compatibility_CombatExtended отвергается, если переопределять нечего.
        /// </summary>
        public virtual void Impact(Thing hitThing)
        {
            base.Impact(hitThing, false);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Impact(hitThing);
        }
    }

    /// <summary>
    /// База — ванильный Projectile_Explosive, а не ProjectileCE, как в самом CE: взрыв по
    /// explosionRadius умеет только он, у Bullet этого поля никто не читает. Пока здесь стоял
    /// ProjectileCE, все ракеты, гранаты, мины и снаряды пака прилетали без взрыва, а ванилла
    /// вдобавок жаловалась на forcedMiss у стволов, которым положено бить по площади.
    ///
    /// Из-за единственного наследования Impact(Thing) приходится повторить: подклассы Core SK
    /// переопределяют именно её.
    /// </summary>
    public class ProjectileCE_Explosive : Projectile_Explosive
    {
        public virtual void Impact(Thing hitThing)
        {
            base.Impact(hitThing, false);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Impact(hitThing);
        }
    }

    // Иерархия как в CE (BulletCE : ProjectileCE), чтобы потомки из Core SK видели Impact(Thing)
    public class BulletCE : ProjectileCE { }

    // Взрывной, как в CE: иначе огненный след прилетал бы без взрыва
    public class Projectile_FireTrail : ProjectileCE_Explosive { }
}

namespace CombatExtended.Lasers
{
    /// <summary>Лазерные лучи CE объявлены как отдельные дефы; в CE это AmmoDef.</summary>
    public class LaserBeamDefCE : AmmoDef
    {
        public int lifetime;
        public float impulse;
        public GraphicData graphicData2;
    }

    public class LaserBeamCE : BulletCE { }
}
