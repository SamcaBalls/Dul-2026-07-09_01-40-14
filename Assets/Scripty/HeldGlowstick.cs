using UnityEngine;

// Sedí na rootu drženého glowsticku (stejný objekt jako HeldItemAnimator).
// Napojí se na jeho eventy: levé = rozsvícení o povrch, pravé = hod.
[RequireComponent(typeof(HeldItemAnimator))]
public class HeldGlowstick : MonoBehaviour
{
    [Tooltip("Svítící zařízení glowsticku. Prázdné = najde se v children.")]
    [SerializeField] private GlowstickGlow glow;

    [Header("Levé tlačítko – rozsvícení o povrch")]
    [Tooltip("Dosah švihu (m).")]
    [SerializeField] private float reach = 3f;
    [Tooltip("Do kterých vrstev se švih trefí (ať netrefí hráče/sebe).")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Pravé tlačítko – hod")]
    [Tooltip("Síla hodu dopředu.")]
    [SerializeField] private float throwForce = 8f;
    [Tooltip("Svislá složka hodu (oblouček).")]
    [SerializeField] private float throwUp = 1f;

    private HeldItemAnimator heldAnimator;
    private Camera cam;

    private void Awake()
    {
        heldAnimator = GetComponent<HeldItemAnimator>();
        if (glow == null) glow = GetComponentInChildren<GlowstickGlow>();
        cam = ResolveCamera();

        if (heldAnimator != null)
        {
            heldAnimator.onPrimaryUse.AddListener(OnPrimary);
            heldAnimator.onSecondaryUse.AddListener(OnThrow);
        }
    }

    private void OnDestroy()
    {
        if (heldAnimator != null)
        {
            heldAnimator.onPrimaryUse.RemoveListener(OnPrimary);
            heldAnimator.onSecondaryUse.RemoveListener(OnThrow);
        }
    }

    // Levé: když švih zasáhne povrch na dosah → glowstick se rozsvítí naplno.
    private void OnPrimary()
    {
        if (glow == null || cam == null) return;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward,
                out RaycastHit hit, reach, hitMask, QueryTriggerInteraction.Ignore))
        {
            glow.Activate(1f);
        }
    }

    // Pravé: glowstick se hodí; stav svícení se přenese na odhozený kus.
    private void OnThrow()
    {
        bool wasGlowing = glow != null && glow.IsGlowing;
        bool wasSpent = glow != null && glow.IsSpent;
        float brightness = glow != null ? glow.Brightness01 : 0f;
        float life = glow != null ? glow.Life01 : 0f;

        if (PlayerEquipment.Instance == null) return;

        GameObject thrown = PlayerEquipment.Instance.ThrowActiveHeldItem(throwForce, throwUp);
        if (thrown == null) return;

        GlowstickGlow worldGlow = thrown.GetComponentInChildren<GlowstickGlow>();
        if (worldGlow == null) return;

        if (wasSpent) worldGlow.MarkSpent();
        else if (wasGlowing) worldGlow.ActivateContinued(brightness, life);
        // jinak: necháme GlowstickImpact rozsvítit nárazem
    }

    // Kameru dohledá jako HidingSpot/PlayerInteract → fallback Camera.main.
    private Camera ResolveCamera()
    {
        PlayerInteract interact = FindAnyObjectByType<PlayerInteract>();
        if (interact != null && interact.playerCamera != null)
            return interact.playerCamera;
        return Camera.main;
    }
}
