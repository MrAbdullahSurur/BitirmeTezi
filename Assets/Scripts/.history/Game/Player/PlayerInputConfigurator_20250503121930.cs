using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;

#if UNITY_EDITOR
[ExecuteInEditMode]
public class PlayerInputConfigurator : MonoBehaviour
{
    [SerializeField] private PlayerInput _playerInput;
    
    [Header("Configuration Settings")]
    [SerializeField] private bool _configureOnAwake = true;
    [SerializeField] private string _arrowsActionMap = "PlayerArrows";
    [SerializeField] private string _wasdActionMap = "PlayerWASD";
    
    private void Awake()
    {
        if (_configureOnAwake)
        {
            ConfigurePlayerInput();
        }
    }
    
    // This will run in editor mode to help configure the component
    private void OnValidate()
    {
        if (_playerInput == null)
        {
            _playerInput = GetComponent<PlayerInput>();
        }
    }
    
    [ContextMenu("Configure Player Input")]
    public void ConfigurePlayerInput()
    {
        if (_playerInput == null)
        {
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput == null)
            {
                Debug.LogError("No PlayerInput component found on this GameObject!", this);
                return;
            }
        }
        
        // Ensure PlayerInput has the right settings
        _playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        
        // Configure for multi-player local play
        _playerInput.neverAutoSwitchControlSchemes = true;
        
        // Check action maps available
        bool hasMaps = false;
        if (_playerInput.actions != null)
        {
            foreach (var map in _playerInput.actions.actionMaps)
            {
                Debug.Log($"Found action map: {map.name}");
                hasMaps = true;
            }
        }
        
        if (!hasMaps)
        {
            Debug.LogError("PlayerInput has no action maps! Check your Input Actions asset.", _playerInput);
        }
        
        // Important - let Unity know we've made changes
        EditorUtility.SetDirty(_playerInput);
        
        Debug.Log("PlayerInput component configured for multiplayer use!", this);
    }
    
    [ContextMenu("Fix Control Schemes")]
    public void FixControlSchemes()
    {
        if (_playerInput == null) return;
        
        // Apply settings for proper multiplayer control schemes
        _playerInput.neverAutoSwitchControlSchemes = true;
        _playerInput.defaultActionMap = _wasdActionMap; // Default to WASD
        
        // Important - let Unity know we've made changes
        EditorUtility.SetDirty(_playerInput);
        
        Debug.Log("Fixed control schemes - set never auto switch and default to WASD!", this);
    }
}
#endif 