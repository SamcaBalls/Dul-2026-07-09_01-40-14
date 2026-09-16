using UnityEngine;
using UnityEngine.InputSystem;

// Otevírání/zavírání inventáře klávesou Tab.
// Panel batohu (BackpackArea) plynule najede (fade in) a zmizí (fade out) –
// stejně jako kapsy. Zmrazí POUZE pohled kamery (WASD pohyb i nepřátelé běží
// dál) a odemkne myš pro drag & drop.
public class InventoryUI : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Kořenový GameObject panelu inventáře (BackpackArea), který se fade-uje. TOHLE MUSÍŠ PŘETÁHNOUT.")]
    [SerializeField] private GameObject inventoryPanel;

    [Tooltip("Pohyb hráče kvůli zmrazení pohledu. Když necháš prázdné, najde se automaticky.")]
    [SerializeField] private PlayerRigidbodyMovement playerMovement;

    [Header("Nastavení")]
    [Tooltip("Klávesa pro otevření/zavření inventáře.")]
    [SerializeField] private Key toggleKey = Key.Tab;

    [Tooltip("Má být inventář otevřený hned na startu hry?")]
    [SerializeField] private bool startOpen = false;

    [Tooltip("Jak dlouho (v sekundách) trvá fade in při otevření.")]
    [SerializeField] private float fadeInDuration = 0.2f;

    [Tooltip("Jak dlouho (v sekundách) trvá fade out při zavření.")]
    [SerializeField] private float fadeOutDuration = 0.2f;

    public bool IsOpen { get; private set; }

    // CanvasGroup ovládá průhlednost celého panelu batohu (i itemů uvnitř) najednou.
    private CanvasGroup panelGroup;

    private void Awake()
    {
        if (inventoryPanel != null)
        {
            // Panel necháváme AKTIVNÍ – viditelnost řeší fade přes CanvasGroup.
            inventoryPanel.SetActive(true);

            panelGroup = inventoryPanel.GetComponent<CanvasGroup>();
            if (panelGroup == null)
                panelGroup = inventoryPanel.AddComponent<CanvasGroup>();

            // Start schované (ať to na prvním snímku neproblikne).
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        // Když není reference nastavená ručně, zkusíme hráče najít sami.
        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerRigidbodyMovement>();

        SetInventoryOpen(startOpen);

        // Alpha rovnou podle stavu, bez fade na úplně prvním snímku.
        if (panelGroup != null)
            panelGroup.alpha = IsOpen ? 1f : 0f;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            SetInventoryOpen(!IsOpen);

        // Fade panelu batohu podle toho, jestli je inventář otevřený.
        if (panelGroup != null)
        {
            float target = IsOpen ? 1f : 0f;
            float duration = IsOpen ? fadeInDuration : fadeOutDuration;

            float step = duration > 0f ? Time.deltaTime / duration : 1f;
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, target, step);

            // Raycasty/interakci povolíme jen dokud je panel aspoň trochu vidět.
            bool visible = panelGroup.alpha > 0.001f;
            panelGroup.blocksRaycasts = visible;
            panelGroup.interactable = visible;
        }
    }

    // Otevře/zavře inventář. Dá se volat i odjinud (např. tlačítkem v UI).
    public void SetInventoryOpen(bool open)
    {
        IsOpen = open;

        // Vizuální on/off řeší fade v Update (panel zůstává aktivní).

        // Při každém otevření se itemy "vysypou" na nová náhodná místa (pokaždé jinak).
        if (open && InventoryManager.Instance != null)
            InventoryManager.Instance.ScatterBackpackItems();

        // Zmrazí jen pohled kamery – pohyb (WASD) běží dál.
        if (playerMovement != null)
            playerMovement.CanLook = !open;

        // Odemkne myš pro drag & drop, při zavření ji zase zamkne a schová.
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }
}
