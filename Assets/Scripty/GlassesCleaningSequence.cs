using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GlassesCleaningSequence : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private PlayerStats playerStats;


    [Header("Volume Config")]
    [SerializeField] private Volume depthVolume;   // Sem v Inspectoru přetáhneš DepthVolume
    [SerializeField] private Volume obecnyVolume; // Sem v Inspectoru přetáhneš ObecnyVolume

    [Header("Nastavení Animace")]
    [SerializeField] private string animationTriggerName = "UtriBryle";
    [SerializeField] private float animationDuration = 3.0f; // Jak dlouho trvá utírání

    [Header("Nastavení Mrknutí")]
    [SerializeField] private float blinkSpeed = 0.15f; // Jak rychle oko mrkne

    private DepthOfField depthOfField;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    public bool isRunning = false;

    private void Start()
    {
        // 1. Vytáhneme Depth of Field z prvního Volume
        if (depthVolume != null)
        {
            depthVolume.profile.TryGet(out depthOfField);
        }
        else
        {
            Debug.LogError("Není přiřazen DepthVolume!");
        }

        // 2. Vytáhneme Vignette a Color Adjustments z druhého Volume
        if (obecnyVolume != null)
        {
            obecnyVolume.profile.TryGet(out vignette);
            obecnyVolume.profile.TryGet(out colorAdjustments);
        }
        else
        {
            Debug.LogError("Není přiřazen ObecnyVolume!");
        }

        // Kontrola, zda máme vše potřebné pro bezchybný běh
        if (depthOfField == null || vignette == null || colorAdjustments == null)
        {
            Debug.LogError("Chyby v nastavení Volume! Zkontroluj, zda na DepthVolume je DoF a na ObecnyVolume jsou Vignette a Color Adjustments.");
        }
    }

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!isRunning && depthOfField != null && vignette != null && colorAdjustments != null && !playerStats.isHiding)
        {
            StartCoroutine(CleaningSequence());
        }
    }

    private IEnumerator CleaningSequence()
    {
        isRunning = true;

        // --- 1. MRKNUTÍ (Zavření oka do absolutní tmy přes ObecnyVolume) ---
        yield return StartCoroutine(AnimateBlink(0f, 1f, 0f, -10f, blinkSpeed));

        // Změna rozmazání v DepthVolume + spuštění animace postavy (vše je skryté ve tmě)
        depthOfField.focalLength.Override(300f);
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger(animationTriggerName);
        }

        // --- Otevření oka ---
        yield return StartCoroutine(AnimateBlink(1f, 0f, -10f, 0f, blinkSpeed));

        // --- ČEKÁNÍ NA UTŘENÍ BRÝLÍ ---
        yield return new WaitForSeconds(animationDuration - 0.1f);

        // --- 2. MRKNUTÍ (Zavření oka před vrácením ostrosti) ---
        yield return StartCoroutine(AnimateBlink(0f, 1f, 0f, -10f, blinkSpeed));

        // Vrácení ostrosti v DepthVolume
        depthOfField.focalLength.Override(1f);

        // --- Otevření oka ---
        yield return StartCoroutine(AnimateBlink(1f, 0f, -10f, 0f, blinkSpeed));
        
        isRunning = false;
    }

    // Pomocná coroutina animující efekty na ObecnyVolume
    private IEnumerator AnimateBlink(float startVignette, float endVignette, float startExposure, float endExposure, float duration)
    {
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            float currentVignette = Mathf.Lerp(startVignette, endVignette, t);
            float currentExposure = Mathf.Lerp(startExposure, endExposure, t);

            vignette.intensity.Override(currentVignette);
            colorAdjustments.postExposure.Override(currentExposure);

            yield return null;
        }

        vignette.intensity.Override(endVignette);
        colorAdjustments.postExposure.Override(endExposure);
    }
}