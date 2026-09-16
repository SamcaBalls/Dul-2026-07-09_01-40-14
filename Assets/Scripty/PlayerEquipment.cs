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

    // Uložené předměty v kapsách
    private ItemData pocket1Data;
    private ItemData pocket2Data;

    // 0 = nic nedrží, 1 = drží předmět z Kapsy 1 (Q), 2 = drží předmět z Kapsy 2 (E)
    private int activePocket = 0; 
    private GameObject currentHeldObject; // Aktuálně vytvořený 3D/2D model v ruce

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

    // Překreslení objektu v ruce
    private void RefreshHeldItem()
    {
        // Vymaže předchozí předmět z ruky
        if (currentHeldObject != null)
        {
            Destroy(currentHeldObject);
        }

        // Zjistí, jaký item má být v ruce
        ItemData activeData = null;
        if (activePocket == 1) activeData = pocket1Data;
        else if (activePocket == 2) activeData = pocket2Data;

        // Pokud v dané kapse něco je, vytvoří to v ruce
        if (activeData != null && activeData.inGamePrefab != null)
        {
            currentHeldObject = Instantiate(activeData.inGamePrefab, handSocket);
            currentHeldObject.transform.localPosition = Vector3.zero;
            currentHeldObject.transform.localRotation = Quaternion.identity;
        }
    }
}