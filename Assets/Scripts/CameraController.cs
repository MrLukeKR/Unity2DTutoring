using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;                 // The player character to follow
    public float horizontalDeadzone = 4f;    // Width of the center area where camera doesn't move
    public float verticalDeadzone = 3f;      // Height of the center area where camera doesn't move
    public float cameraSpeed = 5f;           // How quickly the camera moves to follow player

    public ParallaxScroller parallax;        // Reference to the parallax controller

    [Header("Level Boundaries")]
    public float leftBoundary = -10f;        // Left-most point the player can go
    public float rightBoundary = 10f;        // Right-most point the player can go

    private Vector3 velocity = Vector3.zero;
    private float targetX;
    private bool isAtEdge = false;
    private float lastPlayerX;

    private Camera mainCamera;
    private float cameraWidth;
    private float cameraHeight;
    private CharacterAnimator playerAnimator;

    void Start()
    {
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Calculate camera dimensions in world units
        cameraHeight = 2f * mainCamera.orthographicSize;
        cameraWidth = cameraHeight * mainCamera.aspect;

        // Start with camera at player position
        if (target != null)
        {
            transform.position = new Vector3(target.position.x, transform.position.y, transform.position.z);
            targetX = transform.position.x;
            lastPlayerX = target.position.x;
            playerAnimator = target.GetComponent<CharacterAnimator>();
        }
    }

    void LateUpdate()
    {
        if (target == null || parallax == null)
            return;

        // Check if player is moving
        bool isPlayerMoving = false;
        if (playerAnimator != null)
        {
            isPlayerMoving = Mathf.Abs(playerAnimator.currentVelocity.x) > 0.01f;
        }
        else
        {
            // Fallback if we can't access the animator
            isPlayerMoving = Mathf.Abs(target.position.x - lastPlayerX) > 0.01f;
        }

        // Get player position in screen space
        Vector3 screenPos = mainCamera.WorldToViewportPoint(target.position);

        // Calculate horizontal movement
        float edgeThreshold = 0.5f - (horizontalDeadzone / cameraWidth / 2);
        isAtEdge = false;
        float movementX = 0;

        // Player is approaching left edge
        if (screenPos.x < edgeThreshold)
        {
            isAtEdge = true;
            movementX = -1 * (edgeThreshold - screenPos.x) * cameraWidth;
        }
        // Player is approaching right edge
        else if (screenPos.x > (1 - edgeThreshold))
        {
            isAtEdge = true;
            movementX = (screenPos.x - (1 - edgeThreshold)) * cameraWidth;
        }

        // If player at edge AND player is moving, move camera and activate parallax
        if (isAtEdge && isPlayerMoving)
        {
            // Check level boundaries
            float newCameraX = transform.position.x + movementX;
            float leftCameraLimit = leftBoundary + (cameraWidth / 2);
            float rightCameraLimit = rightBoundary - (cameraWidth / 2);

            // Clamp camera position to level boundaries
            newCameraX = Mathf.Clamp(newCameraX, leftCameraLimit, rightCameraLimit);

            // Only move if we're not hitting a boundary
            if (Mathf.Abs(newCameraX - transform.position.x) > 0.01f)
            {
                targetX = newCameraX;
                Vector3 newPos = new Vector3(targetX, transform.position.y, transform.position.z);
                transform.position = Vector3.SmoothDamp(transform.position, newPos, ref velocity, 0.1f, cameraSpeed);

                // Apply parallax with the same direction as movement
                // We multiply by the camera speed to make it more responsive
                float parallaxDirection = movementX > 0 ? -1 : 1;
                parallax.MoveParallax(parallaxDirection * Mathf.Abs(movementX) * cameraSpeed);
            }
            else
            {
                // We've hit a boundary, stop parallax
                parallax.MoveParallax(0);
            }
        }
        else
        {
            // Player is in deadzone or not moving, stop parallax
            parallax.MoveParallax(0);
        }

        // Store player position for next frame
        lastPlayerX = target.position.x;
    }

    // Visualization for the deadzone in the Scene view
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        float halfWidth = horizontalDeadzone / 2;
        float halfHeight = verticalDeadzone / 2;
        Vector3 center = transform.position;

        // Draw deadzone rectangle
        Gizmos.DrawWireCube(center, new Vector3(horizontalDeadzone, verticalDeadzone, 0));

        // Draw full camera view
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, new Vector3(cameraWidth, cameraHeight, 0));

        // Draw level boundaries
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(leftBoundary, center.y + cameraHeight, 0), new Vector3(leftBoundary, center.y - cameraHeight, 0));
        Gizmos.DrawLine(new Vector3(rightBoundary, center.y + cameraHeight, 0), new Vector3(rightBoundary, center.y - cameraHeight, 0));
    }
}