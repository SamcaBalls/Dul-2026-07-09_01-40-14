using UnityEngine;
using UnityEngine.Events;

// Univerzální zničitelný objekt (dveře, bedna, kámen…). Sekyra švihne a v momentě
// dopadu zavolá TakeHit. Po každém úderu se přepne model stavu poškození,
// v místě úderu se spawne particle a po posledním úderu objekt zmizí
// (volitelně se místo něj spawnou fyzikální úlomky – DestroyableDebris).
public class Destroyable : MonoBehaviour, IDestroyable
{
    public enum DestroyMode { DestroyObject, Deactivate, EventOnly }

    [Header("Výdrž")]
    [Tooltip("Kolik úderů (damage) objekt vydrží.")]
    [SerializeField] private int hitPoints = 3;

    [Header("Stavy poškození (volitelné)")]
    [Tooltip("Modely stavů jako child objekty: [0] = celé, [1] = po 1. úderu, [2] = po 2. úderu… Aktivní je vždy jen jeden. Když je úderů víc než stavů, zůstane poslední. Prázdné = model se nemění.")]
    [SerializeField] private GameObject[] damageStages = new GameObject[0];

    [Header("Efekty (volitelné)")]
    [Tooltip("Prefab particle efektu, který se spawne v místě každého úderu (natočený po normále povrchu).")]
    [SerializeField] private GameObject hitEffect;
    [Tooltip("Prefab efektu, který se spawne na místě objektu při zničení (prach, třísky…).")]
    [SerializeField] private GameObject destroyEffect;
    [Tooltip("Za kolik sekund se spawnuté efekty smažou.")]
    [SerializeField] private float effectLifetime = 3f;

    [Header("Zničení")]
    [SerializeField] private DestroyMode destroyMode = DestroyMode.DestroyObject;
    [Tooltip("Prefab úlomků (kusy jako child objekty), který se spawne na místě objektu při zničení a rozletí se od místa posledního úderu. Prázdné = bez úlomků.")]
    [SerializeField] private GameObject debrisPrefab;
    [Tooltip("Vynásobit scale úlomků scalem tohoto objektu (když jsou dveře ve scéně zvětšené/zmenšené a prefab úlomků je v měřítku 1).")]
    [SerializeField] private bool debrisMatchScale = true;

    [Header("Události (zvuk, drop…)")]
    public UnityEvent onHit;
    public UnityEvent onDestroyed;

    private int hitsTaken = 0;
    private bool isDestroyed = false;

    private void Awake()
    {
        ShowStage(0);
    }

    public void TakeHit(ToolHit hit)
    {
        if (isDestroyed) return;

        hitPoints -= hit.damage;
        hitsTaken++;

        // Particle v místě úderu – i na posledním úderu.
        SpawnEffect(hitEffect, hit.point,
            hit.normal != Vector3.zero ? Quaternion.LookRotation(hit.normal) : Quaternion.identity);

        if (hitPoints > 0)
        {
            ShowStage(hitsTaken);
            onHit?.Invoke();
            return;
        }

        isDestroyed = true;
        SpawnEffect(destroyEffect, transform.position, transform.rotation);
        SpawnDebris(hit);
        onDestroyed?.Invoke();

        switch (destroyMode)
        {
            case DestroyMode.DestroyObject: Destroy(gameObject); break;
            case DestroyMode.Deactivate: gameObject.SetActive(false); break;
            case DestroyMode.EventOnly: break;
        }
    }

    // Zapne model stavu `index` (ořezaný na poslední) a ostatní vypne.
    private void ShowStage(int index)
    {
        if (damageStages == null || damageStages.Length == 0) return;

        int active = Mathf.Min(index, damageStages.Length - 1);
        for (int i = 0; i < damageStages.Length; i++)
            if (damageStages[i] != null) damageStages[i].SetActive(i == active);
    }

    // Úlomky na místě objektu, rozletí se od bodu úderu ve směru úderu (do povrchu).
    private void SpawnDebris(ToolHit hit)
    {
        if (debrisPrefab == null) return;

        GameObject debris = Instantiate(debrisPrefab, transform.position, transform.rotation);
        if (debrisMatchScale)
            debris.transform.localScale = Vector3.Scale(debris.transform.localScale, transform.lossyScale);

        DestroyableDebris pieces = debris.GetComponent<DestroyableDebris>();
        if (pieces == null) pieces = debris.AddComponent<DestroyableDebris>();

        Vector3 direction = hit.normal != Vector3.zero ? -hit.normal : transform.forward;
        pieces.Explode(hit.point, direction);
    }

    private void SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, position, rotation);
        if (effectLifetime > 0f) Destroy(fx, effectLifetime);
    }
}
