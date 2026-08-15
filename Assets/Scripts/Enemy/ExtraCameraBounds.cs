using UnityEngine;

public static class ExtraCameraBounds
{
    public static Vector2 Clamp(Camera camera, Vector2 position, float padding, float worldZ = 0f)
    {
        GetMinMax(camera, worldZ, out Vector2 minimum, out Vector2 maximum);
        padding = Mathf.Max(0f, padding);

        minimum += Vector2.one * padding;
        maximum -= Vector2.one * padding;

        if (minimum.x > maximum.x)
        {
            minimum.x = maximum.x = (minimum.x + maximum.x) * 0.5f;
        }

        if (minimum.y > maximum.y)
        {
            minimum.y = maximum.y = (minimum.y + maximum.y) * 0.5f;
        }

        return new Vector2(
            Mathf.Clamp(position.x, minimum.x, maximum.x),
            Mathf.Clamp(position.y, minimum.y, maximum.y));
    }

    public static bool IsOutside(Camera camera, Vector2 position, float worldZ = 0f)
    {
        GetMinMax(camera, worldZ, out Vector2 minimum, out Vector2 maximum);
        return position.x < minimum.x || position.x > maximum.x
            || position.y < minimum.y || position.y > maximum.y;
    }

    private static void GetMinMax(
        Camera camera,
        float worldZ,
        out Vector2 minimum,
        out Vector2 maximum)
    {
        if (camera == null)
        {
            minimum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            maximum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            return;
        }

        float depth = Mathf.Abs(worldZ - camera.transform.position.z);
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));

        minimum = Vector2.Min(bottomLeft, topRight);
        maximum = Vector2.Max(bottomLeft, topRight);
    }
}
