using UnityEngine;

// Crosshair (zaměřovač) je vidět jen při normálním pohybu. Schová se, když je
// otevřený inventář (Tab) NEBO když je hráč schovaný ve skrýši. Plynule najede
// (fade in) a plynule zmizí (fade out) – stejně jako kapsy.
//
// Skript patří přímo na GameObject "Crosshair". CanvasGroup si přidá sám, když
// tam není, takže není potřeba nic ručně nastavovat. GameObject musí zůstat
// AKTIVNÍ (nezapínej/nevypínej ho přes SetActive) – o viditelnost se stará
// tenhle skript.
public class CrosshairUI : MonoBehaviour
{
    [Header("Nastavení")]
    [Tooltip("Jak dlouho (v sekundách) trvá fade in, když se crosshair zase objeví.")]
    [SerializeField] private float fadeInDuration = 0.15f;

    [Tooltip("Jak dlouho (v sekundách) trvá fade out, když se crosshair schová.")]
    [SerializeField] private float fadeOutDuration = 0.15f;

    [Header("Reference")]
    [Tooltip("Inventář (Tab). Když je otevřený, crosshair se schová. Prázdné = najde se automaticky.")]
    [SerializeField] private InventoryUI inventoryUI;

    [Tooltip("Sdílená data hráče (kvůli isHiding). Prázdné = vezme se z InventoryManageru.")]
    [SerializeField] private PlayerStats playerStats;

    [Tooltip("Čištění brýlí. Když zrovna běží, crosshair se schová. Prázdné = najde se automaticky.")]
    [SerializeField] private GlassesCleaningSequence glassesCleaning;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // CanvasGroup ovládá průhlednost crosshairu.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Crosshair je čistě vizuální – ať nikdy neblokuje kliknutí/raycasty pod sebou.
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void Start()
    {
        // Když nejsou reference nastavené ručně, dohledáme si je sami.
        if (inventoryUI == null)
            inventoryUI = FindAnyObjectByType<InventoryUI>();

        if (playerStats == null && InventoryManager.Instance != null)
            playerStats = InventoryManager.Instance.playerStats;

        if (glassesCleaning == null)
            glassesCleaning = FindAnyObjectByType<GlassesCleaningSequence>();

        // Alpha rovnou podle stavu, ať to na prvním snímku neproblikne.
        canvasGroup.alpha = ShouldShow() ? 1f : 0f;
    }

    private void Update()
    {
        bool show = ShouldShow();

        // Cíl: 1 = plně vidět, 0 = schované.
        float target = show ? 1f : 0f;
        float duration = show ? fadeInDuration : fadeOutDuration;

        // Krok fade podle času (když je duration 0, přepne se okamžitě).
        float step = duration > 0f ? Time.deltaTime / duration : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, step);
    }

    // Crosshair je vidět, DOKUD není otevřený inventář, hráč není schovaný a nečistí si brýle.
    private bool ShouldShow()
    {
        bool inventoryOpen = inventoryUI != null && inventoryUI.IsOpen;
        bool hiding = playerStats != null && playerStats.isHiding;
        bool cleaning = glassesCleaning != null && glassesCleaning.isRunning;
        return !inventoryOpen && !hiding && !cleaning;
    }
}
