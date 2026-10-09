using UnityEngine;

// Sedí na světovém prefabu glowsticku (objekt s Rigidbody). Při nárazu rozsvítí
// glowstick podle rychlosti dopadu – tvrdší náraz = jasněji.
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

    private void Awake()
    {
        if (glow == null) glow = GetComponentInChildren<GlowstickGlow>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Rozsvítí jen poprvé – svítící ani dohořelý glowstick náraz neovlivní.
        if (glow == null || glow.IsGlowing || glow.IsSpent) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < activationSpeed) return;

        float t = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
        glow.Activate(Mathf.Lerp(minBrightness, maxBrightness, t));
    }
}
