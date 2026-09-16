using UnityEngine;
using UnityEngine.InputSystem;

public class TestInventory : MonoBehaviour
{
    public ItemData item1; // Do Inspectoru přetáhni např. Pistol_Data
    public ItemData item2; // Do Inspectoru přetáhni např. Medkit_Data

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            InventoryManager.Instance.TryAddRandomItemToBackpack(item1);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            InventoryManager.Instance.TryAddRandomItemToBackpack(item2);
    }
}