# Sekyra – primary use + systém zničitelných objektů (Destroyable)

**Datum:** 2026-09-29
**Větev:** NovyNovyDih
**Stav:** návrh schválen, čeká se na implementační plán

## Cíl

Přidat sekyře logiku primárního použití (levé tlačítko): Raycast z kamery hráče,
který při zásahu objektu se scriptem `Destroyable` spustí jeho zničení. Systém musí
být rozšiřitelný pro libovolné množství dalších zničitelných objektů (bedny, kameny,
zdi…), aniž by se pro každý psal nový kód.

Dveře jsou speciální případ: vydrží **3 údery** a **každý úder je samostatná zamčená
sekvence ve stylu `HidingSpot`** – při úderu se hráčova skutečná sekyra i kamera
přesunou na sockety na dveřích, ovládání se vypne, Animator dveří přehraje úder,
pak se vše vrátí. Po třetím úderu dveře padnou.

## Mimo rozsah (Non-goals)

- Sekundární použití sekyry (pravé tlačítko) – neřešíme.
- Poškození jiných tvorů/hráče (combat) – jen zničitelné prostředí.
- Loot systém – `Destroyable` jen vyvolá `onDestroyed` event, na který si drop
  případně navěsíš zvlášť.
- Refaktor `HidingSpot` – zůstává beze změny (viz níže).

## Architektura

Čtyři nové skripty + jedno rozšíření `PlayerEquipment`. Sekyra a zničitelné objekty
jsou oddělené přes tenký interface `IDestroyable` – sekyra nezná konkrétní typy.

### 1. `IDestroyable` (interface) + `ToolHit` (struct)

Jediné, co sekyra „vidí". Nový soubor `Assets/Scripty/IDestroyable.cs`.

```csharp
public struct ToolHit
{
    public Vector3 point;    // bod dopadu raycastu
    public Vector3 normal;   // normála povrchu
    public int damage;       // kolik HP úder ubere (sekyra = 1)
    public Camera camera;    // kamera hráče (pro zamčenou sekvenci)
    public GameObject player;// root hráče (pro vypnutí pohybu/fyziky)
}

public interface IDestroyable
{
    // true = tento objekt na sebe „převezme" hráče (zamkne kameru/pohyb),
    //        takže sekyra NEMÁ přehrát svůj švih v ruce (dveře).
    // false = objekt se ničí „na místě", sekyra normálně švihne (bedna…).
    bool CapturesUser { get; }

    // Aplikuje úder. Vrací true, když akci „spotřeboval" (typicky dveře,
    // které rozjely zamčenou sekvenci) – sekyra pak nemá dělat nic dalšího.
    bool TakeHit(ToolHit hit);
}
```

Proč `camera`/`player` v `ToolHit`: sekyra si je vyřeší jednou (přes `PlayerInteract`),
dveře je dostanou hotové a nemusí duplikovat celé `ResolvePlayerReferences` z `HidingSpot`.

### 2. `MeleeToolDamage` (na prefabu sekyry)

Nový soubor `Assets/Scripty/MeleeToolDamage.cs`. Sedí na modelu držené sekyry, na
**stejném GameObjectu jako Animator** (aby ho Animation Event mohl zavolat).

Serializovaná konfigurace:
- `float range = 3f` – dosah raycastu.
- `int damage = 1` – kolik HP ubere jeden úder.
- `LayerMask hitMask` – do čeho se trefuje (ať netrefí hráče/sebe).
- `bool ignoreTriggers = true` – ignoruje trigger collidery (ať netrefí item-pickup hitboxy).

Reference:
- Kameru + hráče si dohledá sám, stejným postupem jako `HidingSpot`
  (přes `PlayerInteract.playerCamera` ve scéně → fallback `Camera.main`).
  **Žádné ruční drátování referencí v YAML.**
- `HeldItemAnimator` sourozenec (pro spuštění švihu).

Implementuje `IPrimaryUse` (viz níže) – žádné ruční drátování UnityEventu v prefabu.

Chování:
- `OnPrimaryUse()` (volá `PlayerEquipment` při stisku levého tlačítka):
  1. Raycast z kamery.
  2. Trefa `IDestroyable` s `CapturesUser == true` (dveře) → `door.TakeHit(...)`,
     a **nespustí** švih sekyry v ruce (dveře si sekyru vypůjčí a animují samy).
  3. Trefa `IDestroyable` s `CapturesUser == false` nebo nic → zapamatuj cíl a zavolej
     `HeldItemAnimator.TryPrimaryUse()` (švih + cooldown). Cíl si pamatuj, jen když
     `TryPrimaryUse()` vrátí `true` (neblokuje cooldown).
- `Hit()` – veřejná metoda pro Animation Event; aplikuje damage na zapamatovaný cíl
  typu „na místě" (`TakeHit`) v okamžiku dopadu švihu, pak cíl zapomene.

Rozhodnutí padne **před** švihem (v `OnPrimaryUse`), proto se u dveří švih vůbec
nespustí – tím je vyřešená kolize s Animatorem dveří.

### 3. `Destroyable : MonoBehaviour, IDestroyable` (univerzální)

Nový soubor `Assets/Scripty/Destroyable.cs`. Objekt, který se ničí „na místě"
(`CapturesUser = false`). Pokrývá všechny budoucí objekty jen nastavením v Inspectoru.

Serializovaná konfigurace:
- `int hitPoints = 1` – kolik úderů vydrží.
- `Animator animator` – volitelný.
- `string[] hitTriggers` – triggery na jednotlivé (přeživší) údery; jeden prvek =
  stejná animace pokaždé, víc prvků = eskalace (Crack1, Crack2…). Prázdné = bez animace.
  Vzor převzatý z `HidingSpot.DirectionData`.
- `string destroyTrigger` – animace zničení na posledním úderu.
- `float destroyDelay` – kolik počkat (coroutina `WaitForSeconds`), než se objekt
  odstraní (dohraje se destroy animace).
- `enum DestroyMode { DestroyObject, Deactivate, EventOnly }`.
- `UnityEvent onHit`, `UnityEvent onDestroyed` – navěšení zvuku, dropu, částic.

`TakeHit(hit)`:
```
if (isDestroyed) return false;
hitPoints -= hit.damage;
if (hitPoints > 0) { přehraj hitTrigger; onHit.Invoke(); }
else { isDestroyed = true; přehraj destroyTrigger; onDestroyed.Invoke();
       po destroyDelay proveď DestroyMode; }
return false; // nepřebírá hráče
```

### 4. `DestroyableDoor : MonoBehaviour, IDestroyable` (dveře, zamčená sekvence)

Nový soubor `Assets/Scripty/DestroyableDoor.cs`. `CapturesUser = true`. Zrcadlí
lock/restore vzor z `HidingSpot`.

Serializovaná konfigurace:
- `int hitPoints = 3`.
- `Animator animator` – Animator dveří; jeho klipy animují `cameraSocket` i `axeSocket`.
- `Transform cameraSocket` – kam se během úderu naparentuje kamera hráče.
- `Transform axeSocket` – kam se naparentuje hráčova sekyra (klip ji rozhýbe).
- `string[] hitAnimTriggers` (délka 3) – trigger pro každý úder (Hit1, Hit2, Break).
- `float hitDuration` – délka zamčené sekvence jednoho úderu.
- `float headReturnLerpDuration` – plynulý návrat kamery (jako `HidingSpot`).
- `UnityEvent onDestroyed`.

`TakeHit(hit)`: pokud `isSequenceRunning` → `return true` (ignoruj další rány).
Jinak spustí `HitRoutine(hit)` a `return true`.

## Tok primárního použití (levé tlačítko)

```
klik → MeleeToolDamage: Raycast z kamery (range, hitMask, ignoreTriggers)
   ├─ trefa IDestroyable a CapturesUser == true (dveře):
   │     door.TakeHit(toolHit)  → zamčená sekvence; ŽÁDNÝ švih sekyry v ruce
   │
   ├─ trefa IDestroyable a CapturesUser == false (bedna…):
   │     zapamatuj cíl → HeldItemAnimator.TryPrimaryUse() (švih)
   │     → Animation Event Hit() → cíl.TakeHit(toolHit) (damage v momentě dopadu)
   │
   └─ nic / netrefil IDestroyable:
         HeldItemAnimator.TryPrimaryUse() (švih naprázdno)
```

Cooldown: pro „na místě" objekty řeší `HeldItemAnimator.useCooldown`. Pro dveře je
cooldownem samotná délka sekvence (`isSequenceRunning` blokuje další úder).

## Dveřní sekvence – jeden úder (pořadí je kritické)

1. `isSequenceRunning = true`; `PlayerEquipment.Instance.BorrowHeldItem(axeSocket)`
   (sekyra se naparentí na `axeSocket`, vypne se bob/sway/holster i čtení use).
2. Vypni pohyb (`PlayerRigidbodyMovement.enabled = false`), `Rigidbody.isKinematic = true`,
   `CharacterController.enabled = false` (přesně jako `HidingSpot.HideRoutine`).
3. Kameru naparentuj do `cameraSocket` (`localPosition/localRotation = 0`).
4. `animator.SetTrigger(hitAnimTriggers[3 - hitPoints])`; `yield WaitForSeconds(hitDuration)`.
5. **Návrat NEJDŘÍV:** kameru zpět do CamHolderu s lerpem (jako `HidingSpot.ShowRoutine`),
   `PlayerEquipment.Instance.ReturnHeldItem()` (sekyra zpět do ruky), obnov pohyb/fyziku.
6. `hitPoints--`. Pokud `<= 0` → **až teď** `Destroy(gameObject)` (event `onDestroyed`).
   Jinak `isSequenceRunning = false`.

⚠️ **Kritické pořadí:** na posledním úderu se break-animace přehraje v kroku 4 (kamera
sleduje pád dveří), ale objekt se fyzicky zničí **až po** návratu kamery a sekyry.
Kdyby se `Destroy(gameObject)` zavolal dřív, zničila by se s dveřmi i naparentovaná
kamera a sekyra.

## Rozšíření `PlayerEquipment` + `IPrimaryUse`

Malý interface pro routing primárního použití (drží `PlayerEquipment` oddělený od
konkrétního nástroje – budoucí zbraně/nástroje si primary use napojí stejně):

```csharp
public interface IPrimaryUse { void OnPrimaryUse(); }
```

Změna vstupního toku v `Update()`: při stisku primárního tlačítka `PlayerEquipment`
nejdřív hledá `IPrimaryUse` na drženém objektu (`currentHeldObject`). Když ho najde,
zavolá `OnPrimaryUse()` (nástroj sám rozhodne, zda švihnout). Jinak fallback na
stávající `currentHeldAnimator?.TryPrimaryUse()`. Sekundární použití zůstává beze změny.

Nové veřejné API pro půjčení sekyry dveřím (analogie ke stávajícímu `SetHidden`):

- `GameObject BorrowHeldItem(Transform newParent)` – naparentí `currentHeldObject`
  na `newParent`, nastaví `isLent = true`, vrátí objekt.
- `void ReturnHeldItem()` – vrátí objekt pod `handSocket`, resetuje lokální transform,
  `isLent = false`.

V `Update()`: když `isLent`, přeskočit blok drženého předmětu (bob, sway, holster
i čtení primary/secondary use) – ať to nefightuje s Animatorem dveří.

## HidingSpot – beze změny

Lock/restore logika se s `HidingSpot` překrývá (~60 řádků: vypnutí pohybu, kinematika,
CharacterController, parent kamery, návratový lerp). Vědomě to **neduplikujeme
do sdílené třídy hned** – `DestroyableDoor` si napíše vlastní lock/restore a `HidingSpot`
zůstane funkční beze změny (menší riziko rozbití hotové cutscény). Až přibude víc
„cutscénových" objektů, vytáhne se sdílený `PlayerLockSequence` helper.

## Ruční nastavení v Unity (mimo kód)

- `MeleeToolDamage` přidat na prefab `SekyraDoRuky` (objekt s Animatorem), nastavit
  `hitMask` (vrstvy zničitelných objektů) a `range`.
- Do klipu „UsePrimary" přidat Animation Event volající `Hit()` v okamžiku dopadu švihu.
  (Lze dopsat i do `.anim` YAML – je to jen jméno metody, reprodukovatelné, na rozdíl
  od `InputActionReference`.)
- Dveře: přidat `DestroyableDoor`, Animator s klipy Hit1/Hit2/Break (animují `cameraSocket`
  i `axeSocket`), vytvořit child transformy `cameraSocket` a `axeSocket`, collider na
  správné vrstvě (v `hitMask` sekyry).
- Zničitelné objekty „na místě": přidat `Destroyable`, nastavit `hitPoints` a případně
  animace/eventy; collider na správné vrstvě.

## Testování

Unity nemá v tomto projektu automatizované testy; ověření bude manuální ve scéně:
- Sekyra + bedna (`Destroyable`, hitPoints=1) → 1 úder zničí, přehraje se animace/event.
- Sekyra + dveře → 3 zamčené sekvence, po 3. dveře zmizí; kamera i sekyra se pokaždé
  korektně vrátí (žádné „cuknutí", žádná zničená kamera).
- Během dveřní sekvence nejde hýbat ani přepínat kapsy.
- Sekyra netrefí item-pickup triggery ani sebe/hráče.
```
