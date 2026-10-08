using UnityEngine;

// "Svítící zařízení" glowsticku – jediný vlastník Lightu, emise a plynulého vyhasínání.
// Sedí na drženém i světovém prefabu. Nezná input ani házení.
public class GlowstickGlow : MonoBehaviour
{
    [Header("Light")]
    [Tooltip("Point light glowsticku. Prázdné = najde se v children, a když žádný není, vytvoří se.")]
    [SerializeField] private Light glowLight;
    [Tooltip("Barva světla i emise. Default = zelená svítivka.")]
    [SerializeField] private Color glowColor = new Color(0.3f, 1f, 0.3f);
    [Tooltip("Intenzita Lightu při plném jasu (brightness01 = 1).")]
    [SerializeField] private float maxIntensity = 4f;
    [Tooltip("Dosah světla (v metrech).")]
    [SerializeField] private float lightRange = 8f;

    [Header("Vyhasínání")]
    [Tooltip("Za jak dlouho (s) od rozsvícení dohasne na nulu.")]
    [SerializeField] private float fadeDuration = 60f;
    [Tooltip("Tvar dohasínání (vstup = zbývající život 1→0, výstup = násobič jasu).")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(1f, 1f, 0f, 0f);

    [Header("Emise těla (volitelné)")]
    [Tooltip("Násobič emisní barvy pro MaterialPropertyBlock. Vidět jen když má materiál zapnutou Emission.")]
    [SerializeField] private float emissiveIntensity = 3f;
    [Tooltip("Renderery, kterým se nastavuje emise. Prázdné = všechny v children.")]
    [SerializeField] private Renderer[] glowRenderers;

    private bool isGlowing;
    private bool isSpent;
    private float life01;        // 1 = čerstvý, 0 = dohořelý
    private float brightness01;  // 0–1

    private MaterialPropertyBlock mpb;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool IsGlowing => isGlowing;
    public bool IsSpent => isSpent;
    public float Brightness01 => brightness01;
    public float Life01 => life01;

    // Předvyplnění referencí při přidání komponenty v editoru.
    private void Reset()
    {
        glowLight = GetComponentInChildren<Light>();
        glowRenderers = GetComponentsInChildren<Renderer>();
    }

    private void Awake()
    {
        if (glowLight == null) glowLight = GetComponentInChildren<Light>();
        if (glowLight == null) glowLight = gameObject.AddComponent<Light>();

        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = lightRange;
        glowLight.intensity = 0f;
        glowLight.enabled = false;

        if (glowRenderers == null || glowRenderers.Length == 0)
            glowRenderers = GetComponentsInChildren<Renderer>();

        mpb = new MaterialPropertyBlock();
        ApplyEmission(0f);
    }

    // Rozsvítí na daný jas s čerstvým životem. Dohořelý glowstick se nerozsvítí.
    public void Activate(float brightness)
    {
        if (isSpent) return;
        isGlowing = true;
        life01 = 1f;
        brightness01 = Mathf.Clamp01(brightness);
        glowLight.enabled = true;
    }

    // Navázání při hodu už svítícího kusu – pokračuje se zbytkovým životem.
    public void ActivateContinued(float brightness, float remainingLife01)
    {
        if (isSpent) return;
        brightness01 = Mathf.Clamp01(brightness);
        life01 = Mathf.Clamp01(remainingLife01);
        if (life01 <= 0f)
        {
            isGlowing = false;
            isSpent = true;
            glowLight.enabled = false;
            ApplyEmission(0f);
        }
        else
        {
            isGlowing = true;
            glowLight.enabled = true;
        }
    }

    // Označí jako dohořelý – už nikdy nesvítí (hod dohořelého kusu).
    public void MarkSpent()
    {
        isGlowing = false;
        isSpent = true;
        if (glowLight != null) glowLight.enabled = false;
        ApplyEmission(0f);
    }

    private void Update()
    {
        if (!isGlowing) return;

        life01 -= (fadeDuration > 0f) ? Time.deltaTime / fadeDuration : 1f;
        if (life01 <= 0f)
        {
            life01 = 0f;
            isGlowing = false;
            isSpent = true;
            glowLight.enabled = false;
            ApplyEmission(0f);
            return;
        }

        float k = fadeCurve.Evaluate(life01) * brightness01;
        glowLight.intensity = maxIntensity * k;
        ApplyEmission(k);
    }

    // Nastaví emisní barvu přes MaterialPropertyBlock všem rendererům (neovlivní sdílený materiál).
    private void ApplyEmission(float k)
    {
        if (glowRenderers == null || mpb == null) return;
        Color c = glowColor * (emissiveIntensity * k);
        foreach (Renderer r in glowRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
