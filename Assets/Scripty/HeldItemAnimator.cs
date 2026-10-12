using UnityEngine;
using UnityEngine.Events;

// Sedí na modelu drženého itemu (child pod rootem, který hýbe PlayerEquipment).
// Přehrává volitelné animační klipy: vytáhnutí (Draw) a použití (primární/sekundární).
// Item, který daný trigger nemá vyplněný, na akci prostě nereaguje.
public class HeldItemAnimator : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Animator s klipy Draw / UsePrimary / UseSecondary. Prázdné = zkusí se najít na tomto objektu.")]
    [SerializeField] private Animator animator;

    [Header("Triggery (prázdné = item danou akci nemá)")]
    [SerializeField] private string drawTrigger = "Draw";
    [SerializeField] private string primaryTrigger = "UsePrimary";
    [SerializeField] private string secondaryTrigger = "UseSecondary";
    [Tooltip("Trigger animace úspěšného zásahu primárem (např. glowstick se rozsvítil). Spouští ho gameplay skript itemu.")]
    [SerializeField] private string primaryHitSuccessTrigger = "PrimaryHitSuccess";

    [Header("Nastavení")]
    [Tooltip("Minimální rozestup mezi použitími (sekundy), aby nešlo spamovat.")]
    [SerializeField] private float useCooldown = 0.5f;

    [Header("Události (volitelné – navěš herní logiku)")]
    public UnityEvent onPrimaryUse;
    public UnityEvent onSecondaryUse;

    // Čas posledního použití (sdílený pro primary i secondary) pro cooldown.
    private float lastUseTime = -999f;

    // Když komponenta přibude v editoru, předvyplní Animator z tohoto objektu.
    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    // Zavolá PlayerEquipment při vytáhnutí itemu (překrývá se s procedurálním slidem).
    public void PlayDraw()
    {
        if (animator != null && !string.IsNullOrEmpty(drawTrigger))
            animator.SetTrigger(drawTrigger);
    }

    // Animace „zásah prošel" – volá gameplay skript itemu, když primár opravdu něco udělal
    // (např. HeldGlowstick po rozsvícení). Nepodléhá cooldownu, je to navazující animace.
    public void PlayPrimaryHitSuccess()
    {
        if (animator != null && !string.IsNullOrEmpty(primaryHitSuccessTrigger))
            animator.SetTrigger(primaryHitSuccessTrigger);
    }

    // Primární použití (levé tlačítko). Vrací true, když se akce fakt provedla.
    public bool TryPrimaryUse()
    {
        return TryUse(primaryTrigger, onPrimaryUse);
    }

    // Sekundární použití (pravé tlačítko). Vrací true, když se akce fakt provedla.
    public bool TrySecondaryUse()
    {
        return TryUse(secondaryTrigger, onSecondaryUse);
    }

    // Společná logika: prázdný trigger = akci nemá; jinak respektuje cooldown,
    // nastaví trigger (má-li animator) a vyvolá volitelný gameplay event.
    private bool TryUse(string trigger, UnityEvent evt)
    {
        if (string.IsNullOrEmpty(trigger)) return false;
        if (Time.time - lastUseTime < useCooldown) return false;

        lastUseTime = Time.time;

        if (animator != null)
            animator.SetTrigger(trigger);

        evt?.Invoke();
        return true;
    }
}
