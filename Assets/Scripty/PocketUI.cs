using UnityEngine;
using UnityEngine.InputSystem;

// Kapsy (Pocket UI) jsou vidět když hráč DRŽÍ prostřední tlačítko myši (kolečko),
// nebo když je otevřený inventář (Tab). Plynule najedou (fade in) a plynule
// zmizí (fade out).
//
// Skript patří přímo na GameObject "Kapsy". CanvasGroup si přidá sám, když tam
// není, takže není potřeba nic ručně nastavovat. GameObject musí zůstat AKTIVNÍ
// (nezapínej/nevypínej ho přes SetActive) – o viditelnost se stará tenhle skript.
public class PocketUI : MonoBehaviour
{
    [Header("Nastavení")]
    [Tooltip("Jak dlouho (v sekundách) trvá fade in při podržení.")]
    [SerializeField] private float fadeInDuration = 0.2f;

    [Tooltip("Jak dlouho (v sekundách) trvá fade out po puštění.")]
    [SerializeField] private float fadeOutDuration = 0.2f;

    [Header("Reference")]
    [Tooltip("Inventář (Tab). Když je otevřený, kapsy se taky zobrazí (kvůli drag & drop). Prázdné = najde se automaticky.")]
    [SerializeField] private InventoryUI inventoryUI;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // CanvasGroup ovládá průhlednost celého panelu kapes najednou.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Když není reference nastavená ručně, zkusíme inventář najít sami.
        if (inventoryUI == null)
            inventoryUI = FindAnyObjectByType<InventoryUI>();

        // Start schované.
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void Update()
    {
        // Drží hráč kolečko (prostřední tlačítko myši)?
        bool held = Mouse.current != null && Mouse.current.middleButton.isPressed;
        bool inventoryOpen = inventoryUI != null && inventoryUI.IsOpen;

        // Kapsy jsou vidět když hráč drží kolečko, NEBO když je otevřený inventář.
        bool show = held || inventoryOpen;

        // Cíl: 1 = plně vidět, 0 = schované.
        float target = show ? 1f : 0f;
        float duration = show ? fadeInDuration : fadeOutDuration;

        // Krok fade podle času (když je duration 0, přepne se okamžitě).
        float step = duration > 0f ? Time.deltaTime / duration : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, step);

        // Raycasty/interakci povolíme jen dokud je panel aspoň trochu vidět,
        // ať schované kapsy neblokují nic pod sebou.
        bool visible = canvasGroup.alpha > 0.001f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }
}
