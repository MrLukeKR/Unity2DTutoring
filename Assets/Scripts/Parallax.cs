using UnityEngine;
using System.Collections.Generic;

public class ParallaxScroller : MonoBehaviour
{
    public Transform[] originalLayers; // 5 images assigned in the inspector
    public float parallaxSpeed = 2f; // Base speed
    public float[] parallaxFactors = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f }; // Different speeds per layer

    private float backgroundWidth; // Width of a single background
    private List<(Transform layer, float speed)> layers = new List<(Transform, float)>(); // Stores all layers with correct speed

    void Start()
    {
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
            Transform original = originalLayers[i];
            float speed = parallaxSpeed * parallaxFactors[i];

            Transform leftClone = Instantiate(original, original.position + Vector3.left * backgroundWidth, Quaternion.identity, transform);
            Transform rightClone = Instantiate(original, original.position + Vector3.right * backgroundWidth, Quaternion.identity, transform);

            // Add original and clones with the same speed
            layers.Add((leftClone, speed));
            layers.Add((original, speed));
            layers.Add((rightClone, speed));
        }
    }

    void Update()
    {
        for (int i = 0; i < layers.Count; i++)
        {
            Transform layer = layers[i].layer;
            float speed = layers[i].speed;

            layer.position += Vector3.left * speed * Time.deltaTime;

            if (layer.position.x <= -backgroundWidth)
            {
                layer.position += Vector3.right * backgroundWidth * 3; // Move to the rightmost position
            }
        }
    }
}
