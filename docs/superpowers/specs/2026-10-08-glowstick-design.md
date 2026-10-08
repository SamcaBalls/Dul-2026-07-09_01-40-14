# Glowstick – rozsvícení o zeď (levé) + hod s rozsvícením podle nárazu (pravé)

**Datum:** 2026-10-08
**Větev:** NovyNovyDih
**Stav:** návrh schválen, čeká se na implementační plán

## Cíl

Přidat itemu **Glowstick** (ve hře „Svítivka") dvě akce:

- **Levé tlačítko (primární):** hráč glowstickem švihne; když švih zasáhne zeď nebo
  jakýkoliv objekt na dosah, glowstick se **rozsvítí naplno**. Švih do prázdna se
  přehraje, ale nerozsvítí nic.
- **Pravé tlačítko (sekundární):** hráč glowstick **hodí**. Při nárazu se rozsvítí
  **podle síly dopadu** (tvrdší náraz = jasněji). Hod glowstick **spotřebuje** –
  zmizí z ruky i z inventáře (klesne váha) a zůstane ležet ve světě (jde zase sebrat).

Jakmile glowstick svítí, světlo **postupně vyhasne** za nastavitelnou dobu (default ~60 s).

## Mimo rozsah (Non-goals)

- Jiné itemy než Glowstick – tento spec řeší pouze jeho.
- `IPrimaryUse`/`ISecondaryUse` routing ze sekyra specu (`2026-09-29`) – **nezavádíme teď.**
  Komponenty tohoto návrhu se do něj později přenesou beze změny (viz „Zvolený přístup").
- Combat / poškození tvorů – glowstick nic neničí.
- Dohasínající glowstick jako spotřební zdroj světla v UI (žádný indikátor zbývajícího
  života) – řeší se jen vizuálně plynulým ztlumením.

## Zvolený přístup

Vstup se napojuje přes **runtime odběr eventů `HeldItemAnimator`** (varianta A z brainstormingu):
`HeldGlowstick` si v `Awake` najde sourozence `HeldItemAnimator` a sám se přihlásí na
jeho `onPrimaryUse` / `onSecondaryUse`. Žádné drátování `UnityEvent`ů v YAML (to je
křehké a už je to zaznamenané jako problém), žádná závislost na nehotovém sekyra systému.

Pokud se v budoucnu zavede `IPrimaryUse`/`ISecondaryUse` routing (sekyra spec), stačí
`HeldGlowstick` překlopit z odběru eventů na implementaci interfacu – `GlowstickGlow`
i `GlowstickImpact` zůstanou beze změny.

## Současný stav (co už existuje)

- **Vstup:** `PlayerEquipment.Update()` čte levé/pravé tlačítko (`primaryUseAction` /
  `secondaryUseAction`) a volá `currentHeldAnimator.TryPrimaryUse()` /
  `TrySecondaryUse()` – cooldown, Animator trigger (`UsePrimary`/`UseSecondary`),
  volitelný `UnityEvent` (`onPrimaryUse`/`onSecondaryUse`).
- **Držený prefab `GlowstickDoRuky`** (`inGamePrefab`): root s `HeldItemAnimator`
  (triggery Draw/UsePrimary/UseSecondary, eventy prázdné), child s Animatorem
  (`Item.controller`, guid `b18952f2…`) a dvě mesh-válcová těla se sdíleným materiálem
  (guid `31321ba1…`). **Žádný Light.**
- **Světový prefab `Glowstick.prefab`** (`worldPrefab`): má `Rigidbody` + `BoxCollider`
  + `WorldItem` → dá se sebrat i fyzicky hodit. **Žádný Light.**
- **Inventář:** každý item je `UIInventoryItem` (v batohu, nebo naparentovaný v kapse
  přes `currentPocketIndex`). Váha se počítá od sebrání do batohu
  (`InventoryManager.TryAddRandomItemToBackpack`). Plné odebrání = `PlayerEquipment.UnequipItem`
  + snížit váhu (`playerStats.currentTotalWeight`) + `Destroy` toho `UIInventoryItem`.
  `PlayerEquipment` zná jen `pocketXData` (ItemData), **ne** instanci `UIInventoryItem`.

## Architektura

Tři nové skripty v `Assets/Scripty/` + dvě malé veřejné metody do stávajících tříd.
Glowstick „svítící zařízení" je oddělené od kontextových ovladačů (ruka vs svět).

### 1. `GlowstickGlow` (na obou prefabech)

Nový soubor `Assets/Scripty/GlowstickGlow.cs`. Jediný vlastník světla – stará se o
Light, emisi a plynulé vyhasínání. Nezná input ani házení.

Serializovaná konfigurace:
- `Light light` – prázdné = v `Awake` se zkusí najít `GetComponentInChildren<Light>()`;
  když žádný není, **vytvoří se** (`AddComponent<Light>` na tomto objektu, Point,
  barva `glowColor`, range `lightRange`, intensity 0, `enabled = false`). → nulové drátování.
- `Color glowColor = (0.3, 1, 0.3)` – default zelená.
- `float maxIntensity = 4` – intensity Lightu při plném jasu (brightness01 = 1).
- `float lightRange = 8`.
- `float fadeDuration = 60` – za jak dlouho (s) od rozsvícení dohasne na nulu.
- `AnimationCurve fadeCurve` – tvar dohasínání v čase (default lineární 1→0).
- `float emissiveIntensity = 3` – násobič emisní barvy pro `MaterialPropertyBlock`.
- `Renderer[] glowRenderers` – prázdné = `GetComponentsInChildren<Renderer>()` v `Awake`.

Stav:
- `bool isGlowing`, `bool isSpent`, `float life01` (1 = čerstvý, 0 = dohořelý),
  `float brightness01` (0–1, nastaveno při aktivaci).

API:
- `void Activate(float brightness01)` – pokud `isSpent`, nedělá nic. Jinak
  `isGlowing = true`, `life01 = 1`, `brightness01 = Clamp01(brightness01)`, zapne Light.
- `void ActivateContinued(float brightness01, float life01)` – navázání při hodu už
  svítícího glowsticku: pokračuje se zbytkovým životem místo plného.
- `void MarkSpent()` – označí jako dohořelý (hod dohořelého glowsticku se už nerozsvítí).
- Gettery: `bool IsGlowing`, `bool IsSpent`, `float Brightness01`, `float Life01`.

`Update` (když `isGlowing`):
```
life01 -= Time.deltaTime / fadeDuration;
if (life01 <= 0) { life01 = 0; isGlowing = false; isSpent = true; vypni Light; emise = 0; }
else {
    float k = fadeCurve.Evaluate(life01);           // tvar dohasínání
    light.intensity = maxIntensity * brightness01 * k;
    // emise přes MaterialPropertyBlock na glowRenderers:
    //   _EmissionColor = glowColor.linear * emissiveIntensity * brightness01 * k
}
```
Poznámka k emisi: `MaterialPropertyBlock` umí nastavit `_EmissionColor`, ale **neumí
zapnout keyword `_EMISSION`**. Emise tělu glowsticku je proto vidět jen tehdy, když má
jeho materiál Emission zapnutou (viz „Ruční nastavení"). Světlo (Light) funguje vždy –
je to hlavní herní prvek. Pokud emise není zapnutá, glowstick jen osvětluje okolí,
ale jeho tělo nezáří.

### 2. `HeldGlowstick` (jen držený prefab)

Nový soubor `Assets/Scripty/HeldGlowstick.cs`. Sedí na rootu `GlowstickDoRuky`
(stejný objekt jako `HeldItemAnimator`). Ovládá reakci na levé/pravé tlačítko.

Serializovaná konfigurace:
- `float reach = 3` – dosah raycastu levého švihu.
- `LayerMask hitMask` – do čeho se švih trefuje (default vše kromě vrstvy hráče).
- `float throwForce = 8` – síla hodu dopředu.
- `float throwUp = 1` – malá svislá složka hodu (oblouček).

Reference (dohledané, bez ručního drátování):
- `HeldItemAnimator` – sourozenec (`GetComponent`), kvůli odběru eventů.
- `GlowstickGlow` – `GetComponentInChildren`.
- `Camera` – jako `HidingSpot`/`PlayerInteract`: přes `PlayerInteract.playerCamera`
  ve scéně → fallback `Camera.main`.

Chování:
- `Awake`: najít reference; `animator.onPrimaryUse.AddListener(OnPrimary)`,
  `animator.onSecondaryUse.AddListener(OnThrow)`.
- `OnDestroy`: odhlásit oba listenery (ať při zničení itemu nezůstanou viset).
- `OnPrimary()`:
  ```
  if (Physics.Raycast(cam.position, cam.forward, out hit, reach, hitMask, Ignore))
      glow.Activate(1f);   // zásah → rozsvítí naplno
  // jinak nic (švih naprázdno zajistí HeldItemAnimator svým triggerem)
  ```
- `OnThrow()`:
  ```
  bool wasGlowing = glow != null && glow.IsGlowing;
  bool wasSpent   = glow != null && glow.IsSpent;
  float b = glow != null ? glow.Brightness01 : 0f;
  float l = glow != null ? glow.Life01 : 0f;

  GameObject thrown = PlayerEquipment.Instance.ThrowActiveHeldItem(throwForce, throwUp);
  if (thrown == null) return;

  var worldGlow = thrown.GetComponentInChildren<GlowstickGlow>();
  if (worldGlow != null) {
      if (wasSpent)        worldGlow.MarkSpent();          // dohořelý → nerozsvítí se
      else if (wasGlowing) worldGlow.ActivateContinued(b, l); // svítí dál se zbytkem
      // jinak: nic → GlowstickImpact ho rozsvítí nárazem
  }
  ```
  (Unity `Destroy` je odložené na konec snímku, takže `this` i `glow` jsou po volání
  `ThrowActiveHeldItem` pořád platné a stav se stihne přečíst/přenést.)

### 3. `GlowstickImpact` (jen světový prefab)

Nový soubor `Assets/Scripty/GlowstickImpact.cs`. Sedí na `Glowstick.prefab` (objekt
s `Rigidbody`). Rozsvítí glowstick při nárazu podle rychlosti dopadu.

Serializovaná konfigurace:
- `float minImpactSpeed = 2`, `float maxImpactSpeed = 10` – rozsah rychlosti dopadu.
- `float minBrightness = 0.2`, `float maxBrightness = 1` – mapování na jas.

Reference: `GlowstickGlow` (`GetComponentInChildren`).

Chování:
```
OnCollisionEnter(c):
    if (glow == null || glow.IsGlowing || glow.IsSpent) return;
    float speed = c.relativeVelocity.magnitude;
    float t = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
    glow.Activate(Mathf.Lerp(minBrightness, maxBrightness, t));
```
Komponenta je oddělená od `HeldGlowstick` schválně: na drženém prefabu žádné kolize
nejsou (visí na `handSocket` bez `Rigidbody`), takže tam `GlowstickImpact` vůbec není.

## Rozšíření stávajících tříd

### `PlayerEquipment.ThrowActiveHeldItem(float force, float up)` → `GameObject`

Nová veřejná metoda. Generická (nezná glow) – hod + spotřeba + vyprázdnění ruky:

1. Pokud `isHidden`, `switchRoutine != null`, nebo `currentHeldObject == null` → `return null`.
2. `ItemData data = GetActiveData(); if (data == null) return null;`
3. `prefab = data.worldPrefab != null ? data.worldPrefab : data.inGamePrefab;`
   když je `null` → `return null`.
4. Origin hodu: serializované `throwOrigin` (volitelné) → fallback `Camera.main`.
   `spawnPos = origin.position + origin.forward * 0.5f;`
   Spawn `obj = Instantiate(prefab, spawnPos, origin.rotation)`.
5. `WorldItem` na `obj` (get-or-add), `worldItem.itemData = data`.
6. `Rigidbody rb = obj.GetComponent<Rigidbody>();`
   `if (rb) rb.AddForce(origin.forward * force + Vector3.up * up, ForceMode.VelocityChange);`
7. Spotřeba z inventáře: `InventoryManager.Instance.RemovePocketItem(activePocket);`
8. Okamžité vyprázdnění ruky (bez holster animace):
   `if (switchRoutine != null) StopCoroutine(switchRoutine);`
   `Destroy(currentHeldObject); currentHeldObject = null; currentHeldAnimator = null;`
   vynulovat `pocketXData` pro `activePocket` (→ `GetActiveData()` vrací null, ruka prázdná).
9. `return obj;`

### `InventoryManager.RemovePocketItem(int pocketIndex)`

Nová veřejná metoda. Najde UI prvek dané kapsy a kompletně ho odstraní:

```
foreach (UIInventoryItem ui in FindObjectsByType<UIInventoryItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
    if (ui.currentPocketIndex == pocketIndex) {
        playerStats.currentTotalWeight = Mathf.Max(0f, playerStats.currentTotalWeight - ui.itemData.weight);
        if (ItemHoverName.Instance != null) ItemHoverName.Instance.SetHovered(ui, false);
        Destroy(ui.gameObject);
        return;
    }
```
`FindObjectsInactive.Include`, protože panel kapes/inventáře může být zrovna skrytý
(fade na alpha 0, ale objekty žijí) – pocketovaný item je naparentovaný ve slotu kapsy.

## Toky

### Levé tlačítko (rozsvícení o zeď)
```
klik → PlayerEquipment: primaryUseAction → HeldItemAnimator.TryPrimaryUse()
       (cooldown + trigger UsePrimary = švih) + onPrimaryUse
   → HeldGlowstick.OnPrimary: Raycast z kamery (reach, hitMask, ignore triggers)
      ├─ zásah povrchu → glow.Activate(1)        (rozsvítí naplno)
      └─ nic           → švih naprázdno, žádné světlo
```

### Pravé tlačítko (hod + spotřeba)
```
klik → secondaryUseAction → HeldItemAnimator.TrySecondaryUse() + onSecondaryUse
   → HeldGlowstick.OnThrow:
        zapamatuj glow stav (wasGlowing / wasSpent / brightness / life)
        thrown = PlayerEquipment.ThrowActiveHeldItem(throwForce, throwUp)
           1. spawn worldPrefab před kamerou, WorldItem.itemData = data
           2. Rigidbody impulz forward*force + up*throwUp
           3. InventoryManager.RemovePocketItem(activePocket)  (váha + Destroy UI)
           4. zruš držený objekt + vynuluj kapsu → ruka prázdná
           5. return spawnutý objekt
        přenes glow stav na thrown:
           wasSpent   → worldGlow.MarkSpent()
           wasGlowing → worldGlow.ActivateContinued(brightness, life)
           jinak      → nic (GlowstickImpact rozsvítí nárazem podle rychlosti)
```

## Edge-cases a rozhodnutí

- **Hod už svítícího glowsticku:** ve světě svítí dál se **zbytkovým** životem
  (`ActivateContinued`), náraz ho už znovu nerozsvěcuje (`IsGlowing` blokuje `GlowstickImpact`).
- **Hod dohořelého glowsticku:** `MarkSpent` na světovém kusu → zůstane tmavý i po nárazu
  (konzistentní: jednou dohořelý glowstick už nesvítí).
- **Dvojí rozsvícení levým:** druhé `Activate` na už svítícím kusu se ignoruje (neresetuje
  život); na dohořelém taky (`isSpent`). Glowstick se tedy nedá „dobít".
- **Cooldown:** levý švih řeší `HeldItemAnimator.useCooldown`. Pravé (hod) je stejně
  jednorázové – item po hodu zmizí, žádné další eventy nepřijdou.
- **Odhozený glowstick jde zase sebrat** (má `WorldItem`); po sebrání je to zase čerstvý
  item z `ItemData` (stav světla se neukládá – přijatelné zjednodušení).

## Ruční nastavení v Unity (mimo kód)

- **`GlowstickDoRuky`** (držený): přidat `GlowstickGlow` + `HeldGlowstick`; nastavit
  `hitMask` (vrstvy, do kterých se švih trefí), `reach`, `throwForce`, `throwUp`.
- **`Glowstick.prefab`** (svět): přidat `GlowstickGlow` + `GlowstickImpact`; nastavit
  `minImpactSpeed`/`maxImpactSpeed` a `minBrightness`/`maxBrightness`.
- **Volitelně – zářící tělo:** na glowstick materiálu (guid `31321ba1…`) zapnout
  **Emission** (jinak svítí jen Light, tělo nezáří). Pozor: materiál je sdílený mezi
  held i world prefabem – zapnutím se rozsvěcovat budou obě těla, to je v pořádku.
- **Volitelně – švih:** klip „UsePrimary" do `Item.controller` (guid `b18952f2…`).
  Světlo a hod fungují i bez animace.
- Light netřeba vytvářet ručně – `GlowstickGlow` si ho vyrobí sám, pokud žádný nenajde.

## Testování

Projekt nemá automatizované testy – ověření ručně ve scéně:
- Držet glowstick (Q/E), levé u zdi na dosah → rozsvítí se zeleně a osvítí okolí;
  levé do prázdna → švih bez světla.
- Rozsvícený glowstick plynule vyhasne za `fadeDuration` a zhasne.
- Pravé → glowstick letí dopředu, **zmizí z ruky i z inventáře** (klesne váha),
  dopadne a rozsvítí se; tvrdší/rychlejší hod → jasnější světlo.
- Hod už svítícího → ve světě svítí dál se zbytkem života (náraz ho neresetuje).
- Odhozený glowstick jde ze země zase sebrat.
- Hod netrefí item-pickup triggery ani hráče (dle `hitMask`/ignore triggers u švihu).
