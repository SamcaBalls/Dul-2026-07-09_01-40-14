using TMPro;
using UnityEngine;

// HUD text, který ukazuje název itemu, na který hráč právě míří a může ho sebrat (F).
// PlayerInteract sem každý snímek hlásí zaměřený WorldItem (nebo null, když na nic nemíří).
//
// Použití: hoď skript na libovolný UI objekt a do "Prompt Text" přetáhni
// předpřipravený TextMeshPro text (ideálně u zaměřovacího křížku).
public class ItemPickupPrompt : MonoBehaviour
{
    public static ItemPickupPrompt Instance;

    [Header("Reference")]
    [Tooltip("Text, do kterého se píše název zaměřeného itemu. Prázdné = nic se nepíše.")]
    [SerializeField] private TMP_Text promptText;

    [Header("Formát")]
    [Tooltip("Šablona textu pro item. {name} se nahradí názvem itemu.")]
    [SerializeField] private string format = "[F] {name}";

    [Tooltip("Text, když hráč míří na skrýš, do které se může schovat. Prázdné = nic.")]
    [SerializeField] private string hideText = "[F] Schovat se";

    // Item, na který hráč právě míří (null = na žádný sebratelný).
    private WorldItem current;

    // true = hráč míří na skrýš, do které se může schovat.
    private bool canHide;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    private void Update()
    {
        if (promptText == null) return;

        string desired;
        // Priorita: název itemu, na který hráč míří...
        // (Unity null-check current != null zachytí i zničený objekt.)
        if (current != null && current.itemData != null)
            desired = format.Replace("{name}", current.itemData.itemName);
        // ...jinak nabídka schování, když míří na použitelnou skrýš...
        else if (canHide)
            desired = hideText;
        // ...jinak nic.
        else
            desired = string.Empty;

        if (promptText.text != desired)
            promptText.text = desired;
    }

    // Volá PlayerInteract každý snímek. null = hráč nemíří na sebratelný item.
    public void SetTarget(WorldItem item)
    {
        current = item;
    }

    // Volá PlayerInteract každý snímek. true = hráč míří na skrýš, do které se může schovat.
    public void SetHideAvailable(bool value)
    {
        canHide = value;
    }
}
