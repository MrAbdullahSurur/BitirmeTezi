using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // The target transform to follow (set by the player script)
    public float smoothSpeed = 0.125f; // Smoothing factor for camera movement
    public Vector3 offset = new Vector3(0, 0, -10); // Default camera offset (keep Z negative)

    [Header("Boundaries")]
    public SpriteRenderer backgroundSprite; // Assign your Background object's SpriteRenderer here

    private Camera _camera;
    private Bounds _cameraBounds;
    private Bounds _backgroundBounds;

    // Debug flag - set to true to enable detailed logs
    public bool debugMode = true;
    private float _debugLogInterval = 1.0f;  // Log every 1 second to avoid spam
    private float _lastLogTime = 0f;

    private void Awake()
    {
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
        Debug.Log($"Camera target set to: {(newTarget != null ? newTarget.name : "NULL")}", this);

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