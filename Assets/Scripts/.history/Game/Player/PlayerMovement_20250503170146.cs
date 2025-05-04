using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))] // PlayerInput bileşeninin olmasını zorunlu kıl
public class PlayerMovement : NetworkBehaviour
{
    // Statik liste: Tüm aktif oyuncuları tutar
    public static List<PlayerMovement> ActivePlayers = new List<PlayerMovement>();

    [Header("Movement Settings")]
    [SerializeField]
    private float _speed;

    [SerializeField]
    private float _rotationSpeed;

    [SerializeField]
    private float _screenBorder;
    
    [Header("References")]
    [SerializeField] 
    private GameObject _playerVisuals;

    // Ağ Değişkeni (Görsel Aktifliği)
    private NetworkVariable<bool> _networkVisualsActive = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner // Sadece sahip değiştirebilir
    );

    // Ağ Değişkeni (Oyuncu Rengi)
    private NetworkVariable<Color> _networkPlayerColor = new NetworkVariable<Color>(
        Color.white, // Varsayılan renk
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server // Sadece sunucu rengi belirleyebilir
    );

    private Rigidbody2D _rigidbody;
    private Vector2 _movementInput;
    private Vector2 _smoothedMovementInput;
    private Vector2 _movementInputSmoothVelocity;
    private Camera _camera;
    private Animator _animator;

    // --- Network Interpolation Data ---
    private NetworkVariable<Vector3> _networkPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Quaternion> _networkRotation = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float _interpolationSpeed = 15f; // Increased speed slightly

    // --- Bileşen Referansları ---
    private SpriteRenderer _spriteRenderer;
    private PlayerInput _playerInput; // PlayerInput referansı

    // --- Host-Specific Client Control ---
    private PlayerMovement _clientPlayerMovementRef = null; // Reference to client's PlayerMovement on the server
    private PlayerShoot _clientPlayerShootRef = null; // Reference to client's PlayerShoot on the server
    private ulong _clientNetworkId = 0;
    private bool _foundClient = false;

    // --- Flags ---
    private bool _isServerControlled = false; // Flag for client object if being moved by server

    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player {OwnerClientId} spawned: IsHost={IsHost}, IsClient={IsClient}, IsOwner={IsOwner}");
        
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        // Subscribe to NetworkVariable changes for interpolation
        if (!IsOwner) {
            _networkPosition.OnValueChanged += OnNetworkPositionChanged;
            _networkRotation.OnValueChanged += OnNetworkRotationChanged;
        }

        // Connect to HealthController's OnDied event if on the server
        if (IsServer)
        {
            HealthController healthController = GetComponent<HealthController>();
            if (healthController != null)
            {
                healthController.OnDied.AddListener(OnPlayerDied);
                Debug.Log($"Player {OwnerClientId} connected to HealthController OnDied event");
            }
        }

        // Görsel referansını ve SpriteRenderer'ı bul
        if (_playerVisuals != null)
        {
            _spriteRenderer = _playerVisuals.GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                 Debug.LogWarning("_playerVisuals içinde SpriteRenderer bulunamadı!", this);
            }
        }
        else
        {   
             Debug.LogError("PlayerMovement: _playerVisuals referansı atanmamış!", this);
        }

        // Olaylara abone ol
        _networkVisualsActive.OnValueChanged += OnVisualsActiveChanged;
        _networkPlayerColor.OnValueChanged += OnPlayerColorChanged;
        
        // Başlangıç durumlarını uygula
        UpdateVisualsState(_networkVisualsActive.Value);
        ApplyPlayerColor(_networkPlayerColor.Value); // Başlangıç rengini uygula

        // --- Sunucu Taraflı Renk Ayarı ---
        if (IsServer)
        {
            // Eğer bu oyuncu Host'un kendisi değilse (yani katılan bir client ise) rengi yeşil yap
            // OwnerClientId kullanarak kontrol edebiliriz. NetworkManager.LocalClientId Host'un ID'sidir.
            if (OwnerClientId != NetworkManager.Singleton.LocalClientId)
            {
                 _networkPlayerColor.Value = Color.green;
            }
            else
            {
                 _networkPlayerColor.Value = Color.white; // Host'un rengi beyaz kalsın
            }
        }
        // --- Sunucu Taraflı Renk Ayarı Sonu ---

        _playerInput = GetComponent<PlayerInput>();

        // --- Role-Specific Setup ---
        if (IsOwner)
        {
            // Set the camera target for the local player - DELAYED
            // SetCameraTarget(); // REMOVED direct call
            StartCoroutine(SetCameraTargetDelayed()); // ADDED delayed call
            
            if (IsHost) // Host Setup
            {
                if (_playerInput != null)
                {
                    _playerInput.enabled = true;
                    _playerInput.defaultActionMap = "PlayerWASD"; 
                    _playerInput.SwitchCurrentActionMap("PlayerWASD");
                    Debug.Log($"Host player {OwnerClientId}: Set control scheme to PlayerWASD");
                }
                StartCoroutine(FindClientPlayerReference()); // Start looking for client
            }
            else // Client Setup
            {
                // Disable PlayerInput, disable ClientInputHandler
                if (_playerInput != null) _playerInput.enabled = false;
                var clientHandler = GetComponent<ClientInputHandler>();
                if (clientHandler != null) clientHandler.enabled = false;
                Debug.Log($"Client player {OwnerClientId}: Input handled by Server.");
                
                // Client Rigidbody should be kinematic if moved by server directly
                // _rigidbody.isKinematic = true; 
            }
             StartCoroutine(ActivateVisualsAfterFade());
             
             // Start Coroutine to connect UI - USE THE CORRECT ONE
             // StartCoroutine(ConnectUIElementsDelayed()); // REMOVED old call
             StartCoroutine(ConnectUIElementsCoroutine()); // ADDED call to the waiting coroutine
        }
        else // Non-Owned Player Setup
        {
            if (_playerInput != null) _playerInput.enabled = false;
             // Non-owner Rigidbody should be kinematic for interpolation
            // _rigidbody.isKinematic = true; 
        }
        
        // Disable network physics components if not server
        // Server needs them enabled to control physics
        if (!IsServer)
        {
             var networkRigidbody = GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
             if (networkRigidbody != null) networkRigidbody.enabled = false;
             // NetworkTransform handles sync, so we disable it on non-server instances if server controls rigidbody
             // var networkTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
             // if (networkTransform != null) networkTransform.enabled = false; 
        }
        
        if (!ActivePlayers.Contains(this)) { ActivePlayers.Add(this); }
        
        // --- UI Bağlantıları (Sadece Sahip Olan Client) ---
        // REMOVED: This direct call was happening too early
        /*
        if (IsOwner)
        {
            ConnectUIElements();
        }
        */
        
        PrintObjectHierarchy();
    }

    public override void OnNetworkDespawn()
    {
        // Olay aboneliklerini kaldır
        _networkPosition.OnValueChanged -= OnNetworkPositionChanged;
        _networkRotation.OnValueChanged -= OnNetworkRotationChanged;
        _networkVisualsActive.OnValueChanged -= OnVisualsActiveChanged;
        _networkPlayerColor.OnValueChanged -= OnPlayerColorChanged;
        
        if (ActivePlayers.Contains(this)) { ActivePlayers.Remove(this); }
        base.OnNetworkDespawn();
    }

    // Görsel aktifliği değişince çağrılır
    private void OnVisualsActiveChanged(bool previousValue, bool newValue)
    {
        UpdateVisualsState(newValue);
    }

    // Oyuncu rengi değişince çağrılır
    private void OnPlayerColorChanged(Color previousValue, Color newValue)
    {
        ApplyPlayerColor(newValue);
    }

    // Görsellerin aktif/pasif durumunu ayarlar
    private void UpdateVisualsState(bool isActive)
    {
        if (_playerVisuals != null)
        {
            _playerVisuals.SetActive(isActive);
            Debug.Log($"Oyuncu görselleri {(isActive ? "etkinleştirildi" : "devre dışı bırakıldı")}. IsOwner: {IsOwner}", this);
        }
        else
        {
            // Bu hata OnNetworkSpawn'da zaten loglanıyordu, tekrar etmeye gerek yok
            // Debug.LogError("PlayerMovement: _playerVisuals referansı atanmamış!", this);
        }
    }

    // Sprite rengini uygular
    private void ApplyPlayerColor(Color color)
    {
         if (_spriteRenderer != null)
         {
             _spriteRenderer.color = color;
         }
    }

    // Sahip fade bitince görselleri aktif eder (NetworkVariable aracılığıyla)
    private IEnumerator ActivateVisualsAfterFade()
    {
        // Simplified: Wait 1 second after spawn to activate visuals
        yield return new WaitForSeconds(1.0f);
        if (IsOwner) 
        { 
             _networkVisualsActive.Value = true;
             Debug.Log($"Player {OwnerClientId} visuals activated.");
        }
    }

    // Host Coroutine to find the client player's components on the server
    private IEnumerator FindClientPlayerReference()
    {
        while (!_foundClient)
        {
            yield return new WaitForSeconds(0.5f); // Check more frequently
            
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) continue;
            
            // Iterate through connected clients (excluding host)
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.Singleton.LocalClientId) continue; // Skip host itself
                
                // Get the NetworkObject for the client (Corrected Usage)
                NetworkObject clientNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (clientNetworkObject != null) // Check if the object was found
                {
                    _clientPlayerMovementRef = clientNetworkObject.GetComponent<PlayerMovement>();
                    _clientPlayerShootRef = clientNetworkObject.GetComponent<PlayerShoot>();
                    _clientNetworkId = clientNetworkObject.NetworkObjectId;
                    
                    if (_clientPlayerMovementRef != null && _clientPlayerShootRef != null)
                    {
                        Debug.Log($"Host found client components for ID: {clientId} (NetworkID: {_clientNetworkId})");
                        _foundClient = true;
                        _clientPlayerMovementRef._isServerControlled = true; // Mark client as server-controlled
                        break; 
                    }
                }
            }
            // if (!_foundClient) Debug.Log("Host looking for client player...");
        }
    }

    // --- Input Handling & Control --- 
    
    // Host PlayerInput callback
    private void OnMove(InputValue inputValue)
    {
        if (!IsOwner || !IsHost) return;
        _movementInput = inputValue.Get<Vector2>();
    }
    
    // UPDATE loop: Host reads its own input AND arrow keys for client control
    private void Update() 
    {
        // Host reads arrow/space keys and sends control command to server
        if (IsOwner && IsHost && _foundClient) 
        { 
             HandleHostInputForClient();
        }
        
        // Non-owners interpolate towards network position/rotation
        if (!IsOwner && !IsServer) // Interpolation for clients viewing other players
        {
            InterpolateMovement();
        }
    }
    
    // HOST: Detects arrow keys/space and sends ServerRpc to control the client
    private void HandleHostInputForClient()
    {
        if (!_foundClient || _clientNetworkId == 0) return; 
        
        float clientHorizontal = 0f;
        float clientVertical = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) clientVertical += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) clientVertical -= 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) clientHorizontal -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) clientHorizontal += 1f;
        
        Vector2 clientMoveInput = new Vector2(clientHorizontal, clientVertical).normalized;
        bool clientFireInput = Input.GetKey(KeyCode.Space);
        
        // Send control command to the server for the target client
        ControlClientPlayerServerRpc(_clientNetworkId, clientMoveInput, clientFireInput);
    }

    // SERVER RPC: Called by the Host to control a specific client player
    [ServerRpc(RequireOwnership = false)] // Host needs to call this, doesn't own client object
    private void ControlClientPlayerServerRpc(ulong targetNetworkObjectId, Vector2 moveInput, bool fireInput)
    {
        // This code runs ON THE SERVER (Host)
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObject))
        {
            Debug.LogError($"Server couldn't find target NetworkObject: {targetNetworkObjectId}");
            return;
        }
        
        PlayerMovement targetMovement = targetObject.GetComponent<PlayerMovement>();
        PlayerShoot targetShoot = targetObject.GetComponent<PlayerShoot>();
        
        if (targetMovement != null)
        {
            targetMovement.ServerControlledMoveAndRotate(moveInput);
        }
        
        if (fireInput && targetShoot != null)
        {
            targetShoot.ServerControlledFire();
        }
    }
    
    // METHOD EXECUTED ON SERVER: Moves and rotates the player based on server command
    public void ServerControlledMoveAndRotate(Vector2 moveInput)
    {
        if (!IsServer) return; // Only server should execute this
        
        // Apply movement directly to transform or rigidbody
        Vector3 moveDelta = new Vector3(moveInput.x, moveInput.y, 0) * _speed * Time.fixedDeltaTime; 
        Vector2 newPosition = _rigidbody.position + (Vector2)moveDelta; // Use Rigidbody position

        // Apply rotation
        if (moveInput != Vector2.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, moveInput); // Use Vector3.forward for 2D
            float targetAngle = targetRotation.eulerAngles.z;
            float currentAngle = _rigidbody.rotation;
            float maxDelta = _rotationSpeed * Time.fixedDeltaTime;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxDelta);
            _rigidbody.MoveRotation(newAngle); // Apply the calculated angle
        }

        _rigidbody.MovePosition(newPosition);
        
        // Update NetworkVariables for clients (Used for interpolation if NetworkTransform is not primary)
        _networkPosition.Value = _rigidbody.position;
        _networkRotation.Value = Quaternion.Euler(0, 0, _rigidbody.rotation); 
        
        // Re-added animator update:
        if(_animator != null) _animator.SetBool("IsMoving", moveInput != Vector2.zero);
    }

    private void FixedUpdate()
    {
        // --- Host Movement Logic --- 
        if (IsOwner && IsHost)
        { 
             SetPlayerVelocity(); // Uses _movementInput (WASD)
             RotateInDirectionOfInput(); // Uses _movementInput (WASD)
             
             // Host updates its OWN animator directly for responsiveness
             // NetworkAnimator will sync this state from the host to others
             SetAnimation(_movementInput != Vector2.zero);
             
             // Update network vars for host's own movement (if not using NetworkTransform primarily)
             _networkPosition.Value = transform.position; // Use transform for host? Or rigidbody?
             _networkRotation.Value = transform.rotation;
        }
        
        // Client movement is now handled by ServerControlledMoveAndRotate via RPC
        // Interpolation is handled in Update for non-owners
    }

    private void SetAnimation(bool isMoving)
    {
       if(_animator != null) _animator.SetBool("IsMoving", isMoving);
    }

    // --- Host-specific Movement (WASD) ---
    private void SetPlayerVelocity()
    {
        if (!IsHost || !IsOwner) return;
        _smoothedMovementInput = Vector2.SmoothDamp(_smoothedMovementInput, _movementInput, ref _movementInputSmoothVelocity, 0.1f);
        _rigidbody.velocity = _smoothedMovementInput * _speed;
        PreventPlayerGoingOffScreen();
    }
    private void RotateInDirectionOfInput()
    {
        if (!IsHost || !IsOwner || _movementInput == Vector2.zero) return;
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, _smoothedMovementInput);
        Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
        _rigidbody.MoveRotation(rotation); 
    }
    private void PreventPlayerGoingOffScreen()
    {
        // Fix camera reference handling for client
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogWarning($"Player {OwnerClientId}: Cannot find main camera for screen boundary check", this);
                return;
            }
        }
        
        Vector2 screenPosition = _camera.WorldToScreenPoint(transform.position);
        float cameraWidth = _camera.pixelWidth;
        float cameraHeight = _camera.pixelHeight;

        if ((screenPosition.x < _screenBorder && _rigidbody.velocity.x < 0) || (screenPosition.x > cameraWidth - _screenBorder && _rigidbody.velocity.x > 0))
        {
            _rigidbody.velocity = new Vector2(0, _rigidbody.velocity.y);
        }

        if ((screenPosition.y < _screenBorder && _rigidbody.velocity.y < 0) || (screenPosition.y > cameraHeight - _screenBorder && _rigidbody.velocity.y > 0))
        {
            _rigidbody.velocity = new Vector2(_rigidbody.velocity.x,0);
        }
    }

    // --- Network Position/Rotation Sync --- 
    private void OnNetworkPositionChanged(Vector3 previous, Vector3 current)
    {
        // Received new position from server - target interpolation
    }
    private void OnNetworkRotationChanged(Quaternion previous, Quaternion current)
    {
        // Received new rotation from server - target interpolation
    }

    // Add debugging method
    [ContextMenu("DebugInputSystem")]
    private void DebugInputSystem()
    {
        if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
        
        Debug.Log($"Player {OwnerClientId} Input Debug:" +
                  $"\nEnabled: {_playerInput.enabled}" + 
                  $"\nCurrent Map: {_playerInput.currentActionMap?.name ?? "NULL"}" +
                  $"\nDefault Map: {_playerInput.defaultActionMap}" +
                  $"\nIs Host: {IsHost}" +
                  $"\nIs Owner: {IsOwner}");
    }

    private void ForceDisableNetworkRigidbody()
    {
        if (!IsOwner || !IsClient) return;
        
        Debug.Log($"Player {OwnerClientId}: Forcefully disabling network components...");
        
        // Disable all network components that might interfere with movement
        var networkComponents = new System.Type[] {
            typeof(Unity.Netcode.Components.NetworkRigidbody2D),
            typeof(Unity.Netcode.Components.NetworkTransform)
        };
        
        foreach (var componentType in networkComponents)
        {
            var component = GetComponent(componentType) as Behaviour;
            if (component != null)
            {
                component.enabled = false;
                Debug.Log($"Player {OwnerClientId}: Disabled {componentType.Name}");
            }
        }
        
        // For the client's player:
        if (IsOwner && !IsHost)
        {
            // Set Rigidbody to kinematic and disable simulation to prevent physics interference
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.simulated = false; // Disable physics simulation entirely
                _rigidbody.velocity = Vector2.zero;
                Debug.Log($"Player {OwnerClientId}: Set client Rigidbody2D to kinematic AND disabled simulation");
            }
        }
    }

    // Debugging utility to print entire component list
    private void PrintObjectHierarchy()
        {
        var components = GetComponents<Component>();
        string componentList = $"Player {OwnerClientId} components:";
        
        foreach (var component in components)
        {
            componentList += $"\n - {component.GetType().Name} (Enabled: {(component is Behaviour b ? b.enabled.ToString() : "N/A")})";
        }
        
        Debug.Log(componentList);
    }

    // Helper method to add fallback handler if needed - can be called manually via SendMessage
    private void TryAddFallbackHandler()
    {
        if (!IsOwner || IsHost) return;
        
        // Check if we need the fallback handler
        if (GetComponent<ClientInputFallback>() == null)
        {
            var fallbackHandler = gameObject.AddComponent<ClientInputFallback>();
            fallbackHandler.enabled = false; // Keep disabled by default
            Debug.Log($"Added fallback input handler to client {OwnerClientId} (disabled by default)", this);
        }
    }
    
    // This can be called via debug console to switch to the fallback handler
    [ContextMenu("Switch To Fallback Input")]
    public void SwitchToFallbackInput()
    {
        if (!IsOwner || IsHost) return;
        
        Debug.Log("Attempting to switch to fallback input handler", this);
        
        // Disable current input handlers
        var currentHandler = GetComponent<ClientInputHandler>();
        if (currentHandler != null)
        {
            currentHandler.enabled = false;
        }
        
        // Disable PlayerInput
        if (_playerInput != null)
        {
            _playerInput.enabled = false;
        }
        
        // Make sure fallback exists and enable it
        var fallback = GetComponent<ClientInputFallback>();
        if (fallback == null)
        {
            fallback = gameObject.AddComponent<ClientInputFallback>();
        }
        
        fallback.enabled = true;
        Debug.Log("Switched to fallback input handler", this);
    }

    // --- UI Bağlantı Metodları ---
    // REMOVED: This coroutine is no longer used
    /*
    private IEnumerator ConnectUIElementsDelayed()
    {
        // Instance'ın Awake'de ayarlanması için bir frame beklemek yeterli olabilir.
        yield return null; 
        ConnectUIElements();
    }
    */

    private void ConnectUIElements()
    {
        if (!IsOwner) return; 
        
        // HealthBar UI (Static referansı kullan)
        if (HealthBarUI.Instance != null) 
        {
            HealthController healthController = GetComponent<HealthController>();
            if (healthController != null)
            {
                HealthBarUI.Instance.SetTrackedHealthController(healthController);
                Debug.Log($"Local Player {OwnerClientId} connected its HealthController to HealthBarUI.Instance.");
            }
            else
            {
                 Debug.LogWarning($"Local Player {OwnerClientId} could not find its HealthController.");
            }
        }
        else
        { 
            Debug.LogWarning($"Local Player {OwnerClientId} could not find HealthBarUI.Instance in the scene.");
        }
    }

    private void InterpolateMovement()
    {
        transform.position = Vector3.Lerp(transform.position, _networkPosition.Value, Time.deltaTime * _interpolationSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotation.Value, Time.deltaTime * _interpolationSpeed);
    }

    private IEnumerator ConnectUIElementsCoroutine()
    {
        // Wait until HealthBarUI.Instance is set, with a timeout
        float waitTime = 0f;
        float maxWaitTime = 2f; // Wait a maximum of 2 seconds

        while (HealthBarUI.Instance == null && waitTime < maxWaitTime)
        {
            yield return null; // Wait for the next frame
            waitTime += Time.deltaTime;
        }

        // Now attempt to connect
        ConnectUIElements();

        if (waitTime >= maxWaitTime)
        {
            Debug.LogError($"Local Player {OwnerClientId} timed out waiting for HealthBarUI.Instance.", gameObject);
        }
    }

    // --- Camera Targeting --- 
    
    private IEnumerator SetCameraTargetDelayed()
    {
        // Wait an initial delay
        yield return new WaitForSeconds(0.5f);
        
        // Try finding CameraFollow with retries
        float startTime = Time.time;
        float maxWaitTime = 10f; // Give up after 10 seconds
        bool cameraFound = false;
        
        while (!cameraFound && Time.time < startTime + maxWaitTime)
        {
            Debug.Log($"Player {OwnerClientId}: Attempt to find CameraFollow at time {Time.time - startTime:F1}s");
            
            // Try to find CameraFollow
            CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
            
            if (cameraFollow != null)
            {
                // Found it!
                Debug.Log($"Player {OwnerClientId}: Found CameraFollow instance on GameObject '{cameraFollow.gameObject.name}' after {Time.time - startTime:F1}s", cameraFollow.gameObject);
                cameraFollow.SetTarget(this.transform);
                Debug.Log($"Player {OwnerClientId}: Successfully set CameraFollow target.", this);
                cameraFound = true;
                break; // Exit the loop
            }
            
            Debug.Log($"Player {OwnerClientId}: CameraFollow not found yet, waiting...");
            
            // Wait before trying again
            yield return new WaitForSeconds(0.5f);
        }
        
        // Log if we couldn't find it after all retries
        if (!cameraFound)
        {
            Debug.LogError($"Player {OwnerClientId}: Failed to find CameraFollow after {maxWaitTime}s of trying. Check that the script is on an active GameObject in the scene.", this);
        }
    }

    // Original method to set camera target (now called by the coroutine)
    private void SetCameraTarget()
    {
        if (!IsOwner) return; // Only the owner controls its camera

        // --- Robust CameraFollow Finding --- 
        CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();

        if (cameraFollow != null)
        {
            Debug.Log($"Player {OwnerClientId}: Found CameraFollow instance via FindObjectOfType on GameObject '{cameraFollow.gameObject.name}'.", cameraFollow.gameObject);
            cameraFollow.SetTarget(this.transform);
            Debug.Log($"Player {OwnerClientId} successfully set CameraFollow target.", this);
            
            // Optionally, still try to get the Camera component for boundary checks later if needed
            _camera = cameraFollow.GetComponent<Camera>();
            if (_camera == null)
            {
                 Debug.LogWarning("The found CameraFollow script is not attached to a GameObject with a Camera component!", cameraFollow.gameObject);
            }
        }
        else
        {
            Debug.LogError($"Player {OwnerClientId} could NOT find any active CameraFollow component in the scene using FindObjectOfType. Ensure the script is present, enabled, and on an active GameObject in the gameplay scene.", this);
            
            // Fallback to old method for logging if needed
            _camera = Camera.main;
            if (_camera != null)
            {
                Debug.Log($"(Fallback Check) Found camera tagged MainCamera: '{_camera.gameObject.name}'", _camera.gameObject);
                var components = _camera.GetComponents<Component>();
                 string componentList = "Components on " + _camera.gameObject.name + ":";
                 foreach (var component in components)
                 {
                     componentList += "\n - " + component.GetType().Name;
        }
                 Debug.Log(componentList);
            }
            else
            {
                Debug.Log("(Fallback Check) Camera.main is also null.");
            }
        }
        // --- End Robust Finding ---
    }

    private void OnPlayerDied()
    {
        Debug.Log($"Player {OwnerClientId} died!");
        
        // Notify GameManager of player death (only on server)
        if (IsServer)
        {
            GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                gameManager.OnPlayerDied(OwnerClientId);
                Debug.Log($"GameManager notified of Player {OwnerClientId} death");
            }
        }
        
        // Disable THIS player's movement but keep visuals
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector2.zero;
            _rigidbody.isKinematic = true;
        }
        
        // Show death effect, etc. if needed
        if (_animator != null)
    {
            _animator.SetBool("IsMoving", false);
            _animator.SetBool("IsDead", true);
        }
        
        // Disable this player's input handling
        if (_playerInput != null)
        {
            _playerInput.enabled = false;
        }
        
        // Disable this specific player's scripts that handle movement or shooting
        PlayerShoot playerShoot = GetComponent<PlayerShoot>();
        if (playerShoot != null)
        {
            playerShoot.enabled = false;
        }
        
        // Disable this player's collider to prevent further interactions
        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }
    }
}
