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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Reset váhy při startu hry
        playerStats.currentTotalWeight = 0f;
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