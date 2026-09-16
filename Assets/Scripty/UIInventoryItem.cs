using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class UIInventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ItemData itemData;
    [HideInInspector] public int currentPocketIndex = 0; // 0 = v batohu, 1/2 = v kapse
    private Image itemImage;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Canvas parentCanvas;

    // Slouží k vrácení itemu, pokud ho hráč pustí mimo zóny nebo do plné kapsy
    private Transform originalParent;
    private Vector2 originalPosition;
    private Quaternion originalRotation;

    private void Awake()
    {
        itemImage = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void Setup(ItemData data)
    {
        itemData = data;

        // Awake nemusel proběhnout, když je item vytvořený pod vypnutým (zavřeným) inventářem.
        if (itemImage == null) itemImage = GetComponent<Image>();

        itemImage.sprite = data.itemIcon;
        itemImage.SetNativeSize();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Uložíme si původní hodnoty
        originalParent = transform.parent;
        originalPosition = rectTransform.anchoredPosition;
        originalRotation = rectTransform.localRotation;
        
        // Přepneme parenta rovnou na hlavní Canvas, aby se item vykresloval PŘED vším ostatním
        transform.SetParent(parentCanvas.transform, true);
        
        canvasGroup.blocksRaycasts = false; // Vypneme kolizi myši, ať trefíme zónu pod itemem
        canvasGroup.alpha = 0.8f; // Trochu item zprůhledníme během tahání
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Pohybujeme itemem za myší (zohledňujeme scale Canvasu)
        rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true; // Zase zapneme
        canvasGroup.alpha = 1f;

        // Pokud má item na konci tahu stále jako parenta hlavní Canvas, 
        // znamená to, že DropZone ho NEPŘIJALA (byl puštěn mimo, nebo je kapsa plná)
        if (transform.parent == parentCanvas.transform)
        {
            // Vrátíme ho přesně tam, odkud ho vzal
            transform.SetParent(originalParent, true);
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localRotation = originalRotation;
        }
    }
}