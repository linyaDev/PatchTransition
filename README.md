# Patch Transition

Мод позволяет играть в Hardcore SK в неполном составе: без Combat Extended, без части его модов,
на новой версии RimWorld.

Пак рассчитан на полный набор модов. Если какого-то нет, пак сыпет ошибками: часть предметов не
грузится, часть механик не включается. Patch Transition подставляет недостающее и файлы пака при
этом не меняет.

**Заглушки работают только пока мода нет.** Поставьте настоящий Combat Extended — заглушка CE
отключается сама, целиком: ни её сборки, ни её дефов в игре не окажется. То же с Rimatomics, Dubs
Bad Hygiene и Rimefeller. Удалять и настраивать ничего не нужно, мод можно оставить на месте.

## Что делает

- **Без Combat Extended** — подставляет сборку-заглушку с типами CE и его дефы. Оружие, броня и
  патроны пака грузятся и работают на ванильной механике.
- **Без Rimatomics, Dubs Bad Hygiene, Rimefeller** — подставляет дефы и типы, на которые ссылается
  пак.
- **На RimWorld 1.7** — правки самого пака вынесены в отдельный мод `HSKFixes-1.7`: подъём
  Mod-класса Core SK и подмена патчей, которые новая Harmony отвергает. Нужны они при полном
  составе модов, то есть когда этот мод как раз ничего не делает.
- **Чистит лог** — скрывает сообщения о патчах пака к дефам CE, которых без CE не существует.
  Остальные ошибки остаются видимыми.

## Установка

1. Положите папку в `Mods` рядом с остальными.
2. В списке модов поставьте её **выше Hardcore SK**.
3. Ничего настраивать не нужно: каждая часть включается сама по тому, чего не хватает.

## Как устроено

Каждая часть лежит в своей папке и подключается по условию. В корне кода нет.

| Папка | Подключается |
|---|---|
| `NoCE` | нет Combat Extended |
| `NoRimatomics`, `NoHygiene`, `NoRimefeller` | нет этого мода |
| — | кода в корне нет |

Условие проверяется при запуске игры. Если мод из строки установлен и включён, его папка для игры
просто не существует — конфликтовать нечему, и работает настоящий мод, а не заглушка.

## Что нужно знать

- Баланс остаётся от CE, механика ванильная: пробитие брони, объём и подавление не читает никто.
  Бой ведёт себя не так, как в CE, и не так, как в ванилле.
- Патчи пака к собственным дефам CE без CE не встанут — они правят внутренности дефов, которых нет.
  Скрыты сообщения, а не причина.
- Графику CE мод не распространяет. Если CE у вас где-то установлен, запустите
  `Tools/CopyCETextures.ps1` — он скопирует нужные паку текстуры. Без этого вместо части иконок
  будут заглушки.
- `ModsConfig.xml` в корне — список модов, на котором мод проверялся. Чтобы повторить: закройте
  игру, сохраните свой `<профиль>\Config\ModsConfig.xml` и положите этот файл на его место.

Подробности по каждой части — в `CLAUDE.md`.

---

# Patch Transition

This mod lets you play Hardcore SK in an incomplete setup: without Combat Extended, without some of
its mods, on a newer RimWorld.

The pack expects its full mod list. With a mod missing it fills the log with errors: some items do
not load, some mechanics never switch on. Patch Transition supplies what is missing, and does not
change the pack's files.

**The stubs only work while the mod is missing.** Install the real Combat Extended and the CE stub
switches itself off entirely — neither its assembly nor its defs will be in the game. The same goes
for Rimatomics, Dubs Bad Hygiene and Rimefeller. Nothing to remove or configure; the mod can stay
where it is.

## What it does

- **Without Combat Extended** — supplies a stub assembly with CE's types and its defs. The pack's
  weapons, armour and ammo load and work on vanilla mechanics.
- **Without Rimatomics, Dubs Bad Hygiene, Rimefeller** — supplies the defs and types the pack
  references.
- **On RimWorld 1.7** — the pack's own breakage moved to a separate mod, `HSKFixes-1.7`: the Core
  SK Mod-class revive and the patch swaps the newer Harmony needs. Those are wanted with the full
  mod list, which is when this mod does nothing.
- **Cleans the log** — hides the messages about the pack's patches to CE defs that do not exist
  without CE. Everything else stays visible.

## Installing

1. Put the folder in `Mods` with the rest.
2. Place it **above Hardcore SK** in the mod list.
3. Nothing to configure: each part switches itself on according to what is missing.

## How it is arranged

Every part lives in its own folder and loads on a condition. The root holds no code.

| Folder | Loads when |
|---|---|
| `NoCE` | Combat Extended is absent |
| `NoRimatomics`, `NoHygiene`, `NoRimefeller` | that mod is absent |
| — | the root carries no code |

The condition is checked when the game starts. If the mod on that row is installed and enabled, its
folder does not exist as far as the game is concerned — nothing to clash, and the real mod runs
instead of the stub.

## Worth knowing

- Balance is CE's, mechanics are vanilla's: armour penetration, bulk and suppression are read by
  nobody. Combat matches neither CE nor vanilla.
- The pack's patches to CE's own defs cannot apply without CE — they edit the insides of defs that
  are not there. The messages are hidden, not the cause.
- CE's art is not shipped. If you have CE installed somewhere, run `Tools/CopyCETextures.ps1` to
  copy the textures the pack asks for. Without it some icons are placeholders.
- `ModsConfig.xml` in the root is the mod list this was tested on. To reproduce it: close the game,
  save your own `<profile>\Config\ModsConfig.xml`, and put this file in its place.

Details on every part are in `CLAUDE.md`.
