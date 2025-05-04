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
    private Vector3 _networkPositionTarget;
    private Quaternion _networkRotationTarget;
    private float _interpolationSpeed = 12f; 

    // --- Bileşen Referansları ---
    private SpriteRenderer _spriteRenderer;
    private PlayerInput _playerInput; // PlayerInput referansı

    // --- Host-Specific Client Control ---
    private ClientInputHandler _clientHandlerRef = null; // Referans client'ın input handler'ına
    private bool _foundClient = false;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player {OwnerClientId} spawned: IsHost={IsHost}, IsClient={IsClient}, IsOwner={IsOwner}");
        
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        // Initialize network interpolation targets
        if (!IsOwner)
        {
             _networkPositionTarget = transform.position;
             _networkRotationTarget = transform.rotation;
        }

        // If client, set up special handling
        if (IsClient && !IsHost && IsOwner)
        {
            // Disable NetworkRigidbody2D for client
            var networkRigidbody = GetComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
            if (networkRigidbody != null)
            {
                networkRigidbody.enabled = false;
                Debug.Log($"Player {OwnerClientId}: Disabled NetworkRigidbody2D for client control");
            }
            
            // Add direct input handler
            if (!TryGetComponent<ClientInputHandler>(out var clientHandler))
            {
                clientHandler = gameObject.AddComponent<ClientInputHandler>();
                Debug.Log($"Added ClientInputHandler to client player {OwnerClientId}");
            }
            
            // Make sure it's enabled
            clientHandler.enabled = true;
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

        // Only set up Player Input for the host
        if (IsOwner && IsHost)
        {
            _camera = Camera.main; 
            if (_camera == null) { Debug.LogError("Sahip oyuncu için Main Camera bulunamadı!"); }
            
            // Set up host input controls (WASD)
            if (_playerInput != null)
            {
                _playerInput.enabled = true;
                _playerInput.defaultActionMap = "PlayerWASD"; 
                _playerInput.SwitchCurrentActionMap("PlayerWASD");
                Debug.Log($"Host player {OwnerClientId}: Set control scheme to PlayerWASD");
            }
            
            StartCoroutine(ActivateVisualsAfterFade()); 
        }
        else if (!IsHost && IsOwner)
        {
            // For client with ownership but not host
            _camera = Camera.main;
            
            // Disable PlayerInput - we'll use ClientInputHandler instead
            if (_playerInput != null)
            {
                _playerInput.enabled = false;
                Debug.Log($"Client player {OwnerClientId}: Disabled PlayerInput in favor of direct input");
            }
            
            StartCoroutine(ActivateVisualsAfterFade());
        }
        else
        {
            // For objects we don't own
            if (_playerInput != null)
            {
                _playerInput.enabled = false;
            }
        }
        
        if (!ActivePlayers.Contains(this)) { ActivePlayers.Add(this); }

        // Host tries to find the client player to control
        if (IsHost) {
            StartCoroutine(FindClientPlayerReference());
        }

        // --- Print components for debugging ---
        PrintObjectHierarchy();
    }

    public override void OnNetworkDespawn()
    {
        // Olay aboneliklerini kaldır
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
        // Hala eski fade bekleme mantığını kullanıyoruz,
        // ama artık sadece sahibi NetworkVariable'ı güncelliyor.
        SceneFade sceneFade = FindObjectOfType<SceneFade>();
        while (sceneFade == null || !sceneFade.isActiveAndEnabled)
        {
            sceneFade = FindObjectOfType<SceneFade>();
            if (sceneFade != null && sceneFade.isActiveAndEnabled) break; 
            yield return null; 
        }
        while (sceneFade.isActiveAndEnabled)
        {
             yield return null; 
        }

        // Fade bitti, NetworkVariable'ı güncelle
        // Bu değişiklik tüm istemcilere ve hosta yayılacak
        _networkVisualsActive.Value = true;
    }

    // Host Coroutine to find the client player
    private IEnumerator FindClientPlayerReference()
    {
        while (!_foundClient)
        {
            yield return new WaitForSeconds(1.0f); // Check every second
            
            // Iterate through all players
            foreach (PlayerMovement player in ActivePlayers)
            {
                // Skip self
                if (player.OwnerClientId == OwnerClientId) continue;
                
                // Check if this player is likely the client (not host, has NetworkObject)
                if (!player.IsHost && player.NetworkObject != null) 
                {
                    _clientHandlerRef = player.GetComponent<ClientInputHandler>();
                    if (_clientHandlerRef != null)
                    {
                        Debug.Log($"Host found client player handler: {player.gameObject.name}");
                        _foundClient = true;
                        break; // Found it
                    }
                }
            }
            if (!_foundClient) {
                // Debug.Log("Host looking for client player...");
            }
        }
    }

    // --- Input Handling --- 
    
    // This is called by PlayerInput component for the HOST
    private void OnMove(InputValue inputValue)
    {
        if (!IsOwner || !IsHost) return; // Only host uses PlayerInput callbacks

        _movementInput = inputValue.Get<Vector2>();
        // Debug.Log($"[{Time.frameCount}] Host OnMove: Input {_movementInput}", this);
    }
    
    // UPDATE loop: Host reads its own input AND sends client input
    private void Update() 
    {
        // Host-specific logic
        if (IsOwner && IsHost) 
        { 
             // Handle HOST's input for client control
             HandleHostInputForClient();
        }
    }
    
    // HOST: Detects arrow keys/space and sends them to the client via ServerRpc
    private void HandleHostInputForClient()
    {
        if (!_foundClient || _clientHandlerRef == null) return; // No client found yet
        
        // Detect arrow keys for client movement
        float clientHorizontal = 0f;
        float clientVertical = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) clientVertical += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) clientVertical -= 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) clientHorizontal -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) clientHorizontal += 1f;
        
        Vector2 clientMoveInput = new Vector2(clientHorizontal, clientVertical);
        if (clientMoveInput.magnitude > 1f) 
            clientMoveInput.Normalize();
            
        // Detect space bar for client firing
        bool clientFireInput = Input.GetKey(KeyCode.Space);
        
        // Send input to the specific client's handler via ServerRpc
        // We call the ServerRpc on the *client's* handler object
        _clientHandlerRef.UpdateInputFromServerServerRpc(clientMoveInput, clientFireInput);
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
        {
            // Interpolate non-owner player movement IN FIXEDUPDATE
            Vector3 previousPos = transform.position;
            transform.position = Vector3.Lerp(transform.position, _networkPositionTarget, Time.fixedDeltaTime * _interpolationSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotationTarget, Time.fixedDeltaTime * _interpolationSpeed);
            
            // Add log to see interpolation happening
            if (Vector3.Distance(previousPos, transform.position) > 0.01f) // Log only if significant movement
            {
                 //Debug.Log($"NonOwner {OwnerClientId} interpolating. Target: {_networkPositionTarget}, Current: {transform.position}");
            }
            return; // Non-owners do nothing else in FixedUpdate
        }
        
        // --- Owner Logic Below ---

        // HOST movement logic
        if (IsHost)
        {
             SetPlayerVelocity();
             RotateInDirectionOfInput();
             SetAnimation(_movementInput != Vector2.zero);
        }
        // CLIENT movement logic
        else // No need for !IsHost check, since we returned if !IsOwner
        {
            // Process input smoothing regardless of movement input for smooth stops
            _smoothedMovementInput = Vector2.SmoothDamp(
                _smoothedMovementInput, 
                _movementInput, 
                ref _movementInputSmoothVelocity, 
                0.1f); // Smoothing time

            // Only move and rotate if there's significant smoothed input
            if (_smoothedMovementInput.sqrMagnitude > 0.001f)
            {
                // 1. Calculate desired position using smoothed input
                Vector3 moveDelta = new Vector3(_smoothedMovementInput.x, _smoothedMovementInput.y, 0) * _speed * Time.fixedDeltaTime; 
                Vector3 desiredPosition = transform.position + moveDelta;
                
                // 2. Apply position locally for responsiveness
                transform.position = desiredPosition;
                
                // 3. Handle rotation locally using smoothed input
                Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _smoothedMovementInput);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime); 
                
                // 4. Request server sync periodically (e.g., every 2 fixed updates)
                if (Time.frameCount % 2 == 0) 
                {
                     RequestTeleportServerRpc(transform.position, transform.rotation);
                     //Debug.Log($"Client requesting sync. Pos: {transform.position}, Rot: {transform.rotation.eulerAngles}");
                }
            }
        }
        
        // Animation update for owner (based on raw input)
        SetAnimation(_movementInput != Vector2.zero);
    }

    // Modify SetAnimation to accept moving state
    private void SetAnimation(bool isMoving)
    {
        // bool isMoving = _movementInput != Vector2.zero; // Original logic removed
        _animator.SetBool("IsMoving", isMoving);
    }

    [ServerRpc]
    private void SyncPositionToServerServerRpc(Vector3 position)
    {
        // On server, directly update position and tell all clients
        transform.position = position;
        
        // Notify all clients about this position
        SyncPositionToClientsClientRpc(position);
    }

    [ClientRpc]
    private void SyncPositionToClientsClientRpc(Vector3 position)
    {
        // Only update position for non-owner clients to avoid fighting with local control
        if (!IsOwner)
        {
            transform.position = position;
        }
    }

    [ServerRpc]
    private void SyncRotationToServerServerRpc(Quaternion rotation)
    {
        // Update rotation on server
        transform.rotation = rotation;
        
        // Broadcast to all clients
        SyncRotationToClientsClientRpc(rotation);
    }

    [ClientRpc]
    private void SyncRotationToClientsClientRpc(Quaternion rotation)
    {
        // Only update for non-owner objects
        if (!IsOwner)
        {
            transform.rotation = rotation;
        }
    }

    // Add a method to apply direct forces for client movement
    private void ApplyClientMovement()
    {
        if (!IsOwner || !IsClient || _movementInput == Vector2.zero)
            return;

        // Apply direct force to ensure movement works
        Vector2 force = _movementInput * _speed * 10f;
        _rigidbody.AddForce(force, ForceMode2D.Force);
        
        Debug.Log($"Client {OwnerClientId}: Applied direct force {force}", this);
    }

    private void SetPlayerVelocity()
    {
        // This method is now ONLY for the HOST
        if (!IsHost || !IsOwner) return;
        
        _smoothedMovementInput = Vector2.SmoothDamp(_smoothedMovementInput, _movementInput, ref _movementInputSmoothVelocity, 0.1f);
        _rigidbody.velocity = _smoothedMovementInput * _speed;
            
        if (_movementInput != Vector2.zero)
        {
             //Debug.Log($"Host velocity set to: {_rigidbody.velocity}");
        }
        
        PreventPlayerGoingOffScreen();
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

    private void RotateInDirectionOfInput()
    {
        // This is now ONLY for the HOST
        if (!IsHost || !IsOwner) return;
        
        if(_movementInput != Vector2.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _smoothedMovementInput);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            _rigidbody.MoveRotation(rotation); // Host uses Rigidbody rotation
        }
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

    // Modify ServerRpc to accept rotation as well
    [ServerRpc]
    private void RequestTeleportServerRpc(Vector3 position, Quaternion rotation)
    {
        // On server: teleport player directly
        //Debug.Log($"Server received sync request. Pos: {position}, Rot: {rotation.eulerAngles} for player {OwnerClientId}");
        
        // Validate position/rotation if needed
        
        // Force update transform on server
        transform.position = position;
        transform.rotation = rotation;
        
        // Update all clients with new transform
        TeleportPlayerClientRpc(position, rotation);
    }

    // Modify ClientRpc to accept rotation and store targets
    [ClientRpc]
    private void TeleportPlayerClientRpc(Vector3 position, Quaternion rotation)
    {
        // Update transform target on non-owner clients
        if (!IsOwner)
        {
            //Debug.Log($"Client {NetworkManager.Singleton.LocalClientId} received sync for {OwnerClientId}. Pos: {position}");
            _networkPositionTarget = position;
            _networkRotationTarget = rotation;
            // Do NOT set transform directly anymore
            // transform.position = position;
            // transform.rotation = rotation;
        }
        // Animator update moved to FixedUpdate for owner
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
}
