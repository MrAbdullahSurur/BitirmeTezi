using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Linq;

[RequireComponent(typeof(PlayerInput))]
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

    [SerializeField] private InputActionAsset inputActions;

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

    private PlayerInput _playerInput;
    private InputAction _moveAction;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player {OwnerClientId} spawned: IsHost={IsHost}, IsClient={IsClient}, IsOwner={IsOwner}");
        
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        if (_playerVisuals != null)
        {
            _spriteRenderer = _playerVisuals.GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer == null) { Debug.LogWarning("_playerVisuals içinde SpriteRenderer bulunamadı!", this); }
        }
        else { Debug.LogError("PlayerMovement: _playerVisuals referansı atanmamış!", this); }

        // Olaylara abone ol
        _networkVisualsActive.OnValueChanged += OnVisualsActiveChanged;
        _networkPlayerColor.OnValueChanged += OnPlayerColorChanged;
        
        // Başlangıç durumlarını uygula
        UpdateVisualsState(_networkVisualsActive.Value);
        ApplyPlayerColor(_networkPlayerColor.Value);

        // --- Sunucu Taraflı Renk Ayarı ---
        if (IsServer)
        {
            _networkPlayerColor.Value = IsHost ? Color.white : Color.green;
        }
        
        if (IsOwner)
        {
            _camera = Camera.main; 
            if (_camera == null) { Debug.LogError("Main Camera not found!"); }
            
            // Enable the component itself
            _playerInput.enabled = true; 
            
            // Determine and cache the correct 'Move' action based on Host/Client
            string mapNameToUse = IsHost ? "PlayerWASD" : "PlayerArrows";
            InputActionMap map = inputActions.FindActionMap(mapNameToUse, throwIfNotFound: true);
            if (map != null)
            {
                _moveAction = map.FindAction("Move", throwIfNotFound: true);
                if (_moveAction == null) Debug.LogError($"Could not find 'Move' action in map '{mapNameToUse}'!");
                // Enable the specific map (optional if default map logic works via Inspector)
                // map.Enable(); // Let PlayerInput handle map switching maybe?
                // Switch map using PlayerInput (if needed, ensure Default Map is set correctly in Inspector)
                _playerInput.SwitchCurrentActionMap(mapNameToUse);
                Debug.Log($"Owner {OwnerClientId}: Attempted switch to Action Map '{mapNameToUse}'");
            }
            else
            {
                 Debug.LogError($"Owner {OwnerClientId}: Could not find Action Map '{mapNameToUse}'!");
            }

            // Subscribe to the global action change event
            InputSystem.onActionChange += HandleActionChange;
            
            StartCoroutine(ActivateVisualsAfterFade()); 
        }
        else
        {   
             // Make sure non-owners PlayerInput is disabled
             _playerInput.enabled = false;
             _playerInput.DeactivateInput();
             _networkPositionTarget = transform.position;
             _networkRotationTarget = transform.rotation;
        }
        
        if (!ActivePlayers.Contains(this)) { ActivePlayers.Add(this); }
    }

    public override void OnNetworkDespawn()
    {
        // Olay aboneliklerini kaldır
        _networkVisualsActive.OnValueChanged -= OnVisualsActiveChanged;
        _networkPlayerColor.OnValueChanged -= OnPlayerColorChanged;
        
        // Unsubscribe from the global event IF WE ARE THE OWNER
        if (IsOwner)
        {
             InputSystem.onActionChange -= HandleActionChange;
        }
        
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
            //Debug.Log($"Oyuncu görselleri {(isActive ? "etkinleştirildi" : "devre dışı bırakıldı")}. IsOwner: {IsOwner}", this);
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

    private void FixedUpdate()
    {
        if (!IsOwner) 
        {
            // Non-owner interpolation using NetworkRigidbody2D
            return; 
        }
        
        // Owner Logic: Apply movement based on _movementInput updated by HandleActionChange
        ApplyMovement(); 
        SetAnimation(_movementInput != Vector2.zero); 
    }

    // Callback for the global InputSystem.onActionChange event
    private void HandleActionChange(object obj, InputActionChange change)
    {
        // We only care if we are the owner and the change is relevant to our 'Move' action
        if (!IsOwner || !(obj is InputAction action) || action != _moveAction)
        { 
            return;
        }

        // Check if the action was performed or canceled
        if (change == InputActionChange.ActionPerformed)
        { 
            _movementInput = action.ReadValue<Vector2>();
            // Log input received via global event
            // Debug.Log($"HandleActionChange (Owner {OwnerClientId}): Input {_movementInput}");
        }
        else if (change == InputActionChange.ActionCanceled) 
        {
            _movementInput = Vector2.zero;
            // Log input canceled
            // Debug.Log($"HandleActionChange (Owner {OwnerClientId}): Input Canceled");
        }
    }

    private void ApplyMovement()
    {
         // Apply smoothing
        _smoothedMovementInput = Vector2.SmoothDamp(
            _smoothedMovementInput, 
            _movementInput, 
            ref _movementInputSmoothVelocity, 
            0.1f); 

        // Apply velocity and rotation via Rigidbody
        SetPlayerVelocity(); 
        RotateInDirectionOfInput();
    }

    private void SetPlayerVelocity()
    {
        if (!IsOwner) return;
        _rigidbody.velocity = _smoothedMovementInput * _speed;
        PreventPlayerGoingOffScreen();
    }

    private void RotateInDirectionOfInput()
    {
        if (!IsOwner) return;
        if(_smoothedMovementInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, _smoothedMovementInput);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            _rigidbody.MoveRotation(rotation); 
        }
    }

    private void SetAnimation(bool isMoving)
    { 
        if (_animator != null) _animator.SetBool("IsMoving", isMoving); 
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
}
