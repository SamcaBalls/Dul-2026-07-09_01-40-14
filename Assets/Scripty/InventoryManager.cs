using System.Collections;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    public PlayerStats playerStats;

    [Header("UI Reference")]
    public RectTransform backpackArea; // UI Panel batohu
    public GameObject itemPrefab; // Prefab itemu s UIInventoryItem

    [Header("Vysypání batohu (Spill)")]
    [Tooltip("Jak dlouho trvá, než item dopadne na místo.")]
    public float spillDuration = 0.35f;
    [Tooltip("Max náhodné zpoždění startu mezi jednotlivými kusy (kaskáda).")]
    public float spillMaxDelay = 0.15f;

    [Header("Vyhazování na zem (Drop)")]
    [Tooltip("Odkud se počítá pozice a směr vyhození (obvykle kamera hráče). Prázdné = zkusí se Camera.main.")]
    public Transform dropOrigin;
    [Tooltip("Jak daleko před hráče se item vyhodí (v metrech).")]
    public float dropForwardDistance = 0.8f;
    [Tooltip("Svislý posun místa spawnu oproti dropOrigin (např. -0.2 = kousek pod úroveň očí).")]
    public float dropHeightOffset = -0.2f;
    [Tooltip("Jak silně se item pošle dopředu při vyhození (jen když má Rigidbody).")]
    public float dropForce = 1.5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Reset váhy při startu hry
        playerStats.currentTotalWeight = 0f;

        // Když není origin nastavený ručně, zkusíme hlavní kameru.
        if (dropOrigin == null && Camera.main != null)
            dropOrigin = Camera.main.transform;
    }

    // Vyhodí item z batohu do světa – ubere váhu a spawne fyzický objekt před hráče.
    // Volá UIInventoryItem, když hráč přetáhne item ven z batohu i kapes.
    public void DropItemToWorld(ItemData itemData)
    {
        if (itemData == null) return;

        // Item opouští batoh → ubereme jeho váhu (neklesneme pod nulu).
        playerStats.currentTotalWeight = Mathf.Max(0f, playerStats.currentTotalWeight - itemData.weight);

        // Přednostně worldPrefab, jinak fallback na inGamePrefab.
        GameObject prefab = itemData.worldPrefab != null ? itemData.worldPrefab : itemData.inGamePrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[Inventory] Item '{itemData.itemName}' nemá worldPrefab ani inGamePrefab – nelze vyhodit.");
            return;
        }

        Transform origin = dropOrigin != null ? dropOrigin : (Camera.main != null ? Camera.main.transform : null);
        if (origin == null)
        {
            Debug.LogWarning("[Inventory] Chybí dropOrigin i Camera.main – item se nedá kam vyhodit.");
            return;
        }

        // Vodorovný směr pohledu (bez sklonu nahoru/dolů), ať item padá rozumně před hráče.
        Vector3 forward = origin.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : origin.forward;

        Vector3 spawnPos = origin.position + forward * dropForwardDistance + Vector3.up * dropHeightOffset;

        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);

        // Zajistíme, že objekt ve světě ví, jaký je to item (aby šel zase sebrat).
        WorldItem worldItem = obj.GetComponent<WorldItem>();
        if (worldItem == null) worldItem = obj.AddComponent<WorldItem>();
        worldItem.itemData = itemData;

        // Když má Rigidbody, lehce ho pošleme dopředu, ať se odkutálí od hráče.
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.AddForce(forward * dropForce, ForceMode.VelocityChange);
    }

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

    // Voláš odkudkoliv ze hry, když hráč něco sebere ze země
    public bool TryAddRandomItemToBackpack(ItemData newItem)
    {
        if (playerStats.currentTotalWeight + newItem.weight <= playerStats.maxCapacity)
        {
            playerStats.currentTotalWeight += newItem.weight;
            SpawnItemInBackpackUI(newItem);
            return true;
        }
        else
        {
            Debug.Log("Batoh je plný! Neuneseš to.");
            return false;
        }
    }

    private void SpawnItemInBackpackUI(ItemData itemData)
    {
        GameObject newItemObject = Instantiate(itemPrefab, backpackArea);
        UIInventoryItem uiItem = newItemObject.GetComponent<UIInventoryItem>();
        uiItem.Setup(itemData);

        // Hodíme ho na random souřadnice + random rotaci (efekt bordelu)
        RectTransform rect = newItemObject.GetComponent<RectTransform>();
        rect.anchoredPosition = RandomPointInBackpack();
        rect.localRotation = Quaternion.Euler(0, 0, Random.Range(-45f, 45f));
    }

    // Náhodný bod uvnitř plochy batohu.
    private Vector2 RandomPointInBackpack()
    {
        return new Vector2(
            Random.Range(backpackArea.rect.xMin, backpackArea.rect.xMax),
            Random.Range(backpackArea.rect.yMin, backpackArea.rect.yMax));
    }

    // Animovaně "vysype" všechny itemy v batohu na nová náhodná místa – pokaždé jinak.
    public void ScatterBackpackItems()
    {
        if (backpackArea == null) return;

        StopAllCoroutines(); // zruší případné probíhající vysypání (rychlé mačkání Tabu)
        StartCoroutine(SpillAllRoutine());
    }

    private IEnumerator SpillAllRoutine()
    {
        // Pro každý item spustíme vlastní "pád" s náhodným zpožděním (kaskáda).
        foreach (RectTransform item in backpackArea)
        {
            Vector2 target = RandomPointInBackpack();
            float targetRot = Random.Range(-45f, 45f);
            float delay = Random.Range(0f, spillMaxDelay);
            StartCoroutine(SpillItemRoutine(item, target, targetRot, delay));
        }
        yield return null;
    }

    private IEnumerator SpillItemRoutine(RectTransform item, Vector2 target, float targetRot, float delay)
    {
        // Start nad horním okrajem plochy, náhodné X a náhodné natočení (tumbling).
        Vector2 start = new Vector2(
            Random.Range(backpackArea.rect.xMin, backpackArea.rect.xMax),
            backpackArea.rect.yMax);
        float startRot = Random.Range(-180f, 180f);

        item.anchoredPosition = start;
        item.localRotation = Quaternion.Euler(0, 0, startRot);

        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < spillDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / spillDuration);
            float ease = 1f - (1f - k) * (1f - k); // ease-out = dopad se zpomalením
            item.anchoredPosition = Vector2.Lerp(start, target, ease);
            item.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(startRot, targetRot, ease));
            yield return null;
        }

        item.anchoredPosition = target;
        item.localRotation = Quaternion.Euler(0, 0, targetRot);
    }
}