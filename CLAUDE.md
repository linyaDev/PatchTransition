# Patch Transition

A transitional layer for Hardcore SK: keeps the pack running while it sits between RimWorld
versions and between mod sets.

The pack is written for one exact environment — Combat Extended, a given RimWorld version, its
full mod list. Remove any part of that and defs disappear, patches miss their targets, numbers
land in the wrong component. This mod fills those gaps from the outside, and every piece is
conditional on the thing it stands in for: whatever is really installed takes over.

Three kinds of gap are covered so far, each in its own conditional folder:

| Gap | Where it lives |
|---|---|
| Combat Extended absent — by far the largest | `NoCE/` (stub assembly, defs, patches) |
| Other mods of the pack absent | `NoRimatomics/`, `NoHygiene/`, `NoRimefeller/` |
| The pack's own code broken on 1.7 | moved out, see `HSKFixes-1.7` |

The third row used to live here too, in a folder attached `IfModActive="skyarkhangel.HSK"`. It is
now a separate mod, `HSKFixes-1.7`: those fixes are needed exactly when the full mod set is present,
which is when this one does nothing at all. What stays here is the log filter for the messages that
cannot be anything else, in `NoCE`.

## Why the CE part exists

Core SK does not merely *support* CE — its content is written **for** CE. Measured on the
installed pack (`Core_SK`, `Core_SK_CoreModule`, `Core_SK_DLCModule`):

| Where | Amount |
|---|---|
| XML references to `CombatExtended.*` classes | 2369, across 265 def files |
| Distinct CE types named in defs | 59 |
| CE abstract defs used as `ParentName` | 30 in the defs, plus `SpacerMediumAmmoBase` injected by a patch |
| Files in `Core_SK.dll` that hard-reference CE | 8 of 887 |

The code coupling is small and guarded; the **def** coupling is total. RimWorld drops a def
entirely when it names a type it cannot find, so without CE the pack loses its weapons, armour
and ammo before the main menu. The fix therefore has to make those names resolve, not remove them.

## How it works

### The shim assembly

`NoCE/Assemblies/CombatExtended.dll` is named exactly `CombatExtended` and holds stub types for
everything the pack's defs mention, plus the few members its assemblies call. Each inherits the
closest vanilla class, so content is not just loadable but playable:

- `VerbPropertiesCE : VerbProperties`, `Verb_ShootCE : Verb_Shoot` — guns fire
- `ProjectilePropertiesCE : ProjectileProperties` — damage comes from `damageAmountBase`, which
  the pack already sets on every projectile
- `ToolCE : Tool` — melee is vanilla
- `AmmoDef : ThingDef` — ammo stays a normal item that no weapon demands
- comps and `DefModExtension`s — inert data holders

No CE logic is reproduced. CE-only fields (armour penetration, bulk, suppression) are declared so
XML parses, and then ignored.

**Picking the vanilla base is a mechanical decision, not a formality.** `ProjectileCE_Explosive`
has to descend from vanilla `Projectile_Explosive`, not from our `ProjectileCE : Bullet`, because
`explosionRadius` is read by `Projectile_Explosive` and by nobody else — while it inherited
`Bullet`, every rocket, shell, grenade and mine in the pack arrived without an explosion, and
vanilla's own `VerbProperties.ConfigErrors` complained about `forcedMiss` on the launchers that
fire them. `IncendiaryFuel` has to be `Filth` for the same kind of reason. `Tools/AuditShim.py`
prints a base-class section for exactly this review; the differences there are intentional (vanilla
instead of CE), so the question to ask of each line is whether it is the *right* vanilla class.

Two stubs do bridge to vanilla on purpose, because Core SK's own code depends on them:
`ProjectileCE.Impact(Thing)` exists in CE's shape and is routed from vanilla's
`Impact(Thing, bool)`, so SK's laser, plasma, poison and incendiary projectiles still run their
impact logic; `CompInventory.container` hands out the pawn's vanilla inventory, so the fire
extinguisher stows the pawn's weapon somewhere real instead of nowhere.

### Why it lives in a conditional folder

The shim is in `NoCE/Assemblies/`, and `LoadFolders.xml` attaches `NoCE` only
`IfModNotActive="CETeam.CombatExtended"`. Plain `Assemblies/` is not an option: the game always
loads what it finds there, which with real CE installed would mean two different types sharing one
name. A conditional load folder gives the same guarantee and loads **much earlier**.

Earlier matters. The order inside `LoadedModManager.LoadAllActiveMods()` is `LoadModContent()` →
`CreateModClasses()` → `LoadModXML()` → `ParseAndProcessXML()`, and `LoadModContent` is where
`ModAssemblyHandler.ReloadAll()` runs `AssemblyIsUsable()` → `asm.GetTypes()` on every mod
assembly. A shim injected from a `Mod` constructor arrives in `CreateModClasses`, half a second too
late: Core SK's own CE-dependent assemblies have already been rejected whole, with
`ReflectionTypeLoadException`. Two of them exist — `Compatibility_CombatExtended` (eight SK
projectile classes) and `FireExtinguisher` — and a def whose `thingClass` cannot be resolved ends
up `null`, which later crashes `ReadingPolicyDatabase` when a game starts.

Three details make the folder route work, all verified against the game's own code:

- `ModAssemblyHandler.ReloadAll()` takes its DLLs from
  `ModContentPack.GetAllFilesForModPreserveOrder(mod, "Assemblies/")`, i.e. from **every** load
  folder of the mod — not just the root.
- `LoadFolder.ShouldLoad` tests `ModLister.AnyModActiveNoSuffix`, so the `_steam` suffix on
  subscribed packageIds is handled for us. (The old hand-rolled check had to match by assembly
  name for exactly that reason.)
- If `LoadFolders.xml` has no entry for the running version, `ModContentPack.InitLoadFolders` falls
  back to the highest declared version not above the current one. One `<v1.6>` block therefore
  covers 1.7 as well.

`NoCEMod` (`Source/PatchTransitionNoCE/NoCEMod.cs`) lives in that same folder and is now only a
voice: it reports which assembly actually won — the shim is recognised by the marker type
`CombatExtended.PatchTransitionShim` — and installs the log filter. It deliberately has **no**
fallback that hand-loads the shim: living in `NoCE` itself, it would not load either if the folder
did not attach, and by `CreateModClasses` there is nothing left to rescue anyway.

### The abstract bases

`NoCE/Defs/CEAbstractBases.xml` supplies the 31 abstract defs the pack inherits from (`AmmoBase`,
`SpacerMediumAmmoBase`, `BaseBulletCE`, `TurretBase`, `AmmoRecipeBase` and friends). They are empty
shells hung off the nearest vanilla base so the children get sane defaults.

They are plain `<Defs>`, not patch operations: the folder is already conditional, and defs are in
the XML tree from the start, before any mod's patches run over it. Some of them need more than a
name — Core SK rewrites `ParentName` with `PatchOperationAttributeSet` and adds `<li>` into
`tradeTags`, so the node has to be there for the operation to find anything.

### The pack leans on more than CE

Core SK references its whole mod list unconditionally, so a missing mod leaves the same kind of
hole CE's absence does. Each is answered the same way — a conditional folder holding exactly what
the pack names, nothing more:

| Folder | Condition | Contents |
|---|---|---|
| `NoRimatomics` | `Dubwise.Rimatomics` absent | defs: `Radiation` (52 lines: it is in `statBases` all over the pack), the `NuclearFallout` condition, the `NuclearWaste` category |
| `NoHygiene` | `Dubwise.DubsBadHygiene` absent | def: `Diarrhea`, which the pack's activated carbon hands out via `HediffGiver_Random`. Assembly `BadHygiene`: `MapComponent_Hygiene` with both water-grid methods |
| `NoRimefeller` | `Dubwise.Rimefeller` absent | assembly `Rimefeller`: `OilGrid` and the two work givers |

The def side is much smaller than CE's — the pack names 0 Rimatomics types and 1 Dubs type in its
*defs*. The **code** side is not, and it bites differently: a missing type does not drop a def here,
it takes out whole features of the pack.

- `Temp-MechJobFix.dll` is a `[StaticConstructorOnStartup]` class whose first statement is
  `typeof(Rimefeller.WorkGiver_OperateResourceConsole)`. Without the type the constructor throws
  `TypeLoadException` and the fix — colony mechs must not operate oil consoles — never installs.
- `HSK_OdysseyChanges.dll` (the DLC module) patches two foreign methods by *name*,
  `Rimefeller.OilGrid.GenerateFields` and `DubsBadHygiene.MapComponent_Hygiene.RegenWaterGrid`.
  `Harmony.PatchAll` throws on the first missing target, which kills the module's whole `Mod` class,
  i.e. every Odyssey fix it carries. Supplying only one of the two just moves the failure to the
  other — they have to be stubbed together.

Two details decided the shapes, and both would have been silent:

- `HasJobOnThing` is **overridden**, not inherited. Inherit it and `AccessTools.Method(type,
  "HasJobOnThing")` walks up to vanilla `WorkGiver_Scanner.HasJobOnThing` — so the pack's prefix,
  which returns `false` for colony mechs, would be attached to the base of every scanning work
  giver in the game and stop mechs working at all.
- `MapComponent_Hygiene` is **abstract**. `Map.FillComponents()` instantiates every
  `AllSubclassesNonAbstract` of `MapComponent` on every map, so a concrete stub would write
  `<li Class="DubsBadHygiene.MapComponent_Hygiene">` into the save, for the real mod to later load
  with empty fields. Harmony patches methods on abstract classes without complaint.
- `OilGrid` is `IExposable`, not a `MapComponent`, and it keeps its own `public Map map` — which is
  what the pack's prefix asks for as `___map`. Read the real type before guessing the base.
- **Read the attribute blob, not the method name in the class name.**
  `Patch_MapComponent_Hygiene_RegenWaterGrid` carries *two* class-level `[HarmonyPatch]` attributes,
  for `RegenWaterGrid` and `RegenDeepWaterGrid`. Harmony merges class-level attributes into one
  target, so the pack in fact patches only the second — and a stub with just `RegenWaterGrid`
  produced the identical `Undefined target method` error as no stub at all. ILSpy prints
  `/*Could not decode attribute arguments*/` for these; `ilspycmd --il-sequence-points` and reading
  the blob bytes is what settled it.

These stubs are not CE work and, like `SKSettingsRevive`, live here only because cutting CE out is
itself temporary. Enabling the real mods remains the better answer whenever they run.

## Hard rule: reversibility

**Installing the real thing must restore the original behaviour with no leftovers.** Everything
that stands in for Combat Extended lives under `NoCE/`, and with CE active that folder does not
exist as far as the game is concerned — no shim, no abstract bases, nothing to clash. The same
holds for `NoRimatomics/`, `NoHygiene/` and `NoRimefeller/`: each is attached only
`IfModNotActive` for the mod it
replaces.

Any new piece follows that rule — its own conditional folder, named after what it stands in for.
**The root carries no code at all.** Both assemblies sit in conditional folders, each under the
condition that makes it meaningful:

| Assembly | Folder | Loads when | Holds |
|---|---|---|---|
| `PatchTransitionNoCE.dll` | `NoCE/` | CE absent | the shim's diagnostics, `LogNoiseFilter` |

One assembly, one folder, one condition. There used to be a second — the pack's own 1.7 breakage —
and it now lives in `HSKFixes-1.7`.

So the root is `About/`, `LoadFolders.xml` and `Tools/` — nothing the game loads unconditionally.

Never patch or edit Core SK itself.

## Layout

```
About/About.xml                        name, packageId linya.patchtransition, loadBefore skyarkhangel.HSK
LoadFolders.xml                        attaches NoCE, NoRimatomics, NoHygiene, NoRimefeller —
                                       each IfModNotActive for the mod it stands in for — and
                                       nothing else: the root carries no code
NoCE/Assemblies/CombatExtended.dll     the stub assembly, loaded by the game itself
NoCE/Defs/CEAbstractBases.xml          31 abstract parents the pack inherits from
NoCE/Defs/CEStats.xml                  17 StatDefs, declared and hidden, read by nobody
NoCE/Defs/CEBodyPartGroups.xml         19 BodyPartGroupDefs, mostly weapon parts
NoCE/Defs/CEMisc.xml                   damage types, apparel layers, item categories,
                                       hediffs, rocket flecks, gun sounds
NoCE/Defs/CEItems.xml                  CE's grenades, flare gun, rigging, turret odds and ends
NoCE/Defs/CEAmmoGraph.xml              ammo sets and classes, the projectiles HSK's guns
                                       fire, FSX and prometheum, unfinished things
NoCE/Patches/CEBodyGroups.xml          hangs CE's _2 part groups off the vanilla ones, so the
                                       animals' ToolCE entries find a body part
NoCE/Patches/CEComps.xml               puts CE's empty armour-durability and ammo-user comps
                                       where the pack's own patches expect to find them
NoRimatomics/Defs/RimatomicsStubs.xml  not about CE: Radiation stat, NuclearFallout condition
NoHygiene/Defs/HygieneStubs.xml        not about CE: the Diarrhea hediff
NoHygiene/Assemblies/BadHygiene.dll    the one type HSK_OdysseyChanges patches by name
NoRimefeller/Assemblies/Rimefeller.dll the three types the pack's own assemblies reach for
Tools/AuditShim.py                     compares every stub field against the real CE class
Tools/CERefs/                          lists the CE types and members the pack's assemblies
                                       reference, with signatures — the other half of the audit
Tools/MemberDump/                      dumps an assembly's members with IL sizes, for comparing
                                       the pack's CE build against upstream
Tools/CEPatches/                       what we change inside CE: the patch series, the disable
                                       list, and the scripts that reproduce both
Tools/CopyCETextures.ps1               refills NoCE/Textures from an installed CE
Tools/ce-textures.txt                  the 59 texture paths our defs and HSK's ask CE for
ModsConfig.xml                         the mod list everything here was measured on; copy over
                                       the profile's own Config/ModsConfig.xml to reproduce
NoCE/Textures/                         CE's art for those paths — gitignored, see below
NoCE/Assemblies/PatchTransitionNoCE.dll  the shim's own voice and its log filter
Source/PatchTransitionNoCE/            NoCEMod.cs — the NoCE folder's Mod class
                                       LogNoiseFilter.cs — hides Core SK's failed patches to CE's
                                       own defs and vanilla's forcedMiss warning on explosives
Source/CombatExtendedShim/             Weapons.cs (tools, verbs, projectiles)
                                       Ammo.cs (ammo defs, ammo comps)
                                       Defs.cs (comps, def extensions, things, buildings)
                                       Tactical.cs (shim marker, CombatExtended.AI base class)
                                       Utilities.cs (the five types Core_SK.dll calls directly)
Source/RimefellerShim/                 Rimefeller.cs — OilGrid, two work givers
Source/BadHygieneShim/                 BadHygiene.cs — MapComponent_Hygiene
```

Nothing CE-shaped belongs in the root. Root content loads unconditionally, which is exactly what
the reversibility rule forbids.

### The textures are borrowed, not shipped

HSK's own ammo and projectile defs name CE's art (`Things/Ammo/Pistol/FMJ`, `Things/Resources/FSX`,
`UI/Icons/ThingCategories/*`). The defs are fine without CE; there is simply nothing to draw, which
is where the ~100 `Texture2D` / `No textures found` / `MatFrom with null sourceTex` lines came from.
`Tools/CopyCETextures.ps1` copies exactly the paths in `Tools/ce-textures.txt` — gathered from the
log, i.e. the paths the game actually looked for — out of an installed CE, including its `HSK` and
DLC folders, into `NoCE/Textures`. 130 files, under a megabyte.

That folder is **gitignored on purpose**: it is someone else's art, and CE's distribution carries
no licence file and says nothing about rights in `About.xml`. Copying it into one's own game is
fine; publishing it from this repository is not ours to do. Which also means the pretty version
only works for someone who has CE installed somewhere — run the script, or live with the
placeholders.

## Working setup

**Two game copies.** Mods are developed in `D:\Mods\` and linked into each copy's `Mods` folder by
junctions, so there is one source of truth and no copying:

| Copy | Version | Use |
|---|---|---|
| `D:\SteamLibrary\steamapps\common\RimWorld` | 1.6.4871 rev590 | the copy played in, and what the projects build against |
| `D:\RimWorld_HSK2` | 1.6.4871 rev590 | second client for multiplayer tests, own SaveData |

The Steam copy was on 1.7 for a while (branch `unstable`) and is back on stable 1.6; the 1.7 section
below was written against that state. The standalone `D:\RimWorld_HSK` and `D:\RimWorld_HSK_1.5`
copies were deleted on 2026-10-15 — anything that referred to them now means the Steam copy.

`RimWorld_HSK2` is a copy of the Steam install made with `robocopy /E /XJ`, so its `Mods` folder
holds its own 27 junctions to the same `D:\Mods\*` targets. A mod added to one copy has to be linked
into the other as well:

```powershell
New-Item -ItemType Junction -Path 'D:\SteamLibrary\steamapps\common\RimWorld\Mods\PatchTransition' -Target 'D:\Mods\HSKVanilla'
New-Item -ItemType Junction -Path 'D:\RimWorld_HSK2\Mods\PatchTransition' -Target 'D:\Mods\HSKVanilla'
```

**The log does not follow `-savedatafolder`.** `RimWorld_HSK2` keeps its saves and configs in its
own `SaveData`, but Unity writes `Player.log` to the shared profile folder, so two copies running
at once would overwrite each other's log. Its shortcut therefore passes
`-logFile D:\RimWorld_HSK2\Player.log`, and the Steam copy keeps writing to the profile folder as
usual — two clients, two logs.

**The source folder is still named `HSKVanilla`.** The mod was renamed to Patch Transition, but
the working copy kept its old directory name on purpose — nothing reads it. RimWorld sees the
junction's name, which is `PatchTransition` in both game copies, and the packageId lives in
`About.xml`. So the target path above is the old name by design, not an oversight.

A junction whose target no longer exists makes the game fail with a black screen at startup
(`ModLister` throws `DirectoryNotFoundException`), so remove the link before renaming or deleting
a mod folder. Delete the link itself, never through it: `[IO.Directory]::Delete($path, $false)`.

**Where the log is.** The profile folder is
`C:\Users\Linya\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\`, and the two versions
write to different places:

| Version | Log |
|---|---|
| 1.6 | `Player.log` in the profile root (previous run: `Player-prev.log`) |
| 1.7 | `Logs\Player.log` (previous run: `Logs\Player-prev.log`) |

1.7 logs are large — 200k lines is normal — so read them by filtering, not whole. Error lines are
tagged `ERROR`, and each is followed by stack frames starting with `at `, which are worth excluding
when counting:

```powershell
$f = 'C:\Users\Linya\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Logs\Player.log'
Select-String -Path $f -Pattern 'ERROR' |
  ForEach-Object { $_.Line.Trim() } | Where-Object { $_ -notmatch '^\s*at ' } |
  Select-Object -Unique
```

**Mod list presets** live in `<profile>\ModLists\*.rml`; the active list is
`<profile>\Config\ModsConfig.xml`. The CE-less test profile is `Min List.rml`: Harmony, HugsLib,
`linya.patchtransition`, the three Core SK modules and `AlienRaces` — nothing else. AlienRaces is not
optional: without it the 1.7 run loses every texture (see the 1.7 section). `Min list 1.7.rml` is
the large list. Back up `ModsConfig.xml` before editing it, and only while the game is closed; the game
rewrites it on exit, and it will silently drop mods it could not find at startup.

**Caches worth knowing about (1.7).** `<profile>\TextureCache\v23\<hash of mod set>\` holds one zip
per mod. A changed mod set means a different hash, so a stale cache can look like missing textures.
`BundleCache` and `ShaderCache` sit next to it.

## How to extend it

The log is the worklist. Run the pack without CE and mine the log file named above:

- `Could not find type named CombatExtended.X` → the stub type is missing; add it with the closest
  vanilla base.
- `XML error: <field> doesn't correspond to any field in type X` → the stub lacks a field the pack
  writes. Declaring it is enough; it does not have to do anything.
- `XML format error: Raw text found inside a list element` → a stub field is typed as a list where
  the defs write a bare value (this is how `bodyShape` was caught).
- `Could not find parent node named "X"` → an abstract base is missing from `CEAbstractBases.xml`.
- `Could not resolve cross-reference ... named X` → a **def** of CE's own is missing, not a type.
  This is now the main category; see below.

Check whether a name really belongs to CE before adding it — find the CE folder by its **root**
`packageId` (`CETeam.CombatExtended`); matching the string anywhere in About.xml also hits mods
that merely depend on CE.

Four traps worth knowing, each already paid for once:

- **A wrong field *type* is worse than a missing field.** A missing field is one log line; a
  mistyped one throws mid-parse and takes the whole def with it, with nothing about CE in the
  message. `Regenerates` declared `float` instead of `bool` cost 16 race defs — Humanlike,
  mechanoids, insectoids, cats, canines — and all the log said was
  `Exception parsing System.Single from "true"`. **Run `python Tools/AuditShim.py` after touching
  the shim**; it compares every stub field against the real CE class and prints the mismatches.
- **Do not invent stub fields.** Read the real thing in `D:\Mods\.source\CombatExtended\`. The
  first `ProjectileFleckData` had `fleckDef`/`speed`/`rotationRate`, all made up; CE writes
  `fleck`, `flecksPerEmission`, `cutoffTickRange`, and the defs with them kept failing.
- **Never shadow a vanilla field.** `destroyOnDrop` on `AmmoDef` or `ai_IsIncendiary` on
  `ProjectilePropertiesCE` compile with a CS0108 warning and quietly break the def: the XML loader
  writes the derived field and the engine reads the base one. Watch the build warnings.
- **Field or property matters.** Core SK's assemblies read `CompInventory.container` as a field
  (`ldfld`), so declaring it as a property gives `MissingFieldException` at runtime.
- **A list whose items are not `<li>` needs `LoadDataFromXmlCustom`.** CE writes some lists in a
  short form where the node name carries data — `<Ammo_ChargedLaser>Bullet_Laserbeam</...>` in
  `ammoTypes`, `<ArmorRating_Sharp>0.5</...>` in `PartialArmorExt`. Without the method the engine
  says "List item found with name X that is not `<li>`" per entry. Ours are deliberately empty:
  nothing reads those lists without CE, and resolving the names would only add missing-def errors.

### The assembly worklist

The log is not enough for one category: a CE type that only the pack's **code** needs. A missing
type there does not drop a def — it throws `TypeLoadException` from inside whatever method touches
it, and the message names neither CE nor the patch class. That is how `SK.CEPatchPatch` quietly cost
the pack every single one of its Harmony patches: it asks for
`CombatExtended.PatchOperationMakeGunCECompatible` in `TargetMethods`, the type would not load, and
`Harmony.PatchAll` died on that class.

`Tools/CERefs` answers it directly — it reads the TypeRef/MemberRef tables of the pack's assemblies
and prints every name in the `CombatExtended` namespace together with the signature it is called by:

```
dotnet run --project Tools/CERefs -- "<Mods>/Core_SK" | cut -f1 | sort -u > wanted.txt
ilspycmd -l c NoCE/Assemblies/CombatExtended.dll | sed 's/^Class //' | grep ^CombatExtended > shim.txt
comm -23 wanted.txt <(sort shim.txt)
```

On 2026-10-13 that diff was five types, all in `Core_SK.dll`, and the member signatures told exactly
what to write (`NoCE/.../Utilities.cs`): `CE_DamageDefOf.Electrical` (a `[DefOf]`, and the def is
already in `CEMisc.xml`), `DefUtility.IsAOEWeapon`, `GenExplosionCE.DoExplosion` — 41 parameters
whose first 36 are vanilla `GenExplosion.DoExplosion` exactly, so the stub forwards —
`PatchOperationMakeGunCECompatible`, and `Utilities.GenClosest.PawnsInRange`. Run the sweep whenever
the pack updates; it is cheap and it catches what no log line will.

### What the pack's CE copy actually is

Worth knowing before guessing at it, and measured rather than assumed: the pack's `CombatExtended`
folder was compared file by file against upstream `v1.6.7.0.0`, the release whose `modVersion`
(`16.7.0.0`) its `About.xml` carries. Upstream lives at `CombatExtended-Continued/CombatExtended`;
the release zip is 22 MB.

| | |
|---|---|
| files, upstream → pack | 8774 → 3915 |
| deleted outright | `ModPatches/` 3819 (CE's compat patches for third-party mods), `Languages/` 937, `Textures/` 296, 31 files under `Patches/Core` and the DLC folders |
| disabled by renaming to `.xml.bak` | 222 — 219 of them ammo defs (`Defs/Ammo/Pistols` 35, `Rifle` 33, `HighCaliber` 30, `Rocket` 22, `Advanced` 21, `Shell` 17, `GENERIC` 14, modded sets, …), plus `Recipes_Production` and two `ResearchProjects` files |
| added by the pack | 100 XML, `AssembliesCompat/*.dll`, an empty `HSK/` folder that nothing references |
| genuinely edited (not whitespace) | 346 files, of which 311 are translations; **35** real content changes — ammo numbers, a few `Patches/Core` and DLC patches, `LoadFolders.xml`, `About.xml` |

So content *is* switched off, but by renaming files, not by commenting: RimWorld reads only `.xml`, so
an `.xml.bak` does not exist as far as the game is concerned. The pack keeps editing those disabled
files when it re-vendors CE, which is why they stay in sync with upstream.

**The code was not cut.** `Tools/MemberDump` dumps an assembly's members with their IL sizes; diffing
the two `CombatExtended.dll` files gives 5573 members upstream against **5630** in the pack — 59
members only in the pack, and the only two missing are compiler-generated lambdas. The additions are
a whole subsystem: `AdvancedAI_GenerateUtility` (18), `AdvancedAI_TakeAndEquipUtility` (13),
`Settings` (12), `JobDriver_LootItem` (6), `AdvancedAI_MoveItemUtility` (4), plus
`IgnoresAccessChecksToAttribute`, i.e. it is built with a publicizer. None of those types are named
in any XML, in the pack's defs or CE's own, so the shim needs nothing for them.

One trap in that comparison: 2824 of 3369 common methods differ in IL size, which says nothing about
edits. The pack's assembly was built 2026-08-02 and the release it matches by `modVersion` was
published 2025-09-21 — it is a rebuild from a later revision, so body sizes are not comparable. Only
the presence or absence of members is.

What this means here: the ammo graph in `NoCE/Defs/CEAmmoGraph.xml` is thin on purpose. The pack runs
its own ammo economy and disables CE's, so unresolved references to CE ammo would be unresolved with
CE installed too — those are not gaps to fill.

### Upstream CE, built from source

Third-party mods we carry forward ourselves are meant to live in `D:\Mods\Updated\`, one folder
each, each junctioned into the game. CE is a clone of `CombatExtended-Continued/CombatExtended`,
and it is still at **`D:\Mods\CombatExtended`**: the move into `Updated/` fails with
`Access to the path is denied` while an editor or an Explorer window holds the folder, so it waits
for a moment when nothing has it open. `Updated/` exists and is empty.

**The junction must be named `CombatExtended`, not `CombatExtended-git`.** Core SK's
`SK.CEPatchPatch` disables CE's `costList` and `researchPrerequisite` operations for everyone and
warns only for source files whose path contains neither `/Core_SK/` nor `/CombatExtended/`. With the
`-git` suffix the test missed, and CE's own files were reported as a foreign mod's — three
`WarningOnce` lines about disabled research patches, with nothing behind them, since the prefix
returns `false` either way. The folder name is what the pack reads; `ModsConfig` goes by packageId,
so renaming is free.

Two things about building it:

- The 1.7 work is on the **`1.7-Test`** branch (`Initial 1.7`, 2026-10-12), and
  `Source/Directory.Build.props` there targets `net10.0` with `LangVersion 14`, so it needs the
  .NET 10 SDK — installed locally at `D:\Mods\.tools\dotnet` (10.0.401) to avoid touching the
  system one. `global.json` pins 9.0.112 with `rollForward: latestMinor`, which accepts neither 8
  nor 10; it is pinned to the installed version locally, and that edit is uncommitted on purpose.
- References are `..\..\..\..\Assembly-CSharp.dll`, `UnityStubs.dll` and `Waylith.dll` — four
  levels up from the project, i.e. **the game root**. So the build has to run through the junction
  path, not through the clone's own location:

```
D:\Mods\.tools\dotnet\dotnet.exe build   "D:\SteamLibrary\steamapps\common\RimWorld\Mods\CombatExtended\Source\CombatExtended\CombatExtended.csproj" -c Release
```

That produces `Assemblies/CombatExtended.dll` (1.2 MB); `CombatExtendedLoader.dll` is committed
prebuilt, and the compat projects are for third-party mods we do not run.

Upstream's own 1.7 port is partial, in the same spirit as the pack's `.xml.bak` trick but in the
project file: the branch's `CombatExtended.csproj` carries `<Compile Remove="AI\**" />` plus a list
of individual exclusions — `CompSquadBrain`, the loadout main tab, `ProjectileCE_Explosive_RL`,
several Harmony patches. Whatever those files do is absent from the build.

**Would the pack run on it?** Measured with `Tools/MemberDump` against the built 1.7 assembly (688
types):

| | |
|---|---|
| CE types the pack's **code** needs (`Tools/CERefs`) | 20 — **all present** |
| CE types the pack's **defs** name | 59 — 56 present |
| missing | `Building_AutoLoaderCE`, `CompProperties_Smoke`, `SmokeInhalationImmuneExtension` |

The first is a false alarm: CE's class is spelled `Building_Auto**l**oaderCE`, and CE's own defs
write it with the capital L too — `GenTypes.GetTypeInAnyAssemblyRaw` calls `GetType` with
`ignoreCase: true`, so either spelling resolves.

The other two are real, and they are the pack's own bug rather than a gap of ours:
`CombatExtended.CompProperties_Smoke` and `CombatExtended.SmokeInhalationImmuneExtension` exist in
**no** CE build — not upstream 1.6.7.0.0, not upstream 1.7, not the pack's own CE — and not in
vanilla either. They are named in `Defs/ThingDefs/VDummy.xml`,
`Defs/BodyParts/BodyParts_Cybernetic.xml` and one autogen patch, and the CE-active run confirmed
the cost: `Exception loading def from file BodyParts_Cybernetic.xml: Could not find type named
CombatExtended.SmokeInhalationImmuneExtension`.

Both therefore live in `HSKFixes-1.7`, not in the CE shim: the
breakage does not depend on CE being absent, so neither should the fix. They keep CE's namespace,
which is safe because CE declares neither name, and the `compClass` points at a small inert comp of
our own (`ThingComp` is abstract, and the shim's `CompInert` is not loaded when CE is).

### Our edits to CE live in Tools/CEPatches

The pack does not run against untouched CE, and the reason decides where a fix belongs. Measured on
one node and it settled the whole question: `statBases/MarketValue`, which the pack's generated
patches replace on 46 ammo defs, exists in **neither** upstream 1.6 nor 1.7 — only in the pack's own
CE copy. It is HSK's edit inside CE, so it has to be repeated inside CE.

| Cause | Where it is fixed |
|---|---|
| a node or def only the pack's CE copy has | in CE |
| the pack names something no CE has | `HSKFixes-1.7` |
| the pack's defs need CE types and CE is absent | here, `NoCE/` |

Core SK itself is never touched.

The CE clone lives outside this repository, so everything we change in it is kept here:
`Tools/CEPatches/` holds the whole thing as one `git format-patch` commit, the list of files to
disable, and two scripts — `PortHSKEdits.py`, which ports HSK's edits onto any CE version and
applies the disable list, and `FindRefHolders.py`, which rebuilds that list from a run's log. The
README there has the commands and the full inventory of what changed.

Two numbers from the first run against real CE 1.7, both of that origin: 47 patch operations missing
their target node, 45 cross-references dangling. The ported edits answer the first, the 15 disabled
ammo files the second — `.xml.bak` is the pack's own way of switching CE content off, since RimWorld
reads only `.xml`.

**Compare against the same version number.** The pack's `About.xml` says `modVersion 16.7.0.0`,
which is upstream's `v1.6.7.0.0`; diffing against anything else mixes HSK's edits with CE's own
development, and 2824 of 3369 methods differ between builds for that reason alone.

### The def worklist

Extract it from the log rather than by eye — the names that matter are the intersection of
"unresolved" and "defined by CE":

```powershell
$f = 'C:\Users\Linya\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Logs\Player.log'
# unresolved references, with counts
Select-String -Path $f -Pattern 'No [A-Za-z.]+Def named (\w+)','cross-reference to [A-Za-z.]+Def named (\w+)'
# then keep only names that appear as <defName> under the CE mod folder
```

A missing cross-reference is not a missing type: the def still loads, the field just goes `null`.
So the question per name is always "what breaks when this is null", not "how many log lines".

On 2026-10-13 the backlog was **138 names / 3068 log lines**; 130 of them are now supplied, in two
rounds. First the pure data — stats, body part groups, damage types, apparel layers, item
categories, hediffs, flecks, gun sounds (~2800 lines). Then the ammo graph: ammo sets and classes
as empty shells, plus the things whose `null` actually broke content — the projectiles HSK's own
guns name in `defaultProjectile`, `UnfinishedTurretGun` (21 turret recipes), `UnfinishedShield`,
`UnfinishedAmmo`, `FSX`, `Prometheum`, and the four ammo items HSK's scenarios hand out at start.

**CE's own items turned out to be mandatory.** They were left out at first as "weapons and apparel
with their own mechanics — faking them is rewriting CE", and the cost looked like a few recipes
with a null product. It was the whole game: starting a colony runs `ApplyPlayerStartingResearch`,
vanilla's `ResearchProjectDef.UnlockedDefs` does
`.SelectMany(x => x.products.Select(y => y.thingDef)) orderby x.label`, and a null product
dereferences into `NullReferenceException` → `ErrorWhileGeneratingMap` → back to the main menu.
`NoCE/Defs/CEItems.xml` therefore supplies the four grenades, the flare gun, the tac rigging, the
backpack and the turret odds and ends, on vanilla bases with CE's numbers and art.

The lesson generalises: **"only a cross-reference" is not the same as "only cosmetic"**. Ask what
dereferences the null, not how many lines it prints.

## Build

```
dotnet build Source/CombatExtendedShim/CombatExtendedShim.csproj -c Release
dotnet build Source/PatchTransitionNoCE/PatchTransitionNoCE.csproj -c Release
dotnet build Source/RimefellerShim/RimefellerShim.csproj -c Release
dotnet build Source/BadHygieneShim/BadHygieneShim.csproj -c Release
```

Every project picks the first game path that exists, so they build on either machine. They
currently point at `D:\RimWorld_HSK` (1.6.4633), because the Steam copy has moved to 1.7. Each
writes straight into the load folder it belongs to; only the first two need a rebuild in normal
work, the other two are a few lines that will not change.

## Known limits

- **Balance is CE's, mechanics are vanilla's.** Armour penetration, bulk and suppression are read
  by nobody now, so combat matches neither CE nor vanilla. Rebalancing is a separate job.
- Core SK patches defs that belong to CE itself. Without CE those operations fail — 1036 lines of
  1057, two thirds of the whole log. The **operations** are unfixable: they edit the insides of
  CE's defs (`.../ammoTypes/Ammo_Arrow_Plasteel`, `.../fixedIngredientFilter/thingDefs/li[text()=
  "Prometheum"]`), so supplying empty defs would not help — our own defs exist and still miss 69
  of them. Making every patch land would mean reproducing CE's def tree, i.e. rewriting CE.
  The **messages** are gone: `Source/PatchTransitionNoCE/LogNoiseFilter.cs` suppresses exactly those
  whose `sourceFile` lies in the pack's CE patch folders, and reports the count in one line. The
  other 21 failures stay visible — they are real.
- **~86 lines of `has incorrect forcedMiss settings`** — the price of making CE's explosive
  projectiles descend from vanilla `Projectile_Explosive`. Vanilla wants `forcedMissRadius` on
  exactly the verbs that fire explosives; CE replaced that scatter with its own ballistics, so
  HSK's 38 launchers, mortars and howitzers have none. `VerbProperties.CausesExplosion` is read
  by `ConfigErrors` and by nothing else in the game, so the warning is advisory — and the
  alternative, writing `forcedMissRadius` onto those verbs, would scatter their shots at random.

## State on RimWorld 1.7

1.7 replaced Unity with an in-house engine (Waylith/Brutal, Vulkan, .NET 10), and the pack is not
ported — but it runs much further than the first attempt suggested.

**The texture catastrophe was a missing mod, not the engine.** With `AlienRaces` added to the
CE-less list, `TextureND` errors went 203 → 0, `MatFrom with null sourceTex` 1971 → 12, and
"recovering from errors" disappeared. What was written up as "1.7 registers no textures at all" was
the downstream of `AlienRace.ThingDef_AlienRace` failing to load. The run then reaches
`[HugsLib] Quickstarter generating map`, i.e. the pack starts a game without CE.

Still broken there, none of it ours:

- **`SK.Controller` does not get constructed at all.** Its constructor opens with two calls to
  `UnityEngine.Application.SetStackTraceLogType`, a method the new engine does not have, so it dies
  on the first line: no `Instance`, no `Settings` for the 17 files that read them, no
  `HarmonyBase.InitPatches()`, no `ModPatches.Init()` — the pack's whole C# layer. The visible
  symptom was `SK.RaidHelperComponent.MapComponentTick` throwing `NullReferenceException` every 250
  ticks, all game long.

  `HSKFixes-1.7` builds the object instead of patching the
  constructor, and it does so from **our own `Mod` constructor** — which runs inside the same
  `CreateModClasses` loop, earlier, because this mod loads before the pack. `CreateModClasses` only
  instantiates a `Mod` type `if (!runningModClasses.ContainsKey(type))`, so registering the
  hand-built instance first means the pack's constructor is never called at all. That is why
  `Error while instantiating a mod of type SK.Controller` is gone from the log: not filtered —
  there is nothing left to fail. The phase is exactly the one the pack did this work in itself.
  The old hook on `LoadModXML` stays as the fallback for a load order where the pack goes first.

  That revive, and everything else about the pack's own code, now lives in `HSKFixes-1.7` —
  including the line that reports what stands where CE should be, which only a folder loaded
  alongside the pack can print. Any Harmony patch — prefix, postfix or finalizer — first reads and re-emits the
  original IL, and that IL contains the call to the missing method; what the new runtime makes of
  that is unknown, whereas it is known that the constructor does reach its first line and no
  further. So: create the instance without its constructor (`GetUninitializedObject`, resolved
  reflectively because the holder type differs between runtimes), set the private `Mod.intContent`
  the base constructor would have set, then repeat what the rest of the body does — `Instance`,
  `GetSettings<ProjectSettings>()`, the three error-check funcs, the ModStreamer flag,
  `InitPatches`, `ModPatches.Init`, the two long events — each step in its own `try`, which the
  pack's all-or-nothing constructor cannot offer. `Controller` has no instance fields of its own
  (checked in IL), so nothing is left unset. It also registers in `LoadedModManager`'s
  `runningModClasses`, which is where the mod-settings window looks.

  The patches are applied class by class rather than through `HarmonyBase.InitPatches`, which is
  `instance.PatchAll(own assembly)` plus `SpecialPatches.Init()`. `PatchAll` throws on the first
  class that will not apply and loses the entire rest of the assembly — and that is exactly what
  happened on the first run: `SK.CEPatchPatch` asked for a CE type the shim did not have, and not a
  single Core SK patch was applied. The type is there now, but the fragility is general, so the
  revive walks the same types in the same order (`AccessTools.GetTypesFromAssembly`, what `PatchAll`
  uses) and calls `CreateClassProcessor(type).Patch()` on each in its own `try`, then reports how
  many classes applied and names those that did not. Only the pack's `HarmonyDebug` reporting event
  is not reproduced — that is diagnostics, not patches.

  **The filter is not optional.** `PatchAll` is
  `GetTypesFromAssembly(asm).DoIf(t => t.HasHarmonyAttribute(), t => CreateClassProcessor(t).Patch())`,
  and the condition has to be copied with it: `PatchClassProcessor.Patch()` in the current Harmony
  does not return early on an unannotated type, it goes straight to looking up auxiliary methods by
  name. Every `JobDriver` subclass inherits an instance `Cleanup(JobCondition)` from vanilla, which
  Harmony then invokes as if it were static — `Non-static method requires a target.` Without the
  filter, 50 of the pack's job drivers plus two private nested classes `PatchAll` never touches were
  reported as failures. Ours re-implements the condition rather than calling `HasHarmonyAttribute`,
  which 1.6's Harmony — the one this builds against — does not have yet.

  Measured on 2026-10-13 with the filter in place: **119 patch classes applied, one failed**. (The
  1438 reported before the filter was not a count of patches: `PatchClassProcessor.Patch()` returns
  an empty list rather than null for the unannotated types it should never have been given, and
  every one of those counted. 120 is the real number of Harmony patch classes in `Core_SK.dll`.)

  The single failure is honest — `SK.SuppressionPatch` is a transpiler over
  `ProjectileCE.ApplySuppression`, i.e. over CE's suppression code. The shim's `ProjectileCE` has no
  such body to rewrite, and giving it an empty method would only move the failure from "undefined
  target method" into the transpiler's own `CodeMatcher`. Without CE there is no suppression to
  patch.

  This is what the old note here said needed its own round of testing: the pack's patches now
  actually run on 1.7. `SKSettingsRevive` stays underneath as the narrow fallback — if the revive
  fails, the settings are still read, which is what stopped the `NullReferenceException` flood.
  Removing one call in `HSKFixesMod` turns the whole thing off again.
- `ArchitectSense.DesignatorUtility` throws from `HarmonyLib.AccessTools.Field` on the new runtime.
- `RimWorld.ReadingPolicyDatabase` throws `NullReferenceException` out of
  `GenTypes.SameOrSubclassOf` — a def whose `thingClass` did not resolve is `null` in that list.
  With the minimal list that includes defs pointing at `FertileFields`, `Mending` and
  `AdaptiveStorage`, which simply are not loaded.
- `ResearchTreeSK` is a separate mod, absent from the minimal list; it accounts for the ~400
  `ResearchTreeSK.ResearchTreeSKModExtension` errors and most of the 238 unresolved
  `ResearchProjectDef` names. Not a CE problem.
- **Two of the pack's mods declare `__instance` of the wrong type**, and 1.7's Harmony refuses the
  binding: `Unrelated classes cannot describe the same non-null object`. Each throw costs far more
  than the one patch, because mods apply their patches in sequence inside a single static
  constructor — the exception takes the whole tail with it:

  | Mod | Broken patch | What the tail loses |
  |---|---|---|
  | `SurvivalToolsLite` | `Prefix_Pawn_WorkTagIsDisabled` wants `Pawn_StoryTracker`, target is `Pawn.WorkTagIsDisabled` (18th of 20) | the weapon switcher on `Pawn_DraftController.Drafted`, and the tool wear in Quarry's `JobDriver_MineQuarry.Mine` — Quarry is in the list |
  | `Minerals` | `initNewMapIce` wants `GenStep_RockChunks`, target is `GenStep_Plants.Generate` (2nd of 3) | the ice pass, and then `PatchAll()` — i.e. every attributed patch the mod has: the skyfaller mineral swap and `GenStep_PreciousLump.Generate` |
  | `Skynet_SK` | `CompatPatch_GetJoyTryGiveJob` wants `JobGiver_EatInGatheringArea`, copied from the line above it, target is `JobGiver_GetJoy.TryGiveJob` | the class itself: the static constructor dies, the type stays uninitialised, and its already-installed postfix on `Pawn_NeedsTracker.ShouldHaveNeed` then throws `TypeInitializationException` for every pawn and every need — 37 lines per pawn |

  1.6's Harmony accepts the same signatures (measured, not assumed: patching a dummy method with a
  deliberately mismatched `__instance` applies without complaint against `D:\RimWorld_HSK`'s
  `0Harmony.dll`), which is why the pack ran with them.
  `HSKFixes-1.7` answers both from one place: a prefix on
  `Harmony.Patch` that first tests for the defect itself — a parameter named `__instance` whose
  type does not accept `original.DeclaringType`, exactly Harmony's own test — and only then looks
  for a known replacement. Fix the signature upstream and the swap stops firing by itself; a broken
  patch we have not read keeps throwing, which is the honest outcome. The replacements keep the
  original logic (`STLWorkTagFix`, `MineralsIceFix` — the latter just calls their own method with
  `null` for the `__instance` its body never uses). Patching `Harmony.Patch` is allowed — also
  measured, together with the end-to-end effect.
- `RimworldFavourites` throws once from its own static constructor on the new runtime. Someone
  else's mod, someone else's 1.7 port.

Measured on the run of 2026-10-13, on the mod list in `ModsConfig.xml`: **zero errors in 2129
lines**. The pack reaches the main menu and starts a map without Combat Extended, with its own C#
layer running — 119 of its 120 Harmony patch classes applied, the settings read from the player's
file, and the three mods whose own code broke (`Temp_MechJobFix`, `SK.OdysseyFixesMod`,
`Pawn_WorkTagIsDisabled`, `Minerals.HarmonyPatches`) fixed from the outside.

What is still not reproduced is named in the limits below: CE's mechanics, the pack's patches to
CE's own defs, and `SK.SuppressionPatch`.

`About.xml` therefore declares 1.7 as supported: the loader and the shim both work there.
