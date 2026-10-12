using UnityEngine;

// Sedí na světovém prefabu glowsticku (objekt s Rigidbody). Při nárazu rozsvítí
// glowstick podle rychlosti dopadu – tvrdší náraz = jasněji. Každý náraz má i zvuk.
[RequireComponent(typeof(Rigidbody))]
public class GlowstickImpact : MonoBehaviour
{
    [Tooltip("Svítící zařízení glowsticku. Prázdné = najde se v children.")]
    [SerializeField] private GlowstickGlow glow;

    [Header("Mapování nárazu na jas")]
    [Tooltip("Rychlost dopadu (m/s), pod kterou se vůbec nerozsvítí (ať se nerozsvítí kus, co jen leží/dosedá).")]
    [SerializeField] private float activationSpeed = 1f;
    [Tooltip("Rychlost dopadu (m/s), pod kterou svítí minimálně.")]
    [SerializeField] private float minImpactSpeed = 2f;
    [Tooltip("Rychlost dopadu (m/s), nad kterou svítí maximálně.")]
    [SerializeField] private float maxImpactSpeed = 10f;
    [Tooltip("Jas při minimální rychlosti dopadu.")]
    [SerializeField] private float minBrightness = 0.2f;
    [Tooltip("Jas při maximální rychlosti dopadu.")]
    [SerializeField] private float maxBrightness = 1f;

    [Header("Zvuk nárazu")]
    [Tooltip("Zvuk dopadu na zem. Prázdné = bez zvuku.")]
    [SerializeField] private AudioClip impactSound;
    [Tooltip("Rychlost dopadu (m/s), pod kterou se zvuk nepřehraje (ať necvaká při kutálení/dosedání).")]
    [SerializeField] private float soundMinSpeed = 0.5f;
    [Tooltip("Hlasitost při slabém nárazu (minImpactSpeed) a při silném (maxImpactSpeed).")]
    [SerializeField] private Vector2 volumeRange = new Vector2(0.3f, 1f);
    [Tooltip("Rozsah náhodného pitche (min, max).")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.85f, 1.15f);
    [Tooltip("Minimální pauza mezi dvěma zvuky nárazu (s) – při poskakování se nespamuje.")]
    [SerializeField] private float soundCooldown = 0.08f;
    [Tooltip("AudioSource pro zvuk nárazu. Prázdné = najde se na objektu, a když žádný není, vytvoří se (3D).")]
    [SerializeField] private AudioSource audioSource;

    private float lastSoundTime = -999f;

    private void Awake()
    {
        if (glow == null) glow = GetComponentInChildren<GlowstickGlow>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        float speed = collision.relativeVelocity.magnitude;
        float t = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);

        PlayImpactSound(speed, t);

        // Rozsvítí jen poprvé – svítící ani dohořelý glowstick náraz neovlivní.
        if (glow == null || glow.IsGlowing || glow.IsSpent) return;
        if (speed < activationSpeed) return;

        glow.Activate(Mathf.Lerp(minBrightness, maxBrightness, t));
    }

    // Zvuk dopadu s náhodným pitchem; hlasitost podle síly nárazu.
    private void PlayImpactSound(float speed, float t)
    {
        if (impactSound == null || speed < soundMinSpeed) return;
        if (Time.time - lastSoundTime < soundCooldown) return;
        lastSoundTime = Time.time;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D – zvuk je slyšet z místa dopadu
        }

        // Pitch je na AudioSource, ne na PlayOneShot → nastaví se před každým přehráním.
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(impactSound, Mathf.Lerp(volumeRange.x, volumeRange.y, t));
    }
}
