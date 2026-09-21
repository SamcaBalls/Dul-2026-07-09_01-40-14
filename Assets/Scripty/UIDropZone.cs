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
            // Item, který v kapse právě je (pokud tam nějaký je) – kandidát na výměnu (swap).
            // Tažený item je během tahání parentovaný na Canvas, takže tady mezi děti nepatří.
            UIInventoryItem existingItem = null;
            foreach (Transform child in transform)
            {
                UIInventoryItem candidate = child.GetComponent<UIInventoryItem>();
                if (candidate != null && candidate != draggedItem)
                {
                    existingItem = candidate;
                    break;
                }
            }

            // Kam patřil tažený item, než ho hráč zvedl – sem půjde vyměněný item.
            Transform swapParent = draggedItem.OriginalParent;
            Vector2 swapPosition = draggedItem.OriginalPosition;
            Quaternion swapRotation = draggedItem.OriginalRotation;

            // 1) Tažený item odhlásíme z jeho staré kapsy (pokud v nějaké byl)
            //    a zaregistrujeme do téhle kapsy.
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

            // 2) Pokud v kapse někdo byl, přesuneme ho tam, odkud přišel tažený item (VÝMĚNA).
            if (existingItem != null)
            {
                existingItem.transform.SetParent(swapParent, true);
                RectTransform existingRect = existingItem.GetComponent<RectTransform>();
                existingRect.anchoredPosition = swapPosition;
                existingRect.localRotation = swapRotation;

                // Přišel tažený item z jiné kapsy? Pak tam vyměněný item zaregistrujeme.
                UIDropZone swapZone = swapParent != null ? swapParent.GetComponent<UIDropZone>() : null;
                if (swapZone != null && swapZone.zoneType == ZoneType.Pocket)
                {
                    existingItem.currentPocketIndex = swapZone.pocketIndex;
                    if (PlayerEquipment.Instance != null)
                        PlayerEquipment.Instance.EquipItem(swapZone.pocketIndex, existingItem.itemData);
                }
                else
                {
                    // Vyměněný item se vrací do batohu.
                    existingItem.currentPocketIndex = 0;
                }
            }
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