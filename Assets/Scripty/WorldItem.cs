using UnityEngine;

// Item ležící ve světě (na zemi). Sedí na fyzickém objektu s colliderem.
// Hráč ho zaměří pohledem a sebere klávesou F (viz PlayerInteract),
// nebo ho sem vyhodí InventoryManager při přetáhnutí ven z batohu.
public class WorldItem : MonoBehaviour
{
    [Tooltip("Jaký item tenhle objekt na zemi představuje.")]
    public ItemData itemData;

    // Zkusí item sebrat do batohu. Vrací true, když se to povedlo (batoh nebyl plný).
    // Při úspěchu se objekt ze světa zničí.
    public bool TryPickUp()
    {
        if (itemData == null)
        {
            Debug.LogWarning($"[WorldItem] '{name}' nemá nastavený itemData – nelze sebrat.");
            return false;
        }

        if (InventoryManager.Instance == null) return false;

        bool added = InventoryManager.Instance.TryAddRandomItemToBackpack(itemData);
        if (added) Destroy(gameObject);
        return added;
    }
}
