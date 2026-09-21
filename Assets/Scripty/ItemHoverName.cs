using TMPro;
using UnityEngine;

// Píše jméno itemu, na kterém má hráč kursor, do předpřipraveného textu.
// Ukazuje se JEN když je otevřený batoh (inventář). Když kursor z itemu sjede
// nebo se batoh zavře, text se vyprázdní.
//
// Použití: hoď tenhle skript na libovolný UI objekt (klidně přímo na ten text)
// a do pole "Name Text" přetáhni svůj předpřipravený TextMeshPro text.
public class ItemHoverName : MonoBehaviour
{
    public static ItemHoverName Instance;

    [Header("Reference")]
    [Tooltip("Předpřipravený text, do kterého se píše jméno itemu pod kursorem.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("Inventář (batoh). Jméno se ukazuje jen když je otevřený. Prázdné = najde se automaticky.")]
    [SerializeField] private InventoryUI inventoryUI;

    // Item, na kterém má hráč právě kursor (null = na žádném).
    private UIInventoryItem hovered;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        if (inventoryUI == null)
            inventoryUI = FindAnyObjectByType<InventoryUI>();
    }

    private void Update()
    {
        if (nameText == null) return;

        // Jméno ukazujeme jen když je hráč v batohu (otevřený inventář).
        bool inBackpack = inventoryUI == null || inventoryUI.IsOpen;

        // Unity null-check (hovered != null) zachytí i zničený item.
        string desired = (inBackpack && hovered != null && hovered.itemData != null)
            ? hovered.itemData.itemName
            : string.Empty;

        if (nameText.text != desired)
            nameText.text = desired;
    }

    // Volá UIInventoryItem, když na něj kursor najede / z něj sjede.
    public void SetHovered(UIInventoryItem item, bool isHovered)
    {
        if (isHovered)
            hovered = item;
        else if (hovered == item)
            hovered = null;
    }
}
