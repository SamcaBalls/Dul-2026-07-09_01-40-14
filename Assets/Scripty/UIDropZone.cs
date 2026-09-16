using UnityEngine;
using UnityEngine.EventSystems;

public class UIDropZone : MonoBehaviour, IDropHandler
{
    public enum ZoneType { Backpack, Pocket }
    public ZoneType zoneType;
    
    public int pocketIndex; // <-- NOVÉ: Nastavíš v Inspectoru (Kapsa 1 = 1, Kapsa 2 = 2)

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        UIInventoryItem draggedItem = eventData.pointerDrag.GetComponent<UIInventoryItem>();
        if (draggedItem == null) return;

        if (zoneType == ZoneType.Pocket)
        {
            if (transform.childCount > 0) return; // Kapsa je plná → drop odmítnut (item se vrátí)

            // Drop přijat: odhlásíme případnou starou kapsu a zaregistrujeme item do téhle.
            if (PlayerEquipment.Instance != null)
            {
                if (draggedItem.currentPocketIndex != 0)
                    PlayerEquipment.Instance.UnequipItem(draggedItem.currentPocketIndex);

                PlayerEquipment.Instance.EquipItem(pocketIndex, draggedItem.itemData);
            }
            draggedItem.currentPocketIndex = pocketIndex;

            draggedItem.transform.SetParent(transform, true);
            draggedItem.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            draggedItem.GetComponent<RectTransform>().localRotation = Quaternion.identity;
        }
        else if (zoneType == ZoneType.Backpack)
        {
            // Item se vrací do batohu → pokud byl v kapse, odhlásíme ji.
            if (draggedItem.currentPocketIndex != 0 && PlayerEquipment.Instance != null)
            {
                PlayerEquipment.Instance.UnequipItem(draggedItem.currentPocketIndex);
                draggedItem.currentPocketIndex = 0;
            }

            draggedItem.transform.SetParent(transform, true);
        }
    }
}