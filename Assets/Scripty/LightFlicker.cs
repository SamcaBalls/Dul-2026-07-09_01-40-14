using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class HospitalLightFlicker : MonoBehaviour
{
    [Header("Režim Blikání")]
    [Tooltip("ZAŠKRTNUTO = Jen zbìsilé rychlé blikání bez pauz.\nODŠKRTNUTO = Realistické chování i s delšími výpadky a pauzami.")]
    public bool continuousFlickerOnly = false;

    [Header("Nastavení Svìtla")]
    [Tooltip("Minimální intenzita pøi výpadku/poklesu.")]
    public float minIntensity = 0.05f;

    [Tooltip("Plná intenzita fungujícího svìtla.")]
    public float maxIntensity = 1.5f;

    [Header("Rychlost Blikání")]
    [Tooltip("Minimální prodleva mezi zmìnami stavu (v sekundách).")]
    public float minFlickerSpeed = 0.01f;

    [Tooltip("Maximální prodleva mezi zmìnami stavu (v sekundách).")]
    public float maxFlickerSpeed = 0.03f;

    [Header("Chování s Pauzami (pouze pokud je Continuous OFF)")]
    [Tooltip("Pravdìpodobnost shluku rychlého blikání vs. pauz (0 až 1).")]
    [Range(0f, 1f)]
    public float burstProbability = 0.5f;

    [Tooltip("Minimální a maximální délka úplného výpadku (ve vteøinách).")]
    public Vector2 pauseDuration = new Vector2(0.2f, 1.5f);

    [Tooltip("Minimální a maximální délka souvislého svícení (ve vteøinách).")]
    public Vector2 stableDuration = new Vector2(0.5f, 3.0f);

    [Header("Vzhled")]
    [Tooltip("Mìní barvu svìtla pøi zhasnutí do chladnìjší/tmavší?")]
    public bool shiftColorOnFlicker = true;

    [Header("Ovládání Materiálu (Emission)")]
    [Tooltip("Sem pøetáhni Objekt (Mesh Renderer), jehož materiál má svítit/zhasínat.")]
    public Renderer targetMeshRenderer;

    [Tooltip("Index materiálu na objektu (0 = první materiál, 1 = druhý materiál, atd.).")]
    public int materialIndex = 0;

    [Tooltip("Jméno barvy v materiálu. Pro URP/HDRP obvykle '_EmissionColor'.")]
    public string emissionColorPropertyName = "_EmissionColor";

    private Light pointLight;
    private Color originalColor;
    private Color dimColor;
    private Material targetMaterial;
    private Color originalEmissionColor;

    private void Awake()
    {
        pointLight = GetComponent<Light>();
        originalColor = pointLight.color;
        dimColor = originalColor * 0.4f;

        if (targetMeshRenderer != null)
        {
            if (materialIndex < targetMeshRenderer.materials.Length)
            {
                targetMaterial = targetMeshRenderer.materials[materialIndex];

                if (targetMaterial.HasProperty(emissionColorPropertyName))
                {
                    originalEmissionColor = targetMaterial.GetColor(emissionColorPropertyName);
                }
            }
            else
            {
                Debug.LogWarning($"Material Index {materialIndex} je mimo rozsah pro objekt {targetMeshRenderer.name}!", this);
            }
        }
    }

    private void OnEnable()
    {
        StartCoroutine(HospitalFlickerRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator HospitalFlickerRoutine()
    {
        while (true)
        {
            // REŽIM 1: Pouze rychlé neustálé blikání bez pauz
            if (continuousFlickerOnly)
            {
                bool isLightOn = Random.value > 0.4f;
                SetLightState(isLightOn);
                yield return new WaitForSeconds(Random.Range(minFlickerSpeed, maxFlickerSpeed));
            }
            // REŽIM 2: Kombinace blikání, výpadkù a souvislého svícení
            else
            {
                float decision = Random.value;

                // A) Shluk rychlého blikání
                if (decision < burstProbability)
                {
                    int flickerCount = Random.Range(4, 12);
                    for (int i = 0; i < flickerCount; i++)
                    {
                        bool isLightOn = Random.value > 0.3f;
                        SetLightState(isLightOn);
                        yield return new WaitForSeconds(Random.Range(minFlickerSpeed, maxFlickerSpeed));
                    }
                }
                // B) Úplný výpadek (Tma)
                else if (decision < burstProbability + 0.3f)
                {
                    SetLightState(false);
                    yield return new WaitForSeconds(Random.Range(pauseDuration.x, pauseDuration.y));
                }
                // C) Stabilní svícení
                else
                {
                    SetLightState(true);
                    yield return new WaitForSeconds(Random.Range(stableDuration.x, stableDuration.y));
                }
            }
        }
    }

    private void SetLightState(bool isOn)
    {
        if (isOn)
        {
            pointLight.intensity = Random.Range(maxIntensity * 0.8f, maxIntensity);
            if (shiftColorOnFlicker) pointLight.color = originalColor;

            SetEmissionState(true);
        }
        else
        {
            pointLight.intensity = Random.Range(0f, minIntensity);
            if (shiftColorOnFlicker) pointLight.color = dimColor;

            SetEmissionState(false);
        }
    }

    private void SetEmissionState(bool isOn)
    {
        if (targetMaterial == null) return;

        if (isOn)
        {
            targetMaterial.EnableKeyword("_EMISSION");
            if (targetMaterial.HasProperty(emissionColorPropertyName))
            {
                targetMaterial.SetColor(emissionColorPropertyName, originalEmissionColor);
            }
        }
        else
        {
            targetMaterial.DisableKeyword("_EMISSION");
            if (targetMaterial.HasProperty(emissionColorPropertyName))
            {
                targetMaterial.SetColor(emissionColorPropertyName, Color.black);
            }
        }
    }
}