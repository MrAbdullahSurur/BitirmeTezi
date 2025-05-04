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

    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player {OwnerClientId} spawned: IsHost={IsHost}, IsClient={IsClient}, IsOwner={IsOwner}");
        
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _playerInput = GetComponent<PlayerInput>(); 

        if (_playerVisuals != null)
        {
            _spriteRenderer = _playerVisuals.GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer == null) { Debug.LogWarning("_playerVisuals içinde SpriteRenderer bulunamadı!", this); }
        }
        else { Debug.LogError("PlayerMovement: _playerVisuals referansı atanmamış!", this); }

        // Subscribe to NetworkVariable changes
        _networkVisualsActive.OnValueChanged += OnVisualsActiveChanged;
        _networkPlayerColor.OnValueChanged += OnPlayerColorChanged;
        
        // --- Server Side Color Setup ---
        if (IsServer)
        {
            _networkPlayerColor.Value = IsHost ? Color.white : Color.green;
        }
        
        if (IsOwner)
        {   
            _camera = Camera.main; 
            if (_camera == null) { Debug.LogError("Main Camera not found!"); }
            
            // --- Enable PlayerInput and Switch Map (if Client) ---
            _playerInput.enabled = true; 
            if (!IsHost) // If this owner is a Client
            {
                 try 
                 {
                      // Ensure the Default Action Map is set to PlayerWASD in Inspector
                      _playerInput.SwitchCurrentActionMap("PlayerArrows"); 
                      Debug.Log($"Client {OwnerClientId}: Switched PlayerInput to map: PlayerArrows");
                 }
                 catch (System.Exception e)
                 {
                      Debug.LogError($"Client {OwnerClientId}: FAILED to switch to action map 'PlayerArrows'. Error: {e.Message}", this);
                 }
            }
            else // If this owner is the Host
            {
                 // Host uses the Default Map (PlayerWASD) set in Inspector
                 Debug.Log($"Host {OwnerClientId}: Using default action map: {_playerInput.defaultActionMap} (Should be PlayerWASD)");
            }
            // --- End PlayerInput Setup ---
            
            StartCoroutine(ActivateVisualsAfterFade()); 
        }
        else
        {
             // For non-owners, ensure PlayerInput is disabled
             _playerInput.enabled = false;
             _playerInput.DeactivateInput();
             // Initialize interpolation targets
             _networkPositionTarget = transform.position;
             _networkRotationTarget = transform.rotation;
        }
        
        if (!ActivePlayers.Contains(this)) { ActivePlayers.Add(this); }
    }

    public override void OnNetworkDespawn()
    {
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

    // --- Input Handling (Re-enable OnMove) ---
    private void OnMove(InputValue inputValue)
    {
        // Log ALL calls to see if input arrives when not focused
        Debug.Log($"OnMove triggered on {gameObject.name}! IsOwner: {IsOwner}, Value: {inputValue.Get<Vector2>()}", this); 
        
        // Only Owner should process input
        if (!IsOwner) return;
        
        _movementInput = inputValue.Get<Vector2>();
    }

    // --- Movement Logic (Back to using Rigidbody) ---
    private void FixedUpdate()
    {
        if (!IsOwner) 
        {
            // Non-owner interpolation (Rely on NetworkRigidbody2D)
            return; 
        }
        
        // Owner movement logic
        ApplyMovement(); 
        SetAnimation(_movementInput != Vector2.zero); 
    }

    private void ApplyMovement()
    {
        _smoothedMovementInput = Vector2.SmoothDamp(
            _smoothedMovementInput, 
            _movementInput, 
            ref _movementInputSmoothVelocity, 
            0.1f); 

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
