using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "ScriptableObjects/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite itemIcon; // Ikonka do batohu
    public float weight; // Váha do kapacity
    
    [Header("In-Game")]
    public GameObject inGamePrefab; // <-- NOVÉ: Objekt, který se hráči reálně spawne v ruce
}