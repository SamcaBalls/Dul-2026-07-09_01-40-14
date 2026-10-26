using UnityEngine;

// Sedí na rootu drženého nástroje (stejný objekt jako HeldItemAnimator), např. SekyraDoRuky.
// Levé tlačítko: švih + raycast z kamery. Zasažený IDestroyable dostane damage v momentě
// dopadu (animation event PrimaryHit přes HeldItemAnimationEvents na objektu s Animatorem).
[RequireComponent(typeof(HeldItemAnimator))]
public class MeleeToolDamage : MonoBehaviour, IPrimaryUse
{
    [Tooltip("Dosah úderu (m).")]
    [SerializeField] private float range = 3f;
    [Tooltip("Kolik HP ubere jeden úder.")]
    [SerializeField] private int damage = 1;
    [Tooltip("Do kterých vrstev se úder trefí (ať netrefí hráče/sebe).")]
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("Ignorovat trigger collidery (ať úder netrefí hitboxy pro sebrání itemů).")]
    [SerializeField] private bool ignoreTriggers = true;
    [Tooltip("Zapnuto = damage až v momentu animation eventu PrimaryHit (HeldItemAnimationEvents na objektu s Animatorem). Vypnuto / bez eventu = hned po kliknutí.")]
    [SerializeField] private bool hitOnAnimationEvent = true;

    private HeldItemAnimator heldAnimator;
    private HeldItemAnimationEvents animEvents;
    private bool hitOnEvent;
    private Camera cam;

    private void Awake()
    {
        heldAnimator = GetComponent<HeldItemAnimator>();
        animEvents = GetComponentInChildren<HeldItemAnimationEvents>();

        PlayerInteract interact = FindAnyObjectByType<PlayerInteract>();
        cam = interact != null && interact.playerCamera != null ? interact.playerCamera : Camera.main;

        hitOnEvent = hitOnAnimationEvent && animEvents != null;
        if (hitOnAnimationEvent && animEvents == null)
            Debug.LogWarning($"[MeleeToolDamage] '{name}' nemá HeldItemAnimationEvents u Animatoru – damage proběhne hned po kliknutí.");

        if (hitOnEvent)
            animEvents.onPrimaryHit.AddListener(Hit);
    }

    private void OnDestroy()
    {
        if (animEvents != null)
            animEvents.onPrimaryHit.RemoveListener(Hit);
    }

    // Volá PlayerEquipment při stisku levého tlačítka. Švih respektuje cooldown.
    public void OnPrimaryUse()
    {
        if (heldAnimator == null || !heldAnimator.TryPrimaryUse()) return;
        if (!hitOnEvent) Hit();
    }

    // Moment dopadu švihu – raycast až teď, ať trefí to, na co hráč míří při dopadu.
    public void Hit()
    {
        if (cam == null) return;

        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit rayHit,
                range, hitMask, ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide))
            return;

        IDestroyable target = rayHit.collider.GetComponentInParent<IDestroyable>();
        if (target == null) return;

        target.TakeHit(new ToolHit
        {
            point = rayHit.point,
            normal = rayHit.normal,
            damage = damage
        });
    }
}
