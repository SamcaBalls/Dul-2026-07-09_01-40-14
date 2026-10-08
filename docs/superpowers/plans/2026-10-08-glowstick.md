# Glowstick Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dát itemu Glowstick dvě akce – levé tlačítko ho rozsvítí při zásahu povrchu, pravé ho hodí a rozsvítí podle síly dopadu (a item přitom spotřebuje).

**Architecture:** Tři nové komponenty – `GlowstickGlow` (sdílené „svítící zařízení": Light + emise + plynulé vyhasínání, na drženém i světovém prefabu), `HeldGlowstick` (ruka: odebírá eventy `HeldItemAnimator`, levé = raycast+rozsvícení, pravé = hod), `GlowstickImpact` (svět: `OnCollisionEnter` → jas podle rychlosti). Plus dvě malé veřejné metody: `PlayerEquipment.ThrowActiveHeldItem` (spawn + impulz + spotřeba + vyprázdnění ruky) a `InventoryManager.RemovePocketItem` (ubere váhu + zničí UI prvek kapsy).

**Tech Stack:** Unity (C#, MonoBehaviour), nový Input System (už zapojený v `PlayerEquipment`), URP. Vstup se napojuje přes runtime odběr `UnityEvent`ů `HeldItemAnimator` (varianta A specu) – žádné drátování v YAML.

**Spec:** `docs/superpowers/specs/2026-10-08-glowstick-design.md`

## Poznámka k testování

Projekt **nemá** automatizované testy ani Unity Test Framework a schválený spec říká ověření ručně ve scéně. Proto u každého code-tasku je „verifikace" = **přepnout do Unity, nechat překompilovat, zkontrolovat Console, že nejsou chyby**. Chování se ověřuje ručně v Play mode v posledním tasku. Nezavádíme test framework (scope creep, mimo spec).

Každý nový `.cs` soubor má vedle sebe `.meta`, který **vygeneruje Unity** při importu. Workflow commitu každého code-tasku: napsat `.cs` → přepnout do Unity (recompile + vytvoří `.meta`) → Console bez chyb → commitnout `.cs` i `.cs.meta`.

---

## File Structure

**Nové soubory:**
- `Assets/Scripty/GlowstickGlow.cs` – svítící zařízení (Light + emise + fade). Jediná třída, co se stará o světlo. Nezná input ani házení.
- `Assets/Scripty/HeldGlowstick.cs` – ovladač drženého glowsticku (reakce na levé/pravé tlačítko přes eventy `HeldItemAnimator`).
- `Assets/Scripty/GlowstickImpact.cs` – ovladač světového glowsticku (rozsvícení při nárazu).

**Upravené soubory:**
- `Assets/Scripty/PlayerEquipment.cs` – nová veřejná metoda `ThrowActiveHeldItem(force, up)` + serializované pole `throwOrigin`.
- `Assets/Scripty/InventoryManager.cs` – nová veřejná metoda `RemovePocketItem(pocketIndex)`.

**Ruční nastavení v Unity (bez kódu):**
- Prefaby `GlowstickDoRuky` a `Glowstick.prefab` – přidání a nakonfigurování komponent.
- Volitelně Emission na glowstick materiálu, volitelně swing klip.

Pořadí tasků respektuje závislosti, takže po každém tasku projekt zkompiluje:
`GlowstickGlow` → `RemovePocketItem` → `ThrowActiveHeldItem` → `GlowstickImpact` → `HeldGlowstick` → wiring → playtest.

---

### Task 1: `GlowstickGlow` – svítící zařízení

**Files:**
- Create: `Assets/Scripty/GlowstickGlow.cs`

- [ ] **Step 1: Vytvořit soubor s kompletním obsahem**

```csharp
using UnityEngine;

// "Svítící zařízení" glowsticku – jediný vlastník Lightu, emise a plynulého vyhasínání.
// Sedí na drženém i světovém prefabu. Nezná input ani házení.
public class GlowstickGlow : MonoBehaviour
{
    [Header("Light")]
    [Tooltip("Point light glowsticku. Prázdné = najde se v children, a když žádný není, vytvoří se.")]
    [SerializeField] private Light glowLight;
    [Tooltip("Barva světla i emise. Default = zelená svítivka.")]
    [SerializeField] private Color glowColor = new Color(0.3f, 1f, 0.3f);
    [Tooltip("Intenzita Lightu při plném jasu (brightness01 = 1).")]
    [SerializeField] private float maxIntensity = 4f;
    [Tooltip("Dosah světla (v metrech).")]
    [SerializeField] private float lightRange = 8f;

    [Header("Vyhasínání")]
    [Tooltip("Za jak dlouho (s) od rozsvícení dohasne na nulu.")]
    [SerializeField] private float fadeDuration = 60f;
    [Tooltip("Tvar dohasínání (vstup = zbývající život 1→0, výstup = násobič jasu).")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(1f, 1f, 0f, 0f);

    [Header("Emise těla (volitelné)")]
    [Tooltip("Násobič emisní barvy pro MaterialPropertyBlock. Vidět jen když má materiál zapnutou Emission.")]
    [SerializeField] private float emissiveIntensity = 3f;
    [Tooltip("Renderery, kterým se nastavuje emise. Prázdné = všechny v children.")]
    [SerializeField] private Renderer[] glowRenderers;

    private bool isGlowing;
    private bool isSpent;
    private float life01;        // 1 = čerstvý, 0 = dohořelý
    private float brightness01;  // 0–1

    private MaterialPropertyBlock mpb;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool IsGlowing => isGlowing;
    public bool IsSpent => isSpent;
    public float Brightness01 => brightness01;
    public float Life01 => life01;

    // Předvyplnění referencí při přidání komponenty v editoru.
    private void Reset()
    {
        glowLight = GetComponentInChildren<Light>();
        glowRenderers = GetComponentsInChildren<Renderer>();
    }

    private void Awake()
    {
        if (glowLight == null) glowLight = GetComponentInChildren<Light>();
        if (glowLight == null) glowLight = gameObject.AddComponent<Light>();

        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = lightRange;
        glowLight.intensity = 0f;
        glowLight.enabled = false;

        if (glowRenderers == null || glowRenderers.Length == 0)
            glowRenderers = GetComponentsInChildren<Renderer>();

        mpb = new MaterialPropertyBlock();
        ApplyEmission(0f);
    }

    // Rozsvítí na daný jas s čerstvým životem. Dohořelý glowstick se nerozsvítí.
    public void Activate(float brightness)
    {
        if (isSpent) return;
        isGlowing = true;
        life01 = 1f;
        brightness01 = Mathf.Clamp01(brightness);
        glowLight.enabled = true;
    }

    // Navázání při hodu už svítícího kusu – pokračuje se zbytkovým životem.
    public void ActivateContinued(float brightness, float remainingLife01)
    {
        if (isSpent) return;
        brightness01 = Mathf.Clamp01(brightness);
        life01 = Mathf.Clamp01(remainingLife01);
        if (life01 <= 0f)
        {
            isGlowing = false;
            isSpent = true;
            glowLight.enabled = false;
            ApplyEmission(0f);
        }
        else
        {
            isGlowing = true;
            glowLight.enabled = true;
        }
    }

    // Označí jako dohořelý – už nikdy nesvítí (hod dohořelého kusu).
    public void MarkSpent()
    {
        isGlowing = false;
        isSpent = true;
        if (glowLight != null) glowLight.enabled = false;
        ApplyEmission(0f);
    }

    private void Update()
    {
        if (!isGlowing) return;

        life01 -= (fadeDuration > 0f) ? Time.deltaTime / fadeDuration : 1f;
        if (life01 <= 0f)
        {
            life01 = 0f;
            isGlowing = false;
            isSpent = true;
            glowLight.enabled = false;
            ApplyEmission(0f);
            return;
        }

        float k = fadeCurve.Evaluate(life01) * brightness01;
        glowLight.intensity = maxIntensity * k;
        ApplyEmission(k);
    }

    // Nastaví emisní barvu přes MaterialPropertyBlock všem rendererům (neovlivní sdílený materiál).
    private void ApplyEmission(float k)
    {
        if (glowRenderers == null || mpb == null) return;
        Color c = glowColor * (emissiveIntensity * k);
        foreach (Renderer r in glowRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
```

- [ ] **Step 2: Přepnout do Unity, nechat překompilovat**

Přepni do Unity Editoru (recompile proběhne automaticky; vytvoří se `GlowstickGlow.cs.meta`).
Expected: Console (okno **Console**) bez červených chyb. Žádné reference na nic, co ještě neexistuje – `GlowstickGlow` nezávisí na ostatních nových skriptech.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripty/GlowstickGlow.cs Assets/Scripty/GlowstickGlow.cs.meta
git commit -m "feat(glowstick): GlowstickGlow – Light + emise + plynulé vyhasínání"
```

---

### Task 2: `InventoryManager.RemovePocketItem` – spotřeba itemu z kapsy

**Files:**
- Modify: `Assets/Scripty/InventoryManager.cs` (přidat metodu za `DropItemToWorld`, cca řádek 88, před `TryAddRandomItemToBackpack`)

- [ ] **Step 1: Přidat metodu `RemovePocketItem`**

Vlož tento blok za konec metody `DropItemToWorld` (uzavírací `}` na cca řádku 87) a před komentář `// Voláš odkudkoliv ze hry, když hráč něco sebere ze země`:

```csharp
    // Kompletně odstraní item z dané kapsy: ubere jeho váhu a zničí jeho UI reprezentaci.
    // Volá PlayerEquipment při hodu drženého itemu (hod item spotřebuje).
    public void RemovePocketItem(int pocketIndex)
    {
        if (pocketIndex == 0) return;

        UIInventoryItem[] items = FindObjectsByType<UIInventoryItem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (UIInventoryItem ui in items)
        {
            if (ui.currentPocketIndex != pocketIndex) continue;

            if (ui.itemData != null)
                playerStats.currentTotalWeight =
                    Mathf.Max(0f, playerStats.currentTotalWeight - ui.itemData.weight);

            // Zrušíme případný hover text mířící na tenhle item (než ho zničíme).
            if (ItemHoverName.Instance != null)
                ItemHoverName.Instance.SetHovered(ui, false);

            Destroy(ui.gameObject);
            return;
        }
    }
```

- [ ] **Step 2: Přepnout do Unity, nechat překompilovat**

Expected: Console bez chyb. (`UIInventoryItem.currentPocketIndex`, `.itemData`, `ItemHoverName.Instance.SetHovered(UIInventoryItem, bool)` i `playerStats.currentTotalWeight` už v projektu existují – ověřeno ve `UIInventoryItem.cs`.)

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripty/InventoryManager.cs
git commit -m "feat(inventory): RemovePocketItem – odebrání itemu z kapsy (váha + UI)"
```

---

### Task 3: `PlayerEquipment.ThrowActiveHeldItem` – hod + spotřeba + vyprázdnění ruky

**Files:**
- Modify: `Assets/Scripty/PlayerEquipment.cs` (přidat serializované pole do hlavičky + metodu za `SetHidden`, cca řádek 251)

- [ ] **Step 1: Přidat serializované pole `throwOrigin`**

Vlož tento `[Header]` blok hned za řádek `[SerializeField] private float holsterDuration = 0.2f;` (konec bloku „Vytažení / schování", cca řádek 48) a před komentář `// Uložené předměty v kapsách`:

```csharp
    [Header("Házení")]
    [Tooltip("Odkud se počítá pozice a směr hodu (obvykle kamera hráče). Prázdné = Camera.main.")]
    [SerializeField] private Transform throwOrigin;
```

- [ ] **Step 2: Přidat metodu `ThrowActiveHeldItem`**

Vlož tuto metodu za konec metody `SetHidden` (uzavírací `}` cca na řádku 251) a před metodu `GetActiveData`:

```csharp
    // Hodí aktuálně držený item do světa: spawne worldPrefab před kamerou, pošle ho
    // dopředu, item spotřebuje (ubere z kapsy i inventáře) a vyprázdní ruku.
    // Vrací spawnutý objekt (nebo null, když se hodit nedá), ať si volající dořeší
    // navazující stav (např. přenos svícení glowsticku).
    public GameObject ThrowActiveHeldItem(float force, float up)
    {
        // Hodit jde jen usazený předmět mimo skrýš (ne během vytahování/schovávání).
        if (isHidden || switchRoutine != null || currentHeldObject == null) return null;

        ItemData data = GetActiveData();
        if (data == null) return null;

        GameObject prefab = data.worldPrefab != null ? data.worldPrefab : data.inGamePrefab;
        if (prefab == null) return null;

        Transform origin = throwOrigin != null
            ? throwOrigin
            : (Camera.main != null ? Camera.main.transform : null);
        if (origin == null) return null;

        // Spawn kousek před kamerou, ať objekt nevznikne uvnitř hráče.
        Vector3 spawnPos = origin.position + origin.forward * 0.5f;
        GameObject obj = Instantiate(prefab, spawnPos, origin.rotation);

        // Aby šel odhozený item zase sebrat.
        WorldItem worldItem = obj.GetComponent<WorldItem>();
        if (worldItem == null) worldItem = obj.AddComponent<WorldItem>();
        worldItem.itemData = data;

        // Impulz dopředu + lehký oblouček.
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(origin.forward * force + Vector3.up * up, ForceMode.VelocityChange);

        // Item spotřebujeme z inventáře (ubere váhu + zničí UI prvek kapsy).
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.RemovePocketItem(activePocket);

        // Okamžité vyprázdnění ruky (bez holster animace) – item z kapsy zmizel.
        Destroy(currentHeldObject);
        currentHeldObject = null;
        currentHeldAnimator = null;

        if (activePocket == 1) pocket1Data = null;
        else if (activePocket == 2) pocket2Data = null;

        return obj;
    }
```

- [ ] **Step 3: Přepnout do Unity, nechat překompilovat**

Expected: Console bez chyb. (`InventoryManager.RemovePocketItem` existuje z Tasku 2; `isHidden`, `switchRoutine`, `currentHeldObject`, `currentHeldAnimator`, `GetActiveData`, `activePocket`, `pocket1Data`, `pocket2Data` jsou privátní členy téže třídy.)

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripty/PlayerEquipment.cs
git commit -m "feat(equipment): ThrowActiveHeldItem – hod drženého itemu + spotřeba"
```

---

### Task 4: `GlowstickImpact` – rozsvícení při nárazu (svět)

**Files:**
- Create: `Assets/Scripty/GlowstickImpact.cs`

- [ ] **Step 1: Vytvořit soubor s kompletním obsahem**

```csharp
using UnityEngine;

// Sedí na světovém prefabu glowsticku (objekt s Rigidbody). Při nárazu rozsvítí
// glowstick podle rychlosti dopadu – tvrdší náraz = jasněji.
[RequireComponent(typeof(Rigidbody))]
public class GlowstickImpact : MonoBehaviour
{
    [Tooltip("Svítící zařízení glowsticku. Prázdné = najde se v children.")]
    [SerializeField] private GlowstickGlow glow;

    [Header("Mapování nárazu na jas")]
    [Tooltip("Rychlost dopadu (m/s), pod kterou svítí minimálně.")]
    [SerializeField] private float minImpactSpeed = 2f;
    [Tooltip("Rychlost dopadu (m/s), nad kterou svítí maximálně.")]
    [SerializeField] private float maxImpactSpeed = 10f;
    [Tooltip("Jas při minimální rychlosti dopadu.")]
    [SerializeField] private float minBrightness = 0.2f;
    [Tooltip("Jas při maximální rychlosti dopadu.")]
    [SerializeField] private float maxBrightness = 1f;

    private void Awake()
    {
        if (glow == null) glow = GetComponentInChildren<GlowstickGlow>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Rozsvítí jen poprvé – svítící ani dohořelý glowstick náraz neovlivní.
        if (glow == null || glow.IsGlowing || glow.IsSpent) return;

        float speed = collision.relativeVelocity.magnitude;
        float t = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
        glow.Activate(Mathf.Lerp(minBrightness, maxBrightness, t));
    }
}
```

- [ ] **Step 2: Přepnout do Unity, nechat překompilovat**

Expected: Console bez chyb. (`GlowstickGlow` + jeho `IsGlowing`/`IsSpent`/`Activate` existují z Tasku 1.)

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripty/GlowstickImpact.cs Assets/Scripty/GlowstickImpact.cs.meta
git commit -m "feat(glowstick): GlowstickImpact – rozsvícení podle síly nárazu"
```

---

### Task 5: `HeldGlowstick` – levé (rozsvícení o povrch) + pravé (hod)

**Files:**
- Create: `Assets/Scripty/HeldGlowstick.cs`

- [ ] **Step 1: Vytvořit soubor s kompletním obsahem**

```csharp
using UnityEngine;

// Sedí na rootu drženého glowsticku (stejný objekt jako HeldItemAnimator).
// Napojí se na jeho eventy: levé = rozsvícení o povrch, pravé = hod.
[RequireComponent(typeof(HeldItemAnimator))]
public class HeldGlowstick : MonoBehaviour
{
    [Tooltip("Svítící zařízení glowsticku. Prázdné = najde se v children.")]
    [SerializeField] private GlowstickGlow glow;

    [Header("Levé tlačítko – rozsvícení o povrch")]
    [Tooltip("Dosah švihu (m).")]
    [SerializeField] private float reach = 3f;
    [Tooltip("Do kterých vrstev se švih trefí (ať netrefí hráče/sebe).")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Pravé tlačítko – hod")]
    [Tooltip("Síla hodu dopředu.")]
    [SerializeField] private float throwForce = 8f;
    [Tooltip("Svislá složka hodu (oblouček).")]
    [SerializeField] private float throwUp = 1f;

    private HeldItemAnimator heldAnimator;
    private Camera cam;

    private void Awake()
    {
        heldAnimator = GetComponent<HeldItemAnimator>();
        if (glow == null) glow = GetComponentInChildren<GlowstickGlow>();
        cam = ResolveCamera();

        if (heldAnimator != null)
        {
            heldAnimator.onPrimaryUse.AddListener(OnPrimary);
            heldAnimator.onSecondaryUse.AddListener(OnThrow);
        }
    }

    private void OnDestroy()
    {
        if (heldAnimator != null)
        {
            heldAnimator.onPrimaryUse.RemoveListener(OnPrimary);
            heldAnimator.onSecondaryUse.RemoveListener(OnThrow);
        }
    }

    // Levé: když švih zasáhne povrch na dosah → glowstick se rozsvítí naplno.
    private void OnPrimary()
    {
        if (glow == null || cam == null) return;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward,
                out RaycastHit hit, reach, hitMask, QueryTriggerInteraction.Ignore))
        {
            glow.Activate(1f);
        }
    }

    // Pravé: glowstick se hodí; stav svícení se přenese na odhozený kus.
    private void OnThrow()
    {
        bool wasGlowing = glow != null && glow.IsGlowing;
        bool wasSpent = glow != null && glow.IsSpent;
        float brightness = glow != null ? glow.Brightness01 : 0f;
        float life = glow != null ? glow.Life01 : 0f;

        if (PlayerEquipment.Instance == null) return;

        GameObject thrown = PlayerEquipment.Instance.ThrowActiveHeldItem(throwForce, throwUp);
        if (thrown == null) return;

        GlowstickGlow worldGlow = thrown.GetComponentInChildren<GlowstickGlow>();
        if (worldGlow == null) return;

        if (wasSpent) worldGlow.MarkSpent();
        else if (wasGlowing) worldGlow.ActivateContinued(brightness, life);
        // jinak: necháme GlowstickImpact rozsvítit nárazem
    }

    // Kameru dohledá jako HidingSpot/PlayerInteract → fallback Camera.main.
    private Camera ResolveCamera()
    {
        PlayerInteract interact = FindAnyObjectByType<PlayerInteract>();
        if (interact != null && interact.playerCamera != null)
            return interact.playerCamera;
        return Camera.main;
    }
}
```

- [ ] **Step 2: Přepnout do Unity, nechat překompilovat**

Expected: Console bez chyb. Závislosti existují: `GlowstickGlow` (Task 1), `PlayerEquipment.Instance.ThrowActiveHeldItem` (Task 3), `HeldItemAnimator.onPrimaryUse`/`onSecondaryUse` (veřejné `UnityEvent`), `PlayerInteract.playerCamera` (veřejné pole).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripty/HeldGlowstick.cs Assets/Scripty/HeldGlowstick.cs.meta
git commit -m "feat(glowstick): HeldGlowstick – levé rozsvícení o povrch + pravé hod"
```

---

### Task 6: Ruční nastavení prefabů v Unity

**Files:** (žádný kód – editace prefabů v Editoru; Unity je uloží jako změny v `.prefab` + případně `.mat`)
- `Assets/Prefaby/Itemy/GlowstickDoRuky.prefab`
- `Assets/Prefaby/Itemy/Glowstick.prefab`
- Volitelně `Assets/Material/...` (glowstick materiál, guid `31321ba15b8f8eb4c954353edc038b1d`)

- [ ] **Step 1: Držený prefab `GlowstickDoRuky`**

V Project okně otevři `Assets/Prefaby/Itemy/GlowstickDoRuky.prefab` (dvojklik → prefab mode).
Vyber **root** objekt `GlowstickDoRuky` (ten, co má komponentu `HeldItemAnimator`).
- Add Component → `Glowstick Glow`. Nech `Glow Light` prázdné (vyrobí se automaticky).
  Zkontroluj `Glow Color` (zelená), `Max Intensity`, `Light Range`, `Fade Duration` (60).
- Add Component → `Held Glowstick`. Nastav:
  - `Reach` = 3
  - `Hit Mask` = vrstvy, do kterých se má švih trefit (odškrtni vrstvu hráče, ať netrefí sebe).
  - `Throw Force` = 8, `Throw Up` = 1.
- Ulož prefab (Ctrl+S) a zavři prefab mode.

- [ ] **Step 2: Světový prefab `Glowstick.prefab`**

Otevři `Assets/Prefaby/Itemy/Glowstick.prefab` (má `Rigidbody` + `BoxCollider` + `WorldItem`).
Vyber **root** objekt:
- Add Component → `Glowstick Glow` (stejné defaulty, Light se vyrobí sám).
- Add Component → `Glowstick Impact`. Nastav `Min/Max Impact Speed` (2 / 10) a `Min/Max Brightness` (0.2 / 1).
- Ulož prefab.

- [ ] **Step 3: (Volitelné) Zářící tělo glowsticku**

Pokud chceš, aby zářilo i tělo glowsticku (ne jen okolí), vyber glowstick materiál
(`guid 31321ba1…`, používají ho obě mesh těla) → v Inspectoru zaškrtni **Emission**
(URP/Lit: sekce Emission → Enable). Barvu nech tmavou/černou – runtime ji přepíše
`GlowstickGlow` přes `MaterialPropertyBlock`. Bez tohoto kroku svítí jen Light.

- [ ] **Step 4: Commit**

```bash
git add Assets/Prefaby/Itemy/GlowstickDoRuky.prefab Assets/Prefaby/Itemy/Glowstick.prefab
# když jsi zapnul Emission, přidej i materiál:
# git add Assets/Material/<glowstick-material>.mat
git commit -m "chore(glowstick): komponenty na prefaby (held: Glow+Held, world: Glow+Impact)"
```

---

### Task 7: Ruční playtest ve scéně

**Files:** žádné (ověření v Play mode scény `Assets/Scenes/Movement.unity`)

- [ ] **Step 1: Příprava**

Otevři scénu `Assets/Scenes/Movement.unity`. Zajisti, že máš glowstick v kapse
(Q/E) – pokud ne, seber ho ze světa nebo přetáhni v inventáři do kapsy. Spusť Play.

- [ ] **Step 2: Levé tlačítko – rozsvícení o zeď**

Drž glowstick (Q nebo E), postav se čelem ke zdi na dosah (< `reach`) a klikni levým.
Expected: glowstick se rozsvítí zeleně a osvětlí okolí. Klikni levým do prázdna (do dálky).
Expected: žádné světlo (švih naprázdno).

- [ ] **Step 3: Vyhasínání**

Po rozsvícení sleduj světlo.
Expected: intenzita plynule klesá a za ~`fadeDuration` sekund úplně zhasne.
(Pro rychlý test můžeš `Fade Duration` dočasně snížit na ~5 v Inspectoru držené komponenty.)

- [ ] **Step 4: Pravé tlačítko – hod + spotřeba**

S glowstickem v ruce klikni pravým (miř dopředu).
Expected: glowstick vyletí dopředu, **zmizí z ruky** a **zmizí z inventáře** (otevři Tab –
není tam; váha v `PlayerStats` klesla o váhu glowsticku). Po dopadu se rozsvítí.
Hoď jiný glowstick prudce do blízké zdi vs. jen pusť na zem.
Expected: prudší/rychlejší dopad = jasnější světlo.

- [ ] **Step 5: Hod už svítícího + opětovné sebrání**

Rozsviť glowstick levým (o zeď), pak ho hoď pravým.
Expected: ve světě svítí dál (se zbytkem jasu/života), náraz ho neresetuje.
Dojdi k odhozenému glowsticku a seber ho (F).
Expected: jde sebrat zpět do batohu (má `WorldItem`).

- [ ] **Step 6: Negativní kontrola**

Ověř, že levý švih netrefí vlastní item-pickup trigger ani hráče (díky `hitMask` +
`QueryTriggerInteraction.Ignore`) – glowstick se nerozsvítí „sám od sebe" při kliknutí
mimo zeď.

- [ ] **Step 7: Commit (pokud byly dolaďovací změny hodnot v prefabech)**

```bash
git add Assets/Prefaby/Itemy/GlowstickDoRuky.prefab Assets/Prefaby/Itemy/Glowstick.prefab
git commit -m "tune(glowstick): doladění hodnot po playtestu"
```
