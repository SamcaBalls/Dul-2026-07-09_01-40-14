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

    [Header("Vytažení / schování (Draw & Holster)")]
    [Tooltip("O kolik metrů předmět sjede dolů, když se schovává (mimo záběr).")]
    [SerializeField] private float holsterDistance = 0.5f;
    [Tooltip("Jak dlouho (v sekundách) trvá vytažení nahoru / schování dolů.")]
    [SerializeField] private float holsterDuration = 0.2f;

    // Uložené předměty v kapsách
    private ItemData pocket1Data;
    private ItemData pocket2Data;

    // 0 = nic nedrží, 1 = drží předmět z Kapsy 1 (Q), 2 = drží předmět z Kapsy 2 (E)
    private int activePocket = 0;
    private GameObject currentHeldObject; // Aktuálně vytvořený 3D/2D model v ruce
    private float bobTimer = 0f; // Čas pro výpočet houpání drženého předmětu

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
    }

    private void OnDisable()
    {
        if (pocket1Action != null) pocket1Action.action.Disable();
        if (pocket2Action != null) pocket2Action.action.Disable();
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

            ApplyHeldItemTransform();
        }
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

        currentHeldObject.transform.localPosition = new Vector3(bobX, bobY - slide, 0f);
        currentHeldObject.transform.localRotation = Quaternion.Euler(tilt, 0f, tilt * 0.5f);
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
        }

        // 2) Když má nový item model, vytvoří se dole a vyjede nahoru (přijede zespoda).
        if (targetData != null && targetData.inGamePrefab != null)
        {
            currentHeldObject = Instantiate(targetData.inGamePrefab, handSocket);
            currentHeldObject.transform.localRotation = Quaternion.identity;

            bobTimer = 0f;        // houpání začne z klidu
            holsterAmount = 1f;   // start schovaný dole
            ApplyHeldItemTransform();

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