using UnityEngine;
using System.Collections.Generic;

public class ParallaxScroller : MonoBehaviour
{
    public Transform[] originalLayers;
    public float parallaxSpeed = 2f;
    public float[] parallaxFactors = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };

    private float backgroundWidth;
    private List<(Transform layer, float speed)> layers = new List<(Transform, float)>();
    private float movementSpeed = 0f;
    private Transform cameraTransform;
    private Vector3 lastCameraPosition;

    void Start()
    {
        cameraTransform = Camera.main.transform;
        lastCameraPosition = cameraTransform.position;

        if (originalLayers.Length > 0)
        {
            SpriteRenderer spriteRenderer = originalLayers[0].GetComponent<SpriteRenderer>();
            if (spriteRenderer)
            {
                backgroundWidth = spriteRenderer.bounds.size.x;
                DuplicateLayers();
            }
        }
    }

    void DuplicateLayers()
    {
        layers.Clear();
        for (int i = 0; i < originalLayers.Length; i++)
        {
            if (i >= parallaxFactors.Length) break;

            Transform original = originalLayers[i];
            float speed = parallaxFactors[i];

            Transform leftClone = Instantiate(original, original.position + Vector3.left * backgroundWidth, Quaternion.identity, transform);
            Transform rightClone = Instantiate(original, original.position + Vector3.right * backgroundWidth, Quaternion.identity, transform);

            layers.Add((leftClone, speed));
            layers.Add((original, speed));
            layers.Add((rightClone, speed));
        }
    }

    public void MoveParallax(float speed)
    {
        movementSpeed = speed;
    }

    void LateUpdate()
    {
        if (Mathf.Abs(movementSpeed) > 0.01f)
        {
            // Apply the parallax effect with the specified movement speed
            ApplyParallaxMovement(movementSpeed);

            // Reset movement speed each frame - it needs to be continually set by the camera
            // This ensures parallax stops immediately when player stops moving
            movementSpeed = 0f;
        }
    }

    void ApplyParallaxMovement(float moveAmount)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            Transform layer = layers[i].layer;
            float speed = layers[i].speed;

            // Move the layer by the specified amount * parallax factor
            // When camera/player moves right, background moves left (negative)
            layer.position += new Vector3(moveAmount * speed * Time.deltaTime, 0, 0);

            // Handle wrapping around
            CheckAndRepositionLayer(layer);
        }
    }

    void CheckAndRepositionLayer(Transform layer)
    {
        float threshold = backgroundWidth * 0.5f;
        float cameraX = Camera.main.transform.position.x;

        // If layer is too far to the left of camera
        if (layer.position.x < cameraX - (backgroundWidth + threshold))
        {
            layer.position += Vector3.right * (backgroundWidth * 3);
        }
        // If layer is too far to the right of camera
        else if (layer.position.x > cameraX + (backgroundWidth + threshold))
        {
            layer.position -= Vector3.right * (backgroundWidth * 3);
        }
    }
}