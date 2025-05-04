using UnityEngine;
using System.Collections;

public class CameraFollow : MonoBehaviour
{
    // Static reference for easy access
    public static CameraFollow Instance { get; private set; }
    
    public Transform target; // The target transform to follow (set by the player script)
    public float smoothSpeed = 5.0f; // Increased from 0.125f for more obvious movement
    public Vector3 offset = new Vector3(0, 0, -10); // Default camera offset (keep Z negative)

    [Header("Boundaries")]
    public SpriteRenderer backgroundSprite; // Assign your Background object's SpriteRenderer here

    // Target recovery data
    private string _targetName = "";
    private int _targetInstanceID = 0;
    
    private Camera _camera;
    private Bounds _cameraBounds;
    private Bounds _backgroundBounds;

    // Debug flag - set to true to enable detailed logs
    public bool debugMode = true;
    private float _debugLogInterval = 1.0f;  // Log every 1 second to avoid spam
    private float _lastLogTime = 0f;

    private void Awake()
    {
        // Check for duplicate instances
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"Multiple CameraFollow instances detected! This one: {gameObject.name} (ID: {GetInstanceID()}), Existing one: {Instance.gameObject.name} (ID: {Instance.GetInstanceID()})", this);
            // Don't disable, just log the warning
        }
        
        Instance = this;
        Debug.Log($"CameraFollow instance set on {gameObject.name} (ID: {GetInstanceID()})", this);
        
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            Debug.LogError("CameraFollow script requires a Camera component on the same GameObject!", this);
            enabled = false; // Disable script if no camera found
            return;
        }
        if (!_camera.orthographic)
        {
            Debug.LogError("CameraFollow boundary logic requires an Orthographic camera!", this);
            enabled = false;
            return;
        }
        if (backgroundSprite == null)
        {
            Debug.LogWarning("CameraFollow: Background Sprite Renderer not assigned. Boundaries will not be enforced.", this);
        }
        
        // Start a coroutine for recovery checks
        StartCoroutine(TargetRecoveryCheckRoutine());
    }
    
    // Attempt to recover target if lost
    private IEnumerator TargetRecoveryCheckRoutine()
    {
        while (true)
        {
            // If target is lost but we have its details, try to recover it
            if (target == null && _targetInstanceID != 0)
            {
                Debug.LogWarning($"CameraFollow: Target lost! Attempting to recover {_targetName} (ID: {_targetInstanceID})", this);
                
                // Option 1: Find by name if unique enough
                if (!string.IsNullOrEmpty(_targetName))
                {
                    GameObject foundObj = GameObject.Find(_targetName);
                    if (foundObj != null)
                    {
                        target = foundObj.transform;
                        Debug.Log($"CameraFollow: Recovered target {_targetName} by name", this);
                    }
                }
                
                // Option 2: Search all player movement components
                if (target == null)
                {
                    PlayerMovement[] allPlayers = FindObjectsOfType<PlayerMovement>();
                    Debug.Log($"CameraFollow: Found {allPlayers.Length} PlayerMovement objects in scene", this);
                    
                    foreach (var player in allPlayers)
                    {
                        if (player.IsOwner)
                        {
                            target = player.transform;
                            Debug.Log($"CameraFollow: Recovered target - owner player {player.gameObject.name}", this);
                            break;
                        }
                    }
                }
            }
            
            yield return new WaitForSeconds(1.0f); // Check every second
        }
    }

    void LateUpdate() // LateUpdate is recommended for camera movement
    {
        if (target == null) 
        {
            if (debugMode && Time.time > _lastLogTime + _debugLogInterval)
            {
                Debug.LogWarning($"CameraFollow: Target is null in LateUpdate! Camera will not move.", this);
                _lastLogTime = Time.time;
            }
            return;
        }

        // Calculate desired position
        Vector3 desiredPosition = target.position + offset;
        
        if (debugMode && Time.time > _lastLogTime + _debugLogInterval)
        {
            Debug.Log($"CameraFollow: Target={target.name}, TargetPos={target.position}, DesiredPos={desiredPosition}", this);
            _lastLogTime = Time.time;
        }

        // Apply boundaries if background is set
        if (backgroundSprite != null)
        {
            // Update bounds info (in case camera size or background changes)
            CalculateBounds();
            
            // Clamp the desired position
            float originalX = desiredPosition.x;
            float originalY = desiredPosition.y;
            
            float clampedX = Mathf.Clamp(desiredPosition.x, _backgroundBounds.min.x + _cameraBounds.extents.x, _backgroundBounds.max.x - _cameraBounds.extents.x);
            float clampedY = Mathf.Clamp(desiredPosition.y, _backgroundBounds.min.y + _cameraBounds.extents.y, _backgroundBounds.max.y - _cameraBounds.extents.y);
            
            desiredPosition = new Vector3(clampedX, clampedY, desiredPosition.z); // Keep original Z
            
            if (debugMode && Time.time > _lastLogTime + _debugLogInterval)
            {
                if (originalX != clampedX || originalY != clampedY)
                {
                    Debug.Log($"CameraFollow: Clamping position - X: {originalX}->{clampedX}, Y: {originalY}->{clampedY}", this);
                    Debug.Log($"CameraFollow: CameraBounds={_cameraBounds.size}, BackgroundBounds={_backgroundBounds.size}", this);
                }
            }
        }

        // Apply smoothing
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime); 
        Vector3 oldPosition = transform.position;
        transform.position = smoothedPosition;
        
        if (debugMode && Time.time > _lastLogTime + _debugLogInterval)
        {
            if (Vector3.Distance(oldPosition, smoothedPosition) > 0.01f)
            {
                Debug.Log($"CameraFollow: Moving camera from {oldPosition} to {smoothedPosition}", this);
            }
            else if (Vector3.Distance(oldPosition, desiredPosition) > 0.1f)
            {
                Debug.Log($"CameraFollow: Camera movement very slow, smoothSpeed={smoothSpeed}", this);
            }
            _lastLogTime = Time.time;
        }
    }

    // Calculate camera and background bounds
    private void CalculateBounds()
    {
        float camVertExtent = _camera.orthographicSize;
        float camHorzExtent = camVertExtent * _camera.aspect;
        _cameraBounds = new Bounds(Vector3.zero, new Vector3(camHorzExtent * 2, camVertExtent * 2, 0)); // Size = extents * 2

        if (backgroundSprite != null)
        {
            _backgroundBounds = backgroundSprite.bounds;
        }
    }

    // Public method to allow the Player script to set the target
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        
        // Store target details for recovery
        if (target != null)
        {
            _targetName = target.gameObject.name;
            _targetInstanceID = target.gameObject.GetInstanceID();
            Debug.Log($"Camera target set to: {_targetName} (ID: {_targetInstanceID})", this);
        }
        else
        {
            Debug.LogError("CameraFollow.SetTarget called with null Transform!", this);
            return;
        }

        // Optional: Immediately snap to the target and apply bounds
        if (target != null)
        {
             Vector3 targetPosition = target.position + offset;
             if (backgroundSprite != null)
             {
                 CalculateBounds();
                 float clampedX = Mathf.Clamp(targetPosition.x, _backgroundBounds.min.x + _cameraBounds.extents.x, _backgroundBounds.max.x - _cameraBounds.extents.x);
                 float clampedY = Mathf.Clamp(targetPosition.y, _backgroundBounds.min.y + _cameraBounds.extents.y, _backgroundBounds.max.y - _cameraBounds.extents.y);
                 targetPosition = new Vector3(clampedX, clampedY, targetPosition.z);
             }
             transform.position = targetPosition; 
        }
    }
} 