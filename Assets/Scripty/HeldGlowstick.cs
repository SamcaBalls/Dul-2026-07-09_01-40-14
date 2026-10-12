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
    [Tooltip("Zvuk rozsvícení v ruce (prasknutí svítivky). Prázdné = bez zvuku.")]
    [SerializeField] private AudioClip lightSound;
    [Tooltip("Hlasitost zvuku rozsvícení.")]
    [SerializeField, Range(0f, 1f)] private float lightSoundVolume = 1f;
    [Tooltip("AudioSource pro zvuk rozsvícení. Prázdné = najde se na objektu, a když žádný není, vytvoří se.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Pravé tlačítko – hod")]
    [Tooltip("Síla hodu dopředu.")]
    [SerializeField] private float throwForce = 8f;
    [Tooltip("Svislá složka hodu (oblouček).")]
    [SerializeField] private float throwUp = 1f;
    [Tooltip("Zapnuto = glowstick opustí ruku až v momentu animation eventu SecondaryHit. Vypnuto = hned po kliknutí.")]
    [SerializeField] private bool throwOnAnimationEvent = true;
    [Tooltip("Odkud glowstick vyletí (pozice + natočení). Prázdné = animovaný model v ruce (objekt s Animatorem).")]
    [SerializeField] private Transform throwSpawnPoint;

    private HeldItemAnimator heldAnimator;
    private HeldItemAnimationEvents animEvents;
    private bool lightOnEvent; // true = rozsvícení řeší animation event PrimaryHit, ne kliknutí
    private bool throwOnEvent; // true = hod řeší animation event SecondaryHit, ne kliknutí
    private Camera cam;

    private void Awake()
    {
        heldAnimator = GetComponent<HeldItemAnimator>();
        animEvents = GetComponentInChildren<HeldItemAnimationEvents>();
        if (glow == null) glow = GetComponentInChildren<GlowstickGlow>();
        cam = ResolveCamera();

        if (throwSpawnPoint == null)
        {
            Animator modelAnimator = GetComponentInChildren<Animator>();
            throwSpawnPoint = modelAnimator != null ? modelAnimator.transform : transform;
        }

        lightOnEvent = lightOnAnimationEvent && animEvents != null;
        throwOnEvent = throwOnAnimationEvent && animEvents != null;
        if ((lightOnAnimationEvent || throwOnAnimationEvent) && animEvents == null)
            Debug.LogWarning($"[HeldGlowstick] '{name}' nemá HeldItemAnimationEvents u Animatoru – svícení i hod proběhnou hned po kliknutí.");

        if (heldAnimator != null)
        {
            heldAnimator.onPrimaryUse.AddListener(OnPrimary);
            heldAnimator.onSecondaryUse.AddListener(OnSecondary);
        }
        if (lightOnEvent)
            animEvents.onPrimaryHit.AddListener(TryLightOnSurface);
        if (throwOnEvent)
            animEvents.onSecondaryHit.AddListener(Throw);
    }

    private void OnDestroy()
    {
        if (heldAnimator != null)
        {
            heldAnimator.onPrimaryUse.RemoveListener(OnPrimary);
            heldAnimator.onSecondaryUse.RemoveListener(OnSecondary);
        }
        if (animEvents != null)
        {
            animEvents.onPrimaryHit.RemoveListener(TryLightOnSurface);
            animEvents.onSecondaryHit.RemoveListener(Throw);
        }
    }

    // Levé kliknutí: bez animation eventu se zkusí rozsvítit hned.
    private void OnPrimary()
    {
        if (!lightOnEvent) TryLightOnSurface();
    }

    // Pravé kliknutí: bez animation eventu se hodí hned.
    private void OnSecondary()
    {
        if (!throwOnEvent) Throw();
    }

    // Když švih zasáhne povrch na dosah → glowstick se rozsvítí naplno.
    // Volá se buď z kliknutí, nebo z animation eventu PrimaryHit (moment zásahu v klipu).
    private void TryLightOnSurface()
    {
        if (glow == null || cam == null) return;

        if (Physics.Raycast(cam.transform.position, cam.transform.forward,
                out _, reach, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Dohořelý kus se už nerozsvítí → ani nezazní.
            if (glow.IsSpent) return;
            glow.Activate(1f);
            PlayLightSound();
            if (heldAnimator != null) heldAnimator.PlayPrimaryHitSuccess();
        }
    }

    private void PlayLightSound()
    {
        if (lightSound == null) return;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            // Zvuk v ruce = u ucha hráče, takže 2D (bez prostorového tlumení).
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        audioSource.PlayOneShot(lightSound, lightSoundVolume);
    }

    // Hod: glowstick opustí ruku; stav svícení se přenese na odhozený kus.
    // Volá se buď z kliknutí, nebo z animation eventu SecondaryHit (moment puštění v klipu).
    private void Throw()
    {
        bool wasGlowing = glow != null && glow.IsGlowing;
        bool wasSpent = glow != null && glow.IsSpent;
        float brightness = glow != null ? glow.Brightness01 : 0f;
        float life = glow != null ? glow.Life01 : 0f;

        if (PlayerEquipment.Instance == null) return;

        GameObject thrown = PlayerEquipment.Instance.ThrowActiveHeldItem(throwForce, throwUp, throwSpawnPoint);
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
