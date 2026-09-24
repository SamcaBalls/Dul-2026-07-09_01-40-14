# Animace držených itemů (vytáhnutí + použití)

- **Datum:** 2026-09-24
- **Stav:** Schváleno k plánování
- **Oblast:** `Assets/Scripty/PlayerEquipment.cs`, nová komponenta `HeldItemAnimator.cs`, prefaby itemů

## Kontext / současný stav

- `PlayerEquipment.cs` drží itemy ve dvou kapsách (Q/E). Při výběru kapsy instancuje `ItemData.inGamePrefab` pod `handSocket`.
- Vytáhnutí/schování ("holster") je teď **procedurální** — `AnimateHolster` posouvá transform rootu nahoru/dolů. Navrch běží bob (idle houpání) a sway (zaostávání při otáčení). Vše zapisuje `localPosition`/`localRotation` na root drženého objektu každý snímek (`ApplyHeldItemTransform`).
- **"Use" (použití) zatím neexistuje** — žádný vstup ani akce.
- Animator už je v projektu použit jinde (`GlassesCleaningSequence` → `SetTrigger("UtriBryle")`, `HidingSpot` → směrové triggery).
- Existuje nový prefab `SekyraDoRuky.prefab` (sekera do ruky).

## Cíle

1. Item může přehrát **volitelný Draw klip** při vytáhnutí (navrch nad procedurálním slidem, který zůstává).
2. Item může reagovat na **primární použití (levé tlačítko)** a **sekundární použití (pravé tlačítko)** vlastním klipem.
3. Každý item si **sám určí**, jestli danou akci (draw / primary / secondary) má — může mít všechny, jednu, nebo žádnou.
4. Přidání nového animovaného itemu = prefab + Animator Controller, **bez zásahu do kódu**.

## Ne-cíle (YAGNI)

- Samotná herní logika použití (poškození, kácení stromu, spotřeba) — řeší se zvlášť. Tento spec dodá jen animaci + volitelný `UnityEvent` hook, na který se gameplay navěsí později.
- Nahrazení procedurálního slide/bob/sway animačními klipy — slide zůstává beze změny.
- Řetězení/comba, směrové útoky, nabíjené použití.

## Klíčová rozhodnutí (z brainstormingu)

- Zdroj animací: klipy míchané — převážně keyované v Unity, občas FBX z Blenderu → **Animator Controller na každém itemu**.
- Use akce: **dvě** (primární LMB, sekundární RMB), item nemusí mít žádnou.
- Draw: **ponechat procedurální slide** + přidat **volitelný Draw klip**.
- Architektura: **komponenta na itemu** (`HeldItemAnimator`), ne konfigurace v `ItemData`. Item je samostatný, `PlayerEquipment` zůstává štíhlý.

## Architektura

### Rozdělení zodpovědností (kvůli konfliktu transformu)

Procedurální kód vlastní **root** drženého objektu (zapisuje mu transform každý snímek). Animator proto **nesmí** animovat root — animuje **child model**. Tím se to skládá: kód posune celý item (bob/sway/holster), klip přidá švih navrch.

```
SekyraDoRuky (root)              ← vlastní PlayerEquipment (bob/sway/holster slide)
  └─ Model (mesh)                ← klipy animují tenhle transform
     ├─ Animator (+ Controller)
     └─ HeldItemAnimator (komponenta)
```

Pravidlo: **na rootu prefabu nesmí být Animator, který hýbe rootovým transformem.** Animator patří na child model.

### Datový tok

1. `PlayerEquipment.SwitchHeldItemRoutine` instancuje item → najde na něm `HeldItemAnimator` (přes `GetComponentInChildren`) a cachne referenci.
2. Když item vyjíždí nahoru (`AnimateHolster(0f)`), zavolá se `heldItemAnimator.PlayDraw()`.
3. V `Update`, když je item držen a `!isHidden` a `switchRoutine == null`:
   - LMB stisknuto → `heldItemAnimator.TryPrimaryUse()`
   - RMB stisknuto → `heldItemAnimator.TrySecondaryUse()`
4. Při přepnutí/schování se starý objekt zničí, cache se nuluje.

## Komponenta `HeldItemAnimator.cs` (nová)

Sedí na child modelu itemu.

**Serializovaná pole (Inspector):**
- `Animator animator` — reference na Animator (default: `GetComponent<Animator>()` v `Reset`/`Awake`).
- `string drawTrigger = "Draw"` — prázdné = item nemá draw klip.
- `string primaryTrigger = "UsePrimary"` — prázdné = nereaguje na LMB.
- `string secondaryTrigger = "UseSecondary"` — prázdné = nereaguje na RMB.
- `float useCooldown = 0.5f` — minimální rozestup mezi použitími (aby nešlo spamovat).
- `UnityEvent onPrimaryUse` / `UnityEvent onSecondaryUse` — volitelný hook pro gameplay (poškození apod.).

**Veřejné metody:**
- `void PlayDraw()` — pokud `drawTrigger` není prázdný a `animator` existuje, `animator.SetTrigger(drawTrigger)`.
- `bool TryPrimaryUse()` — když není v cooldownu a `primaryTrigger` není prázdný: nastaví trigger, spustí cooldown, `onPrimaryUse.Invoke()`, vrátí true. Jinak false.
- `bool TrySecondaryUse()` — analogicky pro secondary.

**Interní stav:**
- Cooldown se sleduje časovačem (`Time.time` posledního použití). Bez animačních eventů — jednoduché a robustní. (Poznámka: pro přesnější časování lze později přejít na Animation Events, ale to je mimo scope.)

## Změny v `PlayerEquipment.cs`

- **Vstup LMB/RMB:** číst přímo `Mouse.current.leftButton.wasPressedThisFrame` / `rightButton.wasPressedThisFrame` (`using UnityEngine.InputSystem`). Důvod: podle projektové paměti je ruční drátování `InputActionReference` přes YAML nespolehlivé; přímé čtení myši nevyžaduje žádné wiring v Inspectoru. (Ošetřit `Mouse.current != null`.)
- **Cache:** nové privátní pole `HeldItemAnimator currentHeldAnimator`.
- V `SwitchHeldItemRoutine` po `Instantiate`: `currentHeldAnimator = currentHeldObject.GetComponentInChildren<HeldItemAnimator>();`, pak zavolat `currentHeldAnimator?.PlayDraw()` **těsně před** `yield return AnimateHolster(0f)`, aby se Draw klip překrýval s vyjížděním nahoru (ne až po něm). Při zničení objektu nastavit `currentHeldAnimator = null`.
- V `Update` (uvnitř bloku `if (currentHeldObject != null)`), s podmínkami `!isHidden && switchRoutine == null && currentHeldAnimator != null`:
  - `Mouse.current.leftButton.wasPressedThisFrame` → `currentHeldAnimator.TryPrimaryUse()`
  - `Mouse.current.rightButton.wasPressedThisFrame` → `currentHeldAnimator.TrySecondaryUse()`

**Gating (kdy use NEjde):** ve skrýši (`isHidden`), během přepínání/vytahování (`switchRoutine != null`), když item nemá `HeldItemAnimator`, a během cooldownu (řeší komponenta).

## Unity-side setup (manuální, v editoru — mimo kód)

Toto udělá uživatel v editoru; kód to nevytvoří:

1. Na `SekyraDoRuky` mít child `Model` s meshem. Zajistit, že root prefabu nemá Animator hýbající rootem.
2. Na `Model` přidat **Animator** + nový **Animator Controller** (`SekyraController`).
3. Controller: stav `Idle` (default). Triggery `Draw`, `UsePrimary`, `UseSecondary`, každý přechod do příslušného stavu a zpět do `Idle`. **Apply Root Motion = OFF.**
4. Nakeyovat klipy (Unity Animation okno nebo import FBX z Blenderu).
5. Na `Model` přidat komponentu `HeldItemAnimator`, vyplnit `animator` a jména triggerů (prázdné = akci nemá). Volitelně navěsit `onPrimaryUse`.

## Testování

- **Manuální (Unity Play mode):**
  - Vytáhnutí itemu s Draw klipem → klip se přehraje jednou, item zároveň vyjede nahoru (slide).
  - LMB → primární klip; RMB → sekundární klip.
  - Item bez daného triggeru (prázdné jméno) → daná akce nic nedělá, žádná chyba.
  - Spam LMB → další švih až po `useCooldown`.
  - Ve skrýši / během přepínání kapes → use ignorováno.
- **Kompilace:** projekt se přeloží bez chyb; `HeldItemAnimator` bez přiřazeného Animatoru nespadne (null-safe).

## Dotčené soubory

- Nový: `Assets/Scripty/HeldItemAnimator.cs` (+ `.meta`)
- Úprava: `Assets/Scripty/PlayerEquipment.cs`
- Editor (mimo kód): prefab `SekyraDoRuky` + Animator Controller
