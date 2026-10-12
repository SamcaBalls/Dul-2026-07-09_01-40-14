using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract : MonoBehaviour
{
    [Header("Interakce")]
    public float interactRange = 3f;
    public Camera playerCamera;

    [Header("Input System")]
    public InputActionReference interactAction;

    [SerializeField] private PlayerStats playerStats;

    private HidingSpot currentHidingSpot; // Uložíme si aktuální skříň přímo sem

    // Na co hráč právě míří – přepočítává se každý snímek (kvůli výpisu názvu itemu).
    private HidingSpot lookedHidingSpot;
    private WorldItem lookedWorldItem;

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.actionMap.Enable();
            interactAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.Disable();
        }
    }

    private void Update()
    {
        // Každý snímek zjistíme, na co hráč míří (kvůli výpisu názvu itemu v HUD).
        UpdateLookTarget();

        if (interactAction != null && interactAction.action.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    // Zaměří skříň / item před hráčem a nahlásí zaměřený item do promptu.
    // Raycast výslovně registruje i triggery (QueryTriggerInteraction.Collide),
    // takže itemu můžeš dát velký trigger collider = větší hitbox na sbírání.
    private void UpdateLookTarget()
    {
        lookedHidingSpot = null;
        lookedWorldItem = null;

        // Ve skrýši po ničem nekoukáme; interakce jde na uloženou skříň.
        bool canScan = playerCamera != null && (playerStats == null || !playerStats.isHiding);

        if (canScan && Physics.Raycast(
                playerCamera.transform.position, playerCamera.transform.forward,
                out RaycastHit hit, interactRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            lookedHidingSpot = hit.collider.GetComponentInParent<HidingSpot>();

            // Skříň má přednost – na ni prompt itemu neukazujeme.
            if (lookedHidingSpot == null)
                lookedWorldItem = hit.collider.GetComponentInParent<WorldItem>();
        }

        // Prompt: název itemu, na který míří, nebo nabídka schování u použitelné skrýše.
        if (ItemPickupPrompt.Instance != null)
        {
            ItemPickupPrompt.Instance.SetTarget(lookedWorldItem);
            // Prompt "Schovat se" jen když se hráč z aktuální pozice fakt může schovat
            // (včetně kontroly povoleného směru), ať tam nesvítí, když by stisk F nic neudělal.
            ItemPickupPrompt.Instance.SetHideAvailable(lookedHidingSpot != null && lookedHidingSpot.CanEnterFrom(transform.position));
        }
    }

    private void TryInteract()
    {
        // 1. Pokud je hráč schovaný, zavoláme interakci na uloženou skříň.
        if (playerStats != null && playerStats.isHiding)
        {
            if (currentHidingSpot != null)
            {
                currentHidingSpot.Interact(gameObject);
            }
            return;
        }

        // 2. Koukáme na skříň? (zjištěno v UpdateLookTarget tento snímek)
        if (lookedHidingSpot != null)
        {
            currentHidingSpot = lookedHidingSpot;
            lookedHidingSpot.Interact(gameObject);
            return;
        }

        // 3. Koukáme na item na zemi? Sebereme ho.
        if (lookedWorldItem != null)
        {
            lookedWorldItem.TryPickUp();
        }
    }
}