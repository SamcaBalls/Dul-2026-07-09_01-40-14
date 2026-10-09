using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerEquipment : MonoBehaviour
{
    public static PlayerEquipment Instance;

    [Header("Ruka hráče")]
    public Transform handSocket; // Jediný bod na hráči, kde drží aktivní předmět

    [Header("Input System")]
    [SerializeField] private InputActionReference pocket1Action; // Q – Kapsa 1
    [SerializeField] private InputActionReference pocket2Action; // E – Kapsa 2
    [SerializeField] private InputActionReference primaryUseAction;   // Levé tlačítko – primární použití
    [SerializeField] private InputActionReference secondaryUseAction; // Pravé tlačítko – sekundární použití

    [Header("Houpání drženého předmětu (Bob)")]
    [Tooltip("Zapne jemné houpání předmětu v ruce (idle bob).")]
    [SerializeField] private bool bobEnabled = true;
    [Tooltip("Rychlost houpání ve svislé ose (kmity za sekundu).")]
    [SerializeField] private float bobFrequency = 1.0f;
    [Tooltip("Svislý rozkmit houpání (v metrech). Malé číslo = jemné.")]
    [SerializeField] private float bobVerticalAmplitude = 0.015f;
    [Tooltip("Vodorovný rozkmit houpání (v metrech).")]
    [SerializeField] private float bobHorizontalAmplitude = 0.008f;
    [Tooltip("Jemné natočení předmětu během houpání (ve stupních).")]
    [SerializeField] private float bobRotationAmplitude = 1.2f;

    [Header("Sway (zaostávání předmětu při otáčení pohledu)")]
    [Tooltip("Zapne jemné zaostávání předmětu při otáčení pohledu.")]
    [SerializeField] private bool swayEnabled = true;
    [Tooltip("Síla zaostávání v poloze. Vyšší = předmět víc ujede stranou při otáčení.")]
    [SerializeField] private float swayPositionAmount = 0.02f;
    [Tooltip("Síla naklonění předmětu při otáčení.")]
    [SerializeField] private float swayRotationAmount = 4f;
    [Tooltip("Jak rychle se sway vrací do klidu / dohání pohled. Vyšší = svižnější.")]
    [SerializeField] private float swaySmoothing = 8f;
    [Tooltip("Maximální posun swaye (v metrech) – ořez při rychlém otáčení.")]
    [SerializeField] private float swayMaxOffset = 0.05f;
    [Tooltip("Maximální náklon swaye (ve stupních) – ořez při rychlém otáčení.")]
    [SerializeField] private float swayMaxAngle = 8f;

    [Header("Vytažení / schování (Draw & Holster)")]
    [Tooltip("O kolik metrů předmět sjede dolů, když se schovává (mimo záběr).")]
    [SerializeField] private float holsterDistance = 0.5f;
    [Tooltip("Jak dlouho (v sekundách) trvá vytažení nahoru / schování dolů.")]
    [SerializeField] private float holsterDuration = 0.2f;

    [Header("Házení")]
    [Tooltip("Odkud se počítá pozice a směr hodu (obvykle kamera hráče). Prázdné = Camera.main.")]
    [SerializeField] private Transform throwOrigin;

    // Uložené předměty v kapsách
    private ItemData pocket1Data;
    private ItemData pocket2Data;

    // 0 = nic nedrží, 1 = drží předmět z Kapsy 1 (Q), 2 = drží předmět z Kapsy 2 (E)
    private int activePocket = 0;
    private GameObject currentHeldObject; // Aktuálně vytvořený 3D/2D model v ruce
    private HeldItemAnimator currentHeldAnimator; // Animátor klipů aktuálně drženého itemu (může být null)
    private float bobTimer = 0f; // Čas pro výpočet houpání drženého předmětu

    // Sway: zaostávání předmětu podle změny rotace pohledu (handSocket) mezi snímky.
    private Quaternion lastSocketRotation;          // world rotace handSocketu z minulého snímku
    private bool hasLastSocketRotation = false;     // false = ještě nemáme referenci (přeskočíme první snímek)
    private Vector3 swayPositionOffset = Vector3.zero;
    private Vector3 swayRotationOffset = Vector3.zero; // (pitch, yaw, roll) ve stupních

    // 0 = plně vytažený (klidová pozice), 1 = schovaný dole mimo záběr
    private float holsterAmount = 0f;
    private bool isHidden = false;        // Vynucené schování (např. ve skrýši)
    private Coroutine switchRoutine;      // Běžící přechod (vytažení/schování)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        if (pocket1Action != null) pocket1Action.action.Enable();
        if (pocket2Action != null) pocket2Action.action.Enable();
        if (primaryUseAction != null) primaryUseAction.action.Enable();
        if (secondaryUseAction != null) secondaryUseAction.action.Enable();
    }

    private void OnDisable()
    {
        if (pocket1Action != null) pocket1Action.action.Disable();
        if (pocket2Action != null) pocket2Action.action.Disable();
        if (primaryUseAction != null) primaryUseAction.action.Disable();
        if (secondaryUseAction != null) secondaryUseAction.action.Disable();
    }

    private void Update()
    {
        // Ve skrýši je předmět schovaný – přepínání kapes ignorujeme.
        if (!isHidden)
        {
            // Stisknutím Q přepne na Kapsu 1
            if (pocket1Action != null && pocket1Action.action.WasPressedThisFrame())
            {
                SelectPocket(1);
            }
            // Stisknutím E přepne na Kapsu 2
            else if (pocket2Action != null && pocket2Action.action.WasPressedThisFrame())
            {
                SelectPocket(2);
            }
        }

        // Posuneme čas houpání a překreslíme pozici předmětu (bob + holster).
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
    }

    // Spočítá jemné zaostávání předmětu podle toho, jak rychle se otáčí pohled (world rotace handSocketu).
    // Nepotřebuje žádnou InputActionReference – čte přímo změnu rotace mezi snímky.
    private void UpdateSway()
    {
        Vector3 targetPos = Vector3.zero;
        Vector3 targetRot = Vector3.zero;

        if (swayEnabled && handSocket != null && Time.deltaTime > 0f)
        {
            if (hasLastSocketRotation)
            {
                // O kolik stupňů se pohled otočil za tento snímek (yaw = do stran, pitch = nahoru/dolů).
                Quaternion delta = Quaternion.Inverse(lastSocketRotation) * handSocket.rotation;
                Vector3 e = delta.eulerAngles;
                float dPitch = Mathf.DeltaAngle(0f, e.x);
                float dYaw = Mathf.DeltaAngle(0f, e.y);

                // Úhlová rychlost (stupně/s) → nezávislé na počtu snímků za sekundu.
                float yawVel = dYaw / Time.deltaTime;
                float pitchVel = dPitch / Time.deltaTime;

                // Předmět „zaostává" = jde opačným směrem než pohled, s ořezem při rychlém otáčení.
                targetPos.x = Mathf.Clamp(-yawVel * swayPositionAmount * 0.01f, -swayMaxOffset, swayMaxOffset);
                targetPos.y = Mathf.Clamp(pitchVel * swayPositionAmount * 0.01f, -swayMaxOffset, swayMaxOffset);

                targetRot.x = Mathf.Clamp(pitchVel * swayRotationAmount * 0.01f, -swayMaxAngle, swayMaxAngle);
                targetRot.y = Mathf.Clamp(-yawVel * swayRotationAmount * 0.01f, -swayMaxAngle, swayMaxAngle);
                targetRot.z = Mathf.Clamp(-yawVel * swayRotationAmount * 0.01f, -swayMaxAngle, swayMaxAngle); // jemný „bank" do zatáčky
            }

            lastSocketRotation = handSocket.rotation;
            hasLastSocketRotation = true;
        }
        else
        {
            hasLastSocketRotation = false;
        }

        // Plynulé dohánění / návrat do klidu (nezávislé na FPS).
        float t = 1f - Mathf.Exp(-swaySmoothing * Time.deltaTime);
        swayPositionOffset = Vector3.Lerp(swayPositionOffset, targetPos, t);
        swayRotationOffset = Vector3.Lerp(swayRotationOffset, targetRot, t);
    }

    // Nastaví lokální pozici/rotaci předmětu v ruce = jemné houpání (bob) + posun při schování (holster).
    private void ApplyHeldItemTransform()
    {
        if (currentHeldObject == null) return;

        // Bob: jemné houpání kolem klidové pozice.
        float bobX = 0f, bobY = 0f, tilt = 0f;
        if (bobEnabled)
        {
            bobY = Mathf.Sin(bobTimer) * bobVerticalAmplitude;
            bobX = Mathf.Sin(bobTimer * 0.5f) * bobHorizontalAmplitude;
            tilt = Mathf.Sin(bobTimer * 0.5f) * bobRotationAmplitude;
        }

        // Holster: 0 = klidová pozice, 1 = sjetý dolů o holsterDistance (plynule přes SmoothStep).
        float slide = Mathf.SmoothStep(0f, 1f, holsterAmount) * holsterDistance;

        // Bob (houpání) + sway (zaostávání při otáčení) se sečtou.
        currentHeldObject.transform.localPosition = new Vector3(bobX, bobY - slide, 0f) + swayPositionOffset;
        currentHeldObject.transform.localRotation = Quaternion.Euler(
            tilt + swayRotationOffset.x,
            swayRotationOffset.y,
            tilt * 0.5f + swayRotationOffset.z);
    }

    // Volá se z UI při vložení itemu do kapsy
    public void EquipItem(int pocketIndex, ItemData itemData)
    {
        if (pocketIndex == 1) pocket1Data = itemData;
        if (pocketIndex == 2) pocket2Data = itemData;

        // Pokud drží zrovna tuto kapsu, aktualizuje předmět v ruce
        if (activePocket == pocketIndex)
        {
            RefreshHeldItem();
        }
    }

    // Volá se z UI při vyndání itemu z kapsy
    public void UnequipItem(int pocketIndex)
    {
        if (pocketIndex == 1) pocket1Data = null;
        if (pocketIndex == 2) pocket2Data = null;

        // Pokud vyndal předmět z kapsy, kterou zrovna držel, zruší ho z ruky
        if (activePocket == pocketIndex)
        {
            RefreshHeldItem();
        }
    }

    // Výběr aktivní kapsy
    public void SelectPocket(int pocketIndex)
    {
        // Pokud zmáčkne klávesu kapsy, kterou už drží, předmět schová (zruší výběr)
        if (activePocket == pocketIndex)
        {
            activePocket = 0;
        }
        else
        {
            activePocket = pocketIndex;
        }

        RefreshHeldItem();
    }

    // Vynucené schování předmětu (volá HidingSpot při vstupu/výstupu ze skrýše).
    public void SetHidden(bool hidden)
    {
        if (isHidden == hidden) return;
        isHidden = hidden;

        // hidden = true  → cílem není žádný předmět → sjede dolů a zmizí.
        // hidden = false → znovu vytáhne předmět z aktivní kapsy (vyjede nahoru).
        RefreshHeldItem();
    }

    // Hodí aktuálně držený item do světa: spawne worldPrefab (v ruce = spawnPoint,
    // jinak před kamerou), pošle ho ve směru pohledu, item spotřebuje (ubere z kapsy
    // i inventáře) a vyprázdní ruku.
    // Vrací spawnutý objekt (nebo null, když se hodit nedá), ať si volající dořeší
    // navazující stav (např. přenos svícení glowsticku).
    public GameObject ThrowActiveHeldItem(float force, float up, Transform spawnPoint = null)
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

        // Spawn tam, kde je item v ruce (plynulé navázání na animaci hodu);
        // bez spawnPointu kousek před kamerou, ať objekt nevznikne uvnitř hráče.
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : origin.position + origin.forward * 0.5f;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : origin.rotation;
        GameObject obj = Instantiate(prefab, spawnPos, spawnRot);

        // Aby šel odhozený item zase sebrat.
        WorldItem worldItem = obj.GetComponent<WorldItem>();
        if (worldItem == null) worldItem = obj.AddComponent<WorldItem>();
        worldItem.itemData = data;

        // Při hodu dolů by spawn padl do kapsle hráče → odhozený kus nesmí kolidovat s hráčem.
        Rigidbody playerBody = GetComponentInParent<Rigidbody>();
        if (playerBody != null)
        {
            Collider[] playerColliders = playerBody.GetComponentsInChildren<Collider>();
            foreach (Collider thrownCol in obj.GetComponentsInChildren<Collider>())
                foreach (Collider playerCol in playerColliders)
                    Physics.IgnoreCollision(thrownCol, playerCol);
        }

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

    // Item, který má být právě v ruce (podle aktivní kapsy).
    private ItemData GetActiveData()
    {
        if (activePocket == 1) return pocket1Data;
        if (activePocket == 2) return pocket2Data;
        return null;
    }

    // Spustí přechod na aktuálně platný předmět (schová starý dolů, vytáhne nový nahoru).
    private void RefreshHeldItem()
    {
        // Ve skrýši nic nedržíme, jinak předmět z aktivní kapsy.
        ItemData targetData = isHidden ? null : GetActiveData();

        if (switchRoutine != null) StopCoroutine(switchRoutine);
        switchRoutine = StartCoroutine(SwitchHeldItemRoutine(targetData));
    }

    private IEnumerator SwitchHeldItemRoutine(ItemData targetData)
    {
        // 1) Když něco držíme, sjede to dolů (zajede) a zničíme to.
        if (currentHeldObject != null)
        {
            yield return AnimateHolster(1f);
            Destroy(currentHeldObject);
            currentHeldObject = null;
            currentHeldAnimator = null;
        }

        // 2) Když má nový item model, vytvoří se dole a vyjede nahoru (přijede zespoda).
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

        switchRoutine = null;
    }

    // Plynule přesune holsterAmount na cíl (0 = vytažený, 1 = schovaný) za holsterDuration sekund.
    private IEnumerator AnimateHolster(float target)
    {
        while (!Mathf.Approximately(holsterAmount, target))
        {
            float step = holsterDuration > 0f ? Time.deltaTime / holsterDuration : 1f;
            holsterAmount = Mathf.MoveTowards(holsterAmount, target, step);
            ApplyHeldItemTransform();
            yield return null;
        }

        holsterAmount = target;
        ApplyHeldItemTransform();
    }
}