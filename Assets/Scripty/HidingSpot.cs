using System.Collections;
using UnityEngine;

public class HidingSpot : MonoBehaviour
{
    [Header("Data")]
    public PlayerStats playerStats; 

    [Header("Komponenty a Pozice")]
    public Animator hidingSpotAnimator; 
    public Transform cameraSocket;      
    public Transform exitPosition;     

    [Header("Kamera a CamHolder")]
    [Tooltip("Sem přetáhni v Inspectoru PŘESNĚ TU KAMERU, kterou používáš pro pohled hráče.")]
    public Camera targetPlayerCamera; 

    [Tooltip("Sem přetáhni objekt CamHolder z hráče.")]
    public Transform defaultCamHolder; 

    [Header("Názvy Animací")]
    public string enterAnimationTrigger = "Enter";
    public string exitAnimationTrigger = "Exit";

    [Header("Časování (v sekundách)")]
    public float enterAnimationDuration = 1.0f;
    public float exitAnimationDuration = 1.0f;

    private bool isTransitioning = false; 
    private bool isFullyHidden = false;   

    private PlayerRigidbodyMovement playerMovementScript;
    private GameObject playerObject;
    private Rigidbody playerRb;

    private void Awake()
    {
        if (playerStats != null)
        {
            playerStats.isHiding = false;
        }
    }

    public void Interact(GameObject player)
    {
        if (isTransitioning) return;

        playerObject = player;

        playerMovementScript = player.GetComponent<PlayerRigidbodyMovement>();
        if (playerMovementScript == null) 
            playerMovementScript = player.GetComponentInChildren<PlayerRigidbodyMovement>();

        playerRb = player.GetComponent<Rigidbody>();

        if (!isFullyHidden)
        {
            StartCoroutine(HideRoutine());
        }
        else
        {
            StartCoroutine(ShowRoutine());
        }
    }

    private IEnumerator HideRoutine()
    {
        isTransitioning = true;

        if (playerStats != null) 
        {
            playerStats.isHiding = true;
        }

        // 1. Deaktivujeme pohyb a fyziku
        if (playerMovementScript != null) playerMovementScript.enabled = false;
        
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.isKinematic = true;
        }

        CharacterController cc = playerObject != null ? playerObject.GetComponent<CharacterController>() : null;
        if (cc != null) cc.enabled = false;

        // 2. Připojíme kameru do socketu skrýše
        if (targetPlayerCamera != null && cameraSocket != null)
        {
            targetPlayerCamera.transform.SetParent(cameraSocket);
            targetPlayerCamera.transform.localPosition = Vector3.zero;
            targetPlayerCamera.transform.localRotation = Quaternion.identity;
        }

        // 3. Spustíme animaci vstupu
        if (hidingSpotAnimator != null)
        {
            hidingSpotAnimator.SetTrigger(enterAnimationTrigger);
        }

        yield return new WaitForSeconds(enterAnimationDuration);

        isTransitioning = false;
        isFullyHidden = true;
    }

    private IEnumerator ShowRoutine()
    {
        isTransitioning = true;

        // 1. Spustíme animaci výstupu
        if (hidingSpotAnimator != null)
        {
            hidingSpotAnimator.SetTrigger(exitAnimationTrigger);
        }

        yield return new WaitForSeconds(exitAnimationDuration);

        // 2. Nastavíme Hráči Přesnou Pozici a Rotaci z ExitPosition přes skript pohybu
        if (playerObject != null && exitPosition != null)
        {
            playerObject.transform.position = exitPosition.position;

            if (playerRb != null)
            {
                playerRb.position = exitPosition.position;
                playerRb.rotation = exitPosition.rotation;
            }

            // DŮLEŽITÉ: Předáme novou rotaci přímo do skriptu pohybu
            if (playerMovementScript != null)
            {
                playerMovementScript.SetPlayerRotation(exitPosition.rotation);
            }
            else
            {
                playerObject.transform.rotation = exitPosition.rotation;
            }
        }

        // 3. Vrátíme kameru do CamHolderu
        if (targetPlayerCamera != null && defaultCamHolder != null)
        {
            targetPlayerCamera.transform.SetParent(defaultCamHolder);
            targetPlayerCamera.transform.localPosition = Vector3.zero;
            targetPlayerCamera.transform.localRotation = Quaternion.identity;
        }

        // 4. Počkáme 1 fyzikální krok na zklidnění
        yield return new WaitForFixedUpdate();

        // 5. Obnovíme fyziku a ovládání
        if (playerRb != null)
        {
            playerRb.isKinematic = false; 
            playerRb.linearVelocity = Vector3.zero; 
        }

        if (playerMovementScript != null) 
        {
            playerMovementScript.enabled = true;
        }
        
        if (playerObject != null)
        {
            CharacterController cc = playerObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
        }

        isTransitioning = false;
        isFullyHidden = false;

        if (playerStats != null) 
        {
            playerStats.isHiding = false;
        }
    }
}