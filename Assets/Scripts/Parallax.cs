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
        if (movementSpeed == 0) return;

        for (int i = 0; i < layers.Count; i++)
        {
            Transform layer = layers[i].layer;
            float speed = layers[i].speed;

            // Move the background only when character is near screen bounds
            layer.position -= new Vector3(movementSpeed * speed * Time.deltaTime, 0, 0);

            // Wrap around effect
            if (layer.position.x <= -backgroundWidth)
            {
                layer.position += Vector3.right * backgroundWidth * 3;
            }
            else if (layer.position.x >= backgroundWidth)
            {
                layer.position -= Vector3.right * backgroundWidth * 3;
            }
        }
    }
}
