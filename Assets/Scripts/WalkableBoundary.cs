
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class WalkableBoundary : MonoBehaviour
{
    private void OnEnable()
    {
        UpdateBoundary();
    }

    private void OnValidate()
    {
        UpdateBoundary();
    }

    private void UpdateBoundary()
    {
        SpriteRenderer spriteRenderer =
            GetComponent<SpriteRenderer>();

        EdgeCollider2D edge =
            GetComponent<EdgeCollider2D>();

        if (spriteRenderer == null ||
            spriteRenderer.sprite == null ||
            edge == null)
            return;

        Bounds bounds = spriteRenderer.sprite.bounds;

        Vector2[] normalizedPoints =
        {
            // Jalan bagian atas
            new Vector2(0.455f, 0.000f),
            new Vector2(0.544f, 0.000f),
            new Vector2(0.544f, 0.202f),

            // Sisi kanan lapangan
            new Vector2(0.735f, 0.202f),
            new Vector2(0.735f, 0.583f),

            // Jalan bagian bawah
            new Vector2(0.544f, 0.583f),
            new Vector2(0.544f, 1.000f),
            new Vector2(0.455f, 1.000f),

            // Sisi kiri lapangan
            new Vector2(0.455f, 0.583f),
            new Vector2(0.261f, 0.583f),
            new Vector2(0.261f, 0.202f),

            // Kembali ke jalan atas
            new Vector2(0.455f, 0.202f),
            new Vector2(0.455f, 0.000f)
        };

        Vector2[] points =
            new Vector2[normalizedPoints.Length];

        for (int i = 0; i < normalizedPoints.Length; i++)
        {
            float x = bounds.min.x +
                normalizedPoints[i].x * bounds.size.x;

            float y = bounds.max.y -
                normalizedPoints[i].y * bounds.size.y;

            points[i] = new Vector2(x, y);
        }

        edge.points = points;
        edge.edgeRadius = 0.025f;
    }
}
