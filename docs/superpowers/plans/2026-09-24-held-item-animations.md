# Animace držených itemů (draw + use) – Implementační plán

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Držené itemy umí přehrát volitelný Draw klip při vytáhnutí a reagovat animací na primární (LMB) a sekundární (RMB) použití; každý item si sám určí, které akce má.

**Architecture:** Nová komponenta `HeldItemAnimator` sedí na child modelu itemu a obaluje jeho `Animator` (triggery Draw/UsePrimary/UseSecondary + cooldown + volitelné UnityEvent hooky). `PlayerEquipment` si ji po vytáhnutí itemu najde a volá — procedurální bob/sway/holster na rootu zůstává beze změny, klipy animují child model, takže se transformy nepraní.

**Tech Stack:** Unity (C#, MonoBehaviour), Unity Animator, Unity Input System (`InputActionReference`), UnityEngine.Events.

**Poznámka k verifikaci:** Projekt nemá Unity Test Framework a kód je navázaný na Animator/Input System/MonoBehaviour → ověřuje se **manuálně v Play mode** + kontrolou, že se projekt v Unity zkompiluje bez chyb (Console). Žádné automatické unit testy se nepřidávají (YAGNI, uživatel je nechtěl).

**Reference:** Spec `docs/superpowers/specs/2026-09-24-held-item-animations-design.md`.

---

## File Structure

- **Create:** `Assets/Scripty/HeldItemAnimator.cs` — komponenta na itemu; jediná zodpovědnost = přehrávat animační klipy itemu (draw/use) a nabídnout gameplay hooky. Unity k němu sám dogeneruje `.meta`.
- **Modify:** `Assets/Scripty/PlayerEquipment.cs` — přidat vstupy pro use, cache animátoru drženého itemu, volání PlayDraw při vytáhnutí a use v Update.
- **Editor (manuální, mimo kód):** prefab `SekyraDoRuky` (struktura root→Model), Animator Controller, Input Actions asset, wiring v Inspectoru. Popsáno v Tasku 3 jako checklist pro uživatele.

---

### Task 1: Komponenta `HeldItemAnimator`

**Files:**
- Create: `Assets/Scripty/HeldItemAnimator.cs`

- [ ] **Step 1: Vytvořit soubor s kompletním kódem**

```csharp
using UnityEngine;
using UnityEngine.Events;

// Sedí na modelu drženého itemu (child pod rootem, který hýbe PlayerEquipment).
// Přehrává volitelné animační klipy: vytáhnutí (Draw) a použití (primární/sekundární).
// Item, který daný trigger nemá vyplněný, na akci prostě nereaguje.
public class HeldItemAnimator : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Animator s klipy Draw / UsePrimary / UseSecondary. Prázdné = zkusí se najít na tomto objektu.")]
    [SerializeField] private Animator animator;

    [Header("Triggery (prázdné = item danou akci nemá)")]
    [SerializeField] private string drawTrigger = "Draw";
    [SerializeField] private string primaryTrigger = "UsePrimary";
    [SerializeField] private string secondaryTrigger = "UseSecondary";

    [Header("Nastavení")]
    [Tooltip("Minimální rozestup mezi použitími (sekundy), aby nešlo spamovat.")]
    [SerializeField] private float useCooldown = 0.5f;

    [Header("Události (volitelné – navěš herní logiku)")]
    public UnityEvent onPrimaryUse;
    public UnityEvent onSecondaryUse;

    // Čas posledního použití (sdílený pro primary i secondary) pro cooldown.
    private float lastUseTime = -999f;

    // Když komponenta přibude v editoru, předvyplní Animator z tohoto objektu.
    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    // Zavolá PlayerEquipment při vytáhnutí itemu (překrývá se s procedurálním slidem).
    public void PlayDraw()
    {
        if (animator != null && !string.IsNullOrEmpty(drawTrigger))
            animator.SetTrigger(drawTrigger);
    }

    // Primární použití (levé tlačítko). Vrací true, když se akce fakt provedla.
    public bool TryPrimaryUse()
    {
        return TryUse(primaryTrigger, onPrimaryUse);
    }

    // Sekundární použití (pravé tlačítko). Vrací true, když se akce fakt provedla.
    public bool TrySecondaryUse()
    {
        return TryUse(secondaryTrigger, onSecondaryUse);
    }

    // Společná logika: prázdný trigger = akci nemá; jinak respektuje cooldown,
    // nastaví trigger (má-li animator) a vyvolá volitelný gameplay event.
    private bool TryUse(string trigger, UnityEvent evt)
    {
        if (string.IsNullOrEmpty(trigger)) return false;
        if (Time.time - lastUseTime < useCooldown) return false;

        lastUseTime = Time.time;

        if (animator != null)
            animator.SetTrigger(trigger);

        evt?.Invoke();
        return true;
    }
}
```

- [ ] **Step 2: Ověřit kompilaci**

Přepnout do Unity, počkat na rekompilaci. Otevřít Console (Window → General → Console).
Expected: žádné compile errory; `HeldItemAnimator` jde přidat jako komponenta.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripty/HeldItemAnimator.cs Assets/Scripty/HeldItemAnimator.cs.meta
git commit -m "feat: HeldItemAnimator komponenta pro draw/use klipy itemů"
```

---

### Task 2: Napojení use vstupů a draw/use volání do `PlayerEquipment`

**Files:**
- Modify: `Assets/Scripty/PlayerEquipment.cs`

- [ ] **Step 1: Přidat InputActionReference pole pro use**

V bloku `[Header("Input System")]` za `pocket2Action` přidat:

```csharp
    [SerializeField] private InputActionReference primaryUseAction;   // Levé tlačítko – primární použití
    [SerializeField] private InputActionReference secondaryUseAction; // Pravé tlačítko – sekundární použití
```

- [ ] **Step 2: Přidat cache animátoru drženého itemu**

Za pole `private GameObject currentHeldObject;` přidat:

```csharp
    private HeldItemAnimator currentHeldAnimator; // Animátor klipů aktuálně drženého itemu (může být null)
```

- [ ] **Step 3: Enable/Disable nových akcí**

V `OnEnable` za existující enable řádky přidat:

```csharp
        if (primaryUseAction != null) primaryUseAction.action.Enable();
        if (secondaryUseAction != null) secondaryUseAction.action.Enable();
```

V `OnDisable` za existující disable řádky přidat:

```csharp
        if (primaryUseAction != null) primaryUseAction.action.Disable();
        if (secondaryUseAction != null) secondaryUseAction.action.Disable();
```

- [ ] **Step 4: Zpracovat use vstup v `Update`**

V `Update`, uvnitř bloku `if (currentHeldObject != null)`, hned za výpočtem bobTimeru a před `UpdateSway();` vložit:

```csharp
            // Použití drženého itemu – jen když není schovaný a zrovna neprobíhá přepínání/vytahování.
            if (!isHidden && switchRoutine == null && currentHeldAnimator != null)
            {
                if (primaryUseAction != null && primaryUseAction.action.WasPressedThisFrame())
                    currentHeldAnimator.TryPrimaryUse();

                if (secondaryUseAction != null && secondaryUseAction.action.WasPressedThisFrame())
                    currentHeldAnimator.TrySecondaryUse();
            }
```

Výsledek bloku (pro kontrolu):

```csharp
        if (currentHeldObject != null)
        {
            if (bobEnabled)
                bobTimer += Time.deltaTime * bobFrequency * Mathf.PI * 2f;

            // Použití drženého itemu – jen když není schovaný a zrovna neprobíhá přepínání/vytahování.
            if (!isHidden && switchRoutine == null && currentHeldAnimator != null)
            {
                if (primaryUseAction != null && primaryUseAction.action.WasPressedThisFrame())
                    currentHeldAnimator.TryPrimaryUse();

                if (secondaryUseAction != null && secondaryUseAction.action.WasPressedThisFrame())
                    currentHeldAnimator.TrySecondaryUse();
            }

            UpdateSway();
            ApplyHeldItemTransform();
        }
```

- [ ] **Step 5: Cachnout animátor a spustit Draw v `SwitchHeldItemRoutine`**

V `SwitchHeldItemRoutine`, v bloku ničení starého objektu, za `currentHeldObject = null;` přidat:

```csharp
            currentHeldAnimator = null;
```

Ve bloku vytváření nového itemu za `currentHeldObject.transform.localRotation = Quaternion.identity;` přidat:

```csharp
            currentHeldAnimator = currentHeldObject.GetComponentInChildren<HeldItemAnimator>();
```

A těsně před `yield return AnimateHolster(0f);` přidat:

```csharp
            // Volitelný Draw klip se překrývá s vyjížděním itemu nahoru.
            if (currentHeldAnimator != null) currentHeldAnimator.PlayDraw();
```

Výsledek bloku vytváření (pro kontrolu):

```csharp
        if (targetData != null && targetData.inGamePrefab != null)
        {
            currentHeldObject = Instantiate(targetData.inGamePrefab, handSocket);
            currentHeldObject.transform.localRotation = Quaternion.identity;
            currentHeldAnimator = currentHeldObject.GetComponentInChildren<HeldItemAnimator>();

            bobTimer = 0f;        // houpání začne z klidu
            holsterAmount = 1f;   // start schovaný dole
            swayPositionOffset = Vector3.zero;   // sway začne z klidu (žádné cuknutí při vytažení)
            swayRotationOffset = Vector3.zero;
            hasLastSocketRotation = false;
            ApplyHeldItemTransform();

            // Volitelný Draw klip se překrývá s vyjížděním itemu nahoru.
            if (currentHeldAnimator != null) currentHeldAnimator.PlayDraw();

            yield return AnimateHolster(0f);
        }
```

- [ ] **Step 6: Ověřit kompilaci**

Přepnout do Unity, počkat na rekompilaci, zkontrolovat Console.
Expected: žádné compile errory. Na `PlayerEquipment` se v Inspectoru objeví nová pole `Primary Use Action` a `Secondary Use Action`.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripty/PlayerEquipment.cs
git commit -m "feat: use vstupy (LMB/RMB) a draw klip napojené v PlayerEquipment"
```

---

### Task 3: Editor setup + manuální ověření (dělá uživatel v Unity)

Toto je kód-free — kód reference nenadrátuje (Animator Controller, InputActionReference wiring). Checklist pro uživatele:

- [ ] **Step 1: Struktura prefabu `SekyraDoRuky`**

Otevřít prefab. Zajistit strukturu: **root** (bez Animatoru hýbajícího rootem) → child **`Model`** s meshem. Pokud je mesh přímo na rootu, vytvořit prázdný child `Model` a mesh (MeshFilter/MeshRenderer nebo skinned mesh) přesunout na něj.

- [ ] **Step 2: Animator + Controller na `Model`**

Na `Model` přidat komponentu **Animator**. Vytvořit nový **Animator Controller** `SekyraController` (Project → Create → Animator Controller), přiřadit ho do pole Controller. Na Animatoru vypnout **Apply Root Motion**.

- [ ] **Step 3: Stavy a triggery v Controlleru**

V Animator okně: nechat `Idle` jako default stav (může být prázdný/loop idle klip). Přidat stavy `UsePrimary`, `UseSecondary` a případně `Draw`. Vytvořit parametry typu **Trigger**: `Draw`, `UsePrimary`, `UseSecondary`. Přechody: `Any State → <stav>` s podmínkou daného triggeru, a `<stav> → Idle` s Exit Time (bez podmínky) po dohrání klipu.

- [ ] **Step 4: Nakeyovat klipy**

Nakeyovat animace (Unity Animation okno na `Model`, nebo import FBX z Blenderu a přiřazení klipů do stavů). Klipy animují transform/kosti `Modelu` (ne rootu).

- [ ] **Step 5: Komponenta `HeldItemAnimator`**

Na `Model` přidat komponentu **HeldItemAnimator**. Zkontrolovat, že `Animator` je vyplněný (Reset ho předvyplní). Nechat/upravit jména triggerů; akci, kterou item nemá, nechat s **prázdným** jménem. Volitelně navěsit `On Primary Use` na herní logiku.

- [ ] **Step 6: Input akce**

V Input Actions assetu vytvořit akce `UsePrimary` (binding: Mouse → Left Button) a `UseSecondary` (binding: Mouse → Right Button). Uložit/`Save Asset`.

- [ ] **Step 7: Wiring na `PlayerEquipment`**

Na objektu s `PlayerEquipment` přetáhnout do polí `Primary Use Action` a `Secondary Use Action` odpovídající `InputActionReference` (UsePrimary / UseSecondary).

- [ ] **Step 8: Manuální ověření v Play mode**

Spustit hru a ověřit:
- Vytáhnutí itemu s Draw klipem → klip se přehraje jednou a překrývá se s vyjížděním nahoru.
- LMB → primární klip; RMB → sekundární klip.
- Item s prázdným triggerem → daná akce nic nedělá, žádná chyba v Console.
- Rychlý spam LMB → další švih až po `useCooldown`.
- Ve skrýši a během přepínání kapes (Q/E) → use se ignoruje.

---

## Self-Review

**Spec coverage:**
- Volitelný Draw klip navrch slide → Task 2 Step 5 (PlayDraw před AnimateHolster) + Task 1 (`PlayDraw`). ✔
- Primární/sekundární use, item si určí → Task 1 (`TryPrimaryUse`/`TrySecondaryUse`, prázdný trigger = nemá) + Task 2 Step 4. ✔
- Přidání itemu bez změny kódu → Task 3 (jen prefab + controller + wiring). ✔
- Rozdělení root/child kvůli konfliktu transformu → Task 3 Step 1 + architektura. ✔
- `useCooldown = 0.5f` → Task 1. ✔
- Volitelné UnityEvent hooky → Task 1. ✔
- Vstup přes InputActionReference, wiring dělá uživatel → Task 2 Steps 1/3/4 + Task 3 Steps 6/7. ✔
- Gating (isHidden, switchRoutine, cooldown) → Task 2 Step 4 + Task 1. ✔

**Placeholder scan:** Žádné TBD/TODO; každý krok obsahuje konkrétní kód nebo konkrétní editor akci. ✔

**Type consistency:** `PlayDraw()`, `TryPrimaryUse()`, `TrySecondaryUse()` použité v Tasku 2 odpovídají definicím v Tasku 1. Pole `currentHeldAnimator` (typ `HeldItemAnimator`) konzistentní. Triggery `Draw`/`UsePrimary`/`UseSecondary` shodné mezi komponentou a editor setupem. ✔
