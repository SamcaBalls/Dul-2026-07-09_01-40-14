using System;
using System.Collections;
using UnityEngine;

public class HidingSpot : MonoBehaviour
{
    // Směry v lokálním nebo globálním prostoru skrýše
    public enum Direction { North, East, South, West }

    [System.Serializable]
    public class DirectionData
    {
        public Direction direction;
        public bool isEnabled = true;
        public string enterAnimationTrigger = "North";
        public string exitAnimationTrigger = "Exit";
    }

    [Header("Data")]
    public PlayerStats playerStats; 

    [Header("Směry a Animace")]
    [Tooltip("Nastav povolené směry a příslušné názvy animací.")]
    public DirectionData[] directionSettings = new DirectionData[4]
    {
        new DirectionData { direction = Direction.North, enterAnimationTrigger = "North", exitAnimationTrigger = "Exit" },
        new DirectionData { direction = Direction.East, enterAnimationTrigger = "East", exitAnimationTrigger = "Exit" },
        new DirectionData { direction = Direction.South, enterAnimationTrigger = "South", exitAnimationTrigger = "Exit" },
        new DirectionData { direction = Direction.West, enterAnimationTrigger = "West", exitAnimationTrigger = "Exit" }
    };

    [Header("Komponenty a Pozice")]
    public Animator hidingSpotAnimator; 
    public Transform cameraSocket;      
    public Transform exitPosition; 
    [SerializeField] private GlassesCleaningSequence bryle;    

    [Header("Kamera a CamHolder")]
    [Tooltip("Sem přetáhni v Inspectoru PŘESNĚ TU KAMERU, kterou používáš pro pohled hráče.")]
    public Camera targetPlayerCamera; 

    [Tooltip("Sem přetáhni objekt CamHolder z hráče.")]
    public Transform defaultCamHolder; 

    [Header("Časování (v sekundách)")]
    public float enterAnimationDuration = 1.0f;
    public float exitAnimationDuration = 1.0f;
    [Tooltip("Jak dlouho hlava (kamera) plynule srovnává (lerpuje) na správnou rotaci po skončení výstupní animace.")]
    public float headExitLerpDuration = 0.4f;

    private bool isTransitioning = false; 
    private bool isFullyHidden = false;   

    private PlayerRigidbodyMovement playerMovementScript;
    private GameObject playerObject;
    private Rigidbody playerRb;

    // Uložený směr, ze kterého hráč do skrýše vstoupil
    private DirectionData currentActiveDirection;

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
        if (bryle != null && bryle.isRunning) return;

        playerObject = player;

        playerMovementScript = player.GetComponent<PlayerRigidbodyMovement>();
        if (playerMovementScript == null) 
            playerMovementScript = player.GetComponentInChildren<PlayerRigidbodyMovement>();

        playerRb = player.GetComponent<Rigidbody>();

        if (!isFullyHidden)
        {
            // Vypočítáme směr příchodu hráče
            Direction detectedDirection = CalculateInteractionDirection(player.transform.position);
            DirectionData data = GetDirectionData(detectedDirection);

            // Pokud je daný směr zakázaný nebo nenastavený, interakci neprovedeme
            if (data == null || !data.isEnabled)
            {
                Debug.LogWarning($"Interakce ze směru {detectedDirection} není povolená!");
                return;
            }

            currentActiveDirection = data;
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

        // Automaticky schováme předmět z ruky (sjede dolů).
        if (PlayerEquipment.Instance != null)
            PlayerEquipment.Instance.SetHidden(true);

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

        // 3. Spustíme animaci vstupu pro detekovaný směr
        if (hidingSpotAnimator != null && currentActiveDirection != null)
        {
            hidingSpotAnimator.SetTrigger(currentActiveDirection.enterAnimationTrigger);
        }

        yield return new WaitForSeconds(enterAnimationDuration);

        isTransitioning = false;
        isFullyHidden = true;
    }

    private IEnumerator ShowRoutine()
    {
        isTransitioning = true;

        // 1. Spustíme animaci výstupu pro směr, ze kterého hráč vešel
        if (hidingSpotAnimator != null && currentActiveDirection != null)
        {
            hidingSpotAnimator.SetTrigger(currentActiveDirection.exitAnimationTrigger);
        }

        yield return new WaitForSeconds(exitAnimationDuration);

        // 2. Nastavíme Hráči Přesnou Pozici a Rotaci z ExitPosition přes skript pohybu
        if (playerObject != null && exitPosition != null)
        {
            playerObject.transform.position = exitPosition.position;

            if (playerRb != null)
            {
                playerRb.position = exitPosition.position;
                playerRb.rotation = Quaternion.Euler(0f, exitPosition.eulerAngles.y, 0f);
            }

            if (playerMovementScript != null)
            {
                playerMovementScript.SetPlayerRotation(Quaternion.Euler(0f, exitPosition.eulerAngles.y, 0f));
            }
            else
            {
                playerObject.transform.rotation = Quaternion.Euler(0f, exitPosition.eulerAngles.y, 0f);
            }
        }

        // 3. Vrátíme kameru do CamHolderu a plynule (lerp) srovnáme hlavu na správnou rotaci
        if (targetPlayerCamera != null && defaultCamHolder != null)
        {
            // Připojíme kameru zpět, ale ZACHOVÁME její aktuální world pozici/rotaci z konce animace,
            // aby nedošlo k okamžitému "cuknutí" pohledu.
            targetPlayerCamera.transform.SetParent(defaultCamHolder, true);

            Vector3 startLocalPos = targetPlayerCamera.transform.localPosition;
            Quaternion startLocalRot = targetPlayerCamera.transform.localRotation;

            // Cílem je lokální identita = hlava kouká rovně dopředu ve směru těla na ExitPosition.
            float elapsed = 0f;
            while (elapsed < headExitLerpDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / headExitLerpDuration);

                targetPlayerCamera.transform.localPosition = Vector3.Lerp(startLocalPos, Vector3.zero, t);
                targetPlayerCamera.transform.localRotation = Quaternion.Slerp(startLocalRot, Quaternion.identity, t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Přesné dorovnání na cílovou lokální pozici/rotaci
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

        // Po vylezení ze skrýše zase vytáhneme předmět z aktivní kapsy (vyjede nahoru).
        if (PlayerEquipment.Instance != null)
            PlayerEquipment.Instance.SetHidden(false);
    }

    /// <summary>
    /// Vypočítá směr hráče vůči lokální rotaci skrýše.
    /// Z/North = Dopředu (+Z), East = Doprava (+X), South = Dozadu (-Z), West = Doleva (-X)
    /// </summary>
    private Direction CalculateInteractionDirection(Vector3 playerPosition)
    {
        Vector3 dirToPlayer = (playerPosition - transform.position);
        
        // Převedeme vektor směřující k hráči do lokálního prostoru skrýše
        Vector3 localDir = transform.InverseTransformDirection(dirToPlayer);
        localDir.y = 0; // Ignorujeme výškový rozdíl

        float angle = Vector3.SignedAngle(Vector3.forward, localDir, Vector3.up);

        // Úhly: North (-45° až 45°), East (45° až 135°), South (135° až -135°), West (-135° až -45°)
        if (angle >= -45f && angle < 45f) return Direction.North;
        if (angle >= 45f && angle < 135f) return Direction.East;
        if (angle >= -135f && angle < -45f) return Direction.West;
        
        return Direction.South;
    }

    private DirectionData GetDirectionData(Direction dir)
    {
        foreach (var data in directionSettings)
        {
            if (data.direction == dir) return data;
        }
        return null;
    }
}