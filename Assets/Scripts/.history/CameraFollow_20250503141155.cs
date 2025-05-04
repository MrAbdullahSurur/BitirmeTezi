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
        if (target == null) return; // Don't move if no target

        // Calculate desired position
        Vector3 desiredPosition = target.position + offset;

        // Apply boundaries if background is set
        if (backgroundSprite != null)
        {
            // Update bounds info (in case camera size or background changes)
            CalculateBounds();
            
            // Clamp the desired position
            float clampedX = Mathf.Clamp(desiredPosition.x, _backgroundBounds.min.x + _cameraBounds.extents.x, _backgroundBounds.max.x - _cameraBounds.extents.x);
            float clampedY = Mathf.Clamp(desiredPosition.y, _backgroundBounds.min.y + _cameraBounds.extents.y, _backgroundBounds.max.y - _cameraBounds.extents.y);
            
            desiredPosition = new Vector3(clampedX, clampedY, desiredPosition.z); // Keep original Z
        }

        // Apply smoothing
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime); 
        transform.position = smoothedPosition;
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