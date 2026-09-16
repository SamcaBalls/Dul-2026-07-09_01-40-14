using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "ScriptableObjects/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    public bool isHiding;
    
    [Header("Inventory Stats")]
    public float maxCapacity = 50f; // Maximální nosnost hráče
    public float currentTotalWeight = 0f; // Aktuální součet vah všech itemů
}