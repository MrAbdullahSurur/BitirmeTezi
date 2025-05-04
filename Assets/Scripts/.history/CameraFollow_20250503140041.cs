using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // The target transform to follow (set by the player script)
    public float smoothSpeed = 0.125f; // Smoothing factor for camera movement
    public Vector3 offset = new Vector3(0, 0, -10); // Default camera offset (keep Z negative)

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            Debug.LogError("CameraFollow script requires a Camera component on the same GameObject!", this);
            enabled = false; // Disable script if no camera found
        }
    }

    void LateUpdate() // LateUpdate is recommended for camera movement
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime); // Apply smoothing based on deltaTime
            transform.position = smoothedPosition;
        }
        // If target is null, the camera stays where it is.
        // Consider adding logic here if you want the camera to reset or do something else.
    }

    // Public method to allow the Player script to set the target
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        Debug.Log($"Camera target set to: {(newTarget != null ? newTarget.name : "NULL")}", this);

        // Optional: Immediately snap to the target when it's first set
        if (target != null)
        {
             transform.position = target.position + offset;
        }
    }
} 