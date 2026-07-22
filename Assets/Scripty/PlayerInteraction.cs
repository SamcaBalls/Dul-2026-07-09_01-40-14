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
        if (interactAction != null && interactAction.action.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        // 1. Pokud je hráč schovaný, zavoláme interakci na uložení skříni
        if (playerStats != null && playerStats.isHiding)
        {
            if (currentHidingSpot != null)
            {
                currentHidingSpot.Interact(gameObject);
            }
            return;
        }

        if (playerCamera == null) return;

        // 2. Pokud schovaný není, hledáme skříň pomocí Raycastu
        RaycastHit hit;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, interactRange))
        {
            HidingSpot hidingSpot = hit.collider.GetComponentInParent<HidingSpot>();

            if (hidingSpot != null)
            {
                currentHidingSpot = hidingSpot; // Uložíme si skříň
                hidingSpot.Interact(gameObject);
            }
        }
    }
}