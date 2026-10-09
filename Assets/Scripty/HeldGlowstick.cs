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
    [Tooltip("Zapnuto = rozsvícení až v momentu animation eventu PrimaryHit (HeldItemAnimationEvents na objektu s Animatorem). Vypnuto = hned po kliknutí.")]
    [SerializeField] private bool lightOnAnimationEvent = true;

    [Header("Pravé tlačítko – hod")]
    [Tooltip("Síla hodu dopředu.")]
    [SerializeField] private float throwForce = 8f;
    [Tooltip("Svislá složka hodu (oblouček).")]
    [SerializeField] private float throwUp = 1f;

    private HeldItemAnimator heldAnimator;
    private HeldItemAnimationEvents animEvents;
    private bool useAnimationEvent; // true = rozsvícení řeší animation event, ne kliknutí
    private Camera cam;

    private void Awake()
    {
        heldAnimator = GetComponent<HeldItemAnimator>();
        animEvents = GetComponentInChildren<HeldItemAnimationEvents>();
        if (glow == null) glow = GetComponentInChildren<GlowstickGlow>();
        cam = ResolveCamera();

        useAnimationEvent = lightOnAnimationEvent && animEvents != null;
        if (lightOnAnimationEvent && animEvents == null)
            Debug.LogWarning($"[HeldGlowstick] '{name}' nemá HeldItemAnimationEvents u Animatoru – svítí se hned po kliknutí.");

        if (heldAnimator != null)
        {
            heldAnimator.onPrimaryUse.AddListener(OnPrimary);
            heldAnimator.onSecondaryUse.AddListener(OnThrow);
        }
        if (useAnimationEvent)
            animEvents.onPrimaryHit.AddListener(TryLightOnSurface);
    }

    private void OnDestroy()
    {
        if (heldAnimator != null)
        {
            heldAnimator.onPrimaryUse.RemoveListener(OnPrimary);
            heldAnimator.onSecondaryUse.RemoveListener(OnThrow);
        }
        if (animEvents != null)
            animEvents.onPrimaryHit.RemoveListener(TryLightOnSurface);
    }

    // Levé kliknutí: bez animation eventu se zkusí rozsvítit hned.
    private void OnPrimary()
    {
        if (!useAnimationEvent) TryLightOnSurface();
    }

    // Když švih zasáhne povrch na dosah → glowstick se rozsvítí naplno.
    // Volá se buď z kliknutí, nebo z animation eventu PrimaryHit (moment zásahu v klipu).
    private void TryLightOnSurface()
    {
        if (glow == null || cam == null) return;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward,
                out _, reach, hitMask, QueryTriggerInteraction.Ignore))
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
