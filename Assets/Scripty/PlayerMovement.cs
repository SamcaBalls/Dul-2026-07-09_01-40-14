using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerRigidbodyMovement : MonoBehaviour
{
    [Header("Nový Input Systém (Reference na akce)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference vaultAction;

    [Header("Pohybové rychlosti (Hororově pomalé)")]
    public float walkSpeed = 2.0f;
    public float sprintSpeed = 3.5f;
    public float crouchSpeed = 1.0f;

    [Header("Nastavení kamery a pohledu")]
    public Transform cameraHolder;
    public Transform mainCamera;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 60.0f;
    [Tooltip("Když je false, kamera se nehýbe (např. při otevřeném inventáři). Pohyb WASD funguje dál.")]
    public bool CanLook = true;

    [Header("Plynulost pohledu (Sliding/Smoothing)")]
    [Tooltip("Jak rychle kamera klouže za myší. Nižší číslo = větší skluz (hladší), vyšší číslo = ostřejší reakce.")]
    public float cameraSmoothing = 15.0f; 

    [Header("Plížení (Crouch)")]
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;
    public float crouchTransitionSpeed = 8.0f;
    [Tooltip("Vzdálenost pro kontrolu stropu nad hlavou hráče při vstávání.")]
    public float ceilingCheckDistance = 1.0f;

    [Header("Houpání hlavy (Headbob)")]
    public float bobFrequency = 4.5f;
    public float bobHorizontalAmplitude = 0.04f;
    public float bobVerticalAmplitude = 0.04f;

    [Header("Přelézání překážek (Vaulting)")]
    public LayerMask obstacleLayer;
    public float vaultCheckDistance = 1.2f;
    public float vaultMaxHeight = 1.5f;
    public float vaultDuration = 0.4f;
    [Tooltip("Kolikrát RYCHLEJŠÍ bude přelézání, pokud hráč u toho sprintuje. Vyšší číslo = rychlejší vault.")]
    public float vaultSprintSpeedMultiplier = 1.5f;
    [Tooltip("Výška od země hráče, ze které se střílí Raycast dopředu.")]
    public float vaultCheckHeightOffset = 0.2f;
    [Tooltip("Jak hluboko za bod nárazu má systém koukat, aby našel zem pro dopad (tloušťka zdi + rezerva).")]
    public float vaultWallThicknessOffset = 0.7f;
    [Tooltip("Úhel, o který se nakloní hlava/kamera během přelézání (ve stupních).")]
    public float vaultMaxLeanAngle = 5f;
    [Tooltip("Jak vysoko (v metrech) se hráč zvedne v oblouku během přelézání. Oblouk má plynulý rozjezd i dosednutí (žádné cuknutí).")]
    public float vaultArcHeight = 0.6f;

    [Header("Kontrola země (Ground Check)")]
    [Tooltip("Rezerva POD chodidly, do které se ještě počítá zem (kvůli schodům/nerovnostem). Skutečná délka paprsku se dopočítá z výšky kapsle, takže platí, i když se kapsle za běhu roztáhne na standingHeight.")]
    public float groundCheckDistance = 0.3f;
    public LayerMask groundLayer;

    // Privátní komponenty a proměnné
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    
    private float rotationX = 0;
    private float defaultCameraY;
    private float defaultCameraHolderY; 
    private float bobTimer = 0;
    
    private bool isCrouching = false;
    private bool isSprinting = false;
    private bool isVaulting = false;
    private bool isGrounded;

    private Quaternion targetBodyRotation;

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (lookAction != null) lookAction.action.Enable();
        if (sprintAction != null) sprintAction.action.Enable();
        if (crouchAction != null) crouchAction.action.Enable();
        
        if (vaultAction != null)
        {
            vaultAction.action.Enable();
            vaultAction.action.started += OnVaultPerformed;
        }
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (lookAction != null) lookAction.action.Disable();
        if (sprintAction != null) sprintAction.action.Disable();
        if (crouchAction != null) crouchAction.action.Disable();
        
        if (vaultAction != null)
        {
            vaultAction.action.started -= OnVaultPerformed;
            vaultAction.action.Disable();
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (mainCamera != null) defaultCameraY = mainCamera.localPosition.y;
        if (cameraHolder != null) defaultCameraHolderY = cameraHolder.localPosition.y;

        targetBodyRotation = transform.rotation;
    }

    void Update()
    {
        HandleMouseLook();
        HandleCrouchLerp(); 
        HandleHeadbob();
    }

    void FixedUpdate()
    {
        if (isVaulting) return;

        CheckGround();
        HandleMovement();
    }

    /// <summary>
    /// Vynutí novou rotaci hráče i vnitřní proměnné pro myš (např. při vylezení ze skrýše)
    /// </summary>
    public void SetPlayerRotation(Quaternion newRotation)
    {
        transform.rotation = newRotation;
        targetBodyRotation = newRotation;

        // Vynulujeme vertikální naklonění kamery nahoru/dolů
        rotationX = 0f;
        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.identity;
        }
    }

    void CheckGround()
    {
        // Chodidla jsou pod pivotem (transform.position) o (půlka výšky kapsle − posun středu).
        // Kapsle se za běhu roztáhne z ~1.66 m na standingHeight (2 m), takže chodidla klesnou
        // ~1.17 m pod pivot. Pevná groundCheckDistance (1.1) na ně přestala dosáhnout → isGrounded
        // bylo pořád false a HandleHeadbob padal do reset větve = žádný headbob vestoje.
        // Paprsek proto natáhneme přesně ke chodidlům + rezerva, ať platí při jakékoli výšce kapsle.
        float pivotToFeet = capsuleCollider.height * 0.5f - capsuleCollider.center.y;
        float rayLength = pivotToFeet + groundCheckDistance;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, rayLength, groundLayer);
    }

    void HandleMouseLook()
    {
        if (!CanLook) return;
        if (lookAction == null) return;

        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();

        rotationX += -lookInput.y * lookSpeed * 0.05f;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        
        Quaternion targetHolderRot = Quaternion.Euler(rotationX, 0, 0);

        if (!isVaulting)
        {
            targetBodyRotation *= Quaternion.Euler(0, lookInput.x * lookSpeed * 0.05f, 0);

            if (cameraHolder != null)
            {
                cameraHolder.localRotation = Quaternion.Slerp(cameraHolder.localRotation, targetHolderRot, Time.deltaTime * cameraSmoothing);
            }
            
            transform.rotation = Quaternion.Slerp(transform.rotation, targetBodyRotation, Time.deltaTime * cameraSmoothing);
        }
    }

    void HandleMovement()
    {
        if (moveAction == null) return;

        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        Vector3 targetVelocity = new Vector3(moveInput.x, 0, moveInput.y);
        targetVelocity = transform.TransformDirection(targetVelocity);

        isSprinting = (sprintAction != null && sprintAction.action.IsPressed()) && !isCrouching;
        float currentSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);

        targetVelocity *= currentSpeed;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 velocityChange = (targetVelocity - currentVelocity);
        
        velocityChange.x = Mathf.Clamp(velocityChange.x, -currentSpeed, currentSpeed);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -currentSpeed, currentSpeed);
        velocityChange.y = 0;

        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    void HandleCrouchLerp()
    {
        if (crouchAction == null) return;

        bool crouchButtonPressed = crouchAction.action.IsPressed();

        if (crouchButtonPressed)
        {
            isCrouching = true;
        }
        else if (isCrouching) 
        {
            Vector3 origin = transform.position + Vector3.up * (capsuleCollider.height * 0.5f);
            Debug.DrawRay(origin, Vector3.up * ceilingCheckDistance, Color.yellow);

            float radius = capsuleCollider.radius * 0.9f;
            if (Physics.SphereCast(origin, radius, Vector3.up, out RaycastHit hit, ceilingCheckDistance, groundLayer | obstacleLayer))
            {
                isCrouching = true; 
            }
            else
            {
                isCrouching = false; 
            }
        }

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float currentHeight = Mathf.Lerp(capsuleCollider.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        capsuleCollider.height = currentHeight;

        if (cameraHolder != null)
        {
            float targetCameraY = isCrouching ? (crouchHeight - 0.2f) : defaultCameraHolderY;
            Vector3 targetCamPos = new Vector3(cameraHolder.localPosition.x, targetCameraY, cameraHolder.localPosition.z);
            cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, targetCamPos, Time.deltaTime * crouchTransitionSpeed);
        }
    }

    void HandleHeadbob()
    {
        if (isVaulting || mainCamera == null) return; 

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        if (!isGrounded || horizontalVelocity.magnitude < 0.1f)
        {
            bobTimer = 0;
            mainCamera.localPosition = new Vector3(
                Mathf.Lerp(mainCamera.localPosition.x, 0, Time.deltaTime * 5f),
                Mathf.Lerp(mainCamera.localPosition.y, defaultCameraY, Time.deltaTime * 5f),
                mainCamera.localPosition.z
            );
            return;
        }

        float speedFactor = horizontalVelocity.magnitude;
        bobTimer += Time.deltaTime * bobFrequency * speedFactor;

        float newX = Mathf.Sin(bobTimer * 0.5f) * bobHorizontalAmplitude;
        float newY = defaultCameraY + Mathf.Sin(bobTimer) * bobVerticalAmplitude;

        mainCamera.localPosition = new Vector3(newX, newY, mainCamera.localPosition.z);
    }

    private void OnVaultPerformed(InputAction.CallbackContext context)
    {
        if (isVaulting || moveAction == null) return;

        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();

        if (moveInput.y <= 0.1f) return; 

        Vector3 origin = transform.position + Vector3.up * vaultCheckHeightOffset;
        
        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, vaultCheckDistance, obstacleLayer))
        {
            Vector3 airOrigin = hit.point + (transform.forward * vaultWallThicknessOffset) + (Vector3.up * vaultMaxHeight);
            
            if (Physics.Raycast(airOrigin, Vector3.down, out RaycastHit groundHit, 10f))
            {
                Vector3 checkBackOrigin = groundHit.point + Vector3.up * vaultCheckHeightOffset;
                Vector3 directionBack = (hit.point - checkBackOrigin).normalized;
                float distanceBack = Vector3.Distance(checkBackOrigin, hit.point);

                if (!Physics.Raycast(checkBackOrigin, directionBack, out RaycastHit backHit, distanceBack, obstacleLayer))
                {
                    Debug.Log("[Vault] Selhalo: Překážka je příliš široká.");
                    return; 
                }

                Vector3 targetPos = new Vector3(groundHit.point.x, transform.position.y, groundHit.point.z);
                
                float calculatedDuration = isSprinting ? (vaultDuration / vaultSprintSpeedMultiplier) : vaultDuration;

                StartCoroutine(PerformVault(targetPos, calculatedDuration));
            }
        }
    }

    IEnumerator PerformVault(Vector3 targetPosition, float customDuration)
    {
        isVaulting = true;

        // Při přelézání schováme předmět z ruky (sjede dolů), ať ho hráč nemá v rukou.
        if (PlayerEquipment.Instance != null)
            PlayerEquipment.Instance.SetHidden(true);

        rb.isKinematic = true;
        capsuleCollider.enabled = false;
        
        float elapsedTime = 0;
        Vector3 startPosition = transform.position;
        Quaternion startPlayerRotation = transform.rotation;

        Vector3 moveDirection = (targetPosition - startPosition).normalized;
        moveDirection.y = 0; 
        Quaternion targetPlayerRotation = Quaternion.LookRotation(moveDirection);

        while (elapsedTime < customDuration)
        {
            float t = elapsedTime / customDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float newX = Mathf.Lerp(startPosition.x, targetPosition.x, smoothT);
            float newZ = Mathf.Lerp(startPosition.z, targetPosition.z, smoothT);
            // Oblouk pomocí sin^2: svislá rychlost je NULOVÁ na začátku i na konci → hladký rozjezd a hlavně hladké dosednutí bez cuknutí.
            float arcPhase = Mathf.Sin(t * Mathf.PI);
            float arc = vaultArcHeight * arcPhase * arcPhase;
            float newY = Mathf.Lerp(startPosition.y, targetPosition.y, smoothT) + arc;
            transform.position = new Vector3(newX, newY, newZ);
            
            transform.rotation = Quaternion.Slerp(startPlayerRotation, targetPlayerRotation, smoothT);

            float leanAmount = Mathf.Sin(t * Mathf.PI) * vaultMaxLeanAngle;
            if (cameraHolder != null)
            {
                cameraHolder.localRotation = Quaternion.Euler(rotationX, 0, leanAmount);
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;

        // NEVOLÁME SetPlayerRotation – ta nuluje rotationX (svislý pohled) a přehazuje kameru na identity,
        // což způsobovalo cuknutí hlavy nahoru po přelezení. Yaw těla srovnáme na směr přelezení,
        // ale svislý pohled (rotationX) i plynulé řízení kamery ponecháme beze změny.
        transform.rotation = targetPlayerRotation;
        targetBodyRotation = targetPlayerRotation;
        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(rotationX, 0f, 0f); // sundá náklon (lean), zachová pohled nahoru/dolů
        }

        capsuleCollider.enabled = true;
        rb.isKinematic = false;

        // Po dokončení přelézání předmět zase vytáhneme (vyjede nahoru).
        if (PlayerEquipment.Instance != null)
            PlayerEquipment.Instance.SetHidden(false);

        isVaulting = false;
        Debug.Log("[Vault] Hotovo!");
    }
}