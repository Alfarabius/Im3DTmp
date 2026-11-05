using System.Collections.Generic;
using UnityEngine;

public static class DisplayResolutions
{
    public static Vector2Int[] GetSizes(Resolution[] modes, Vector2Int currentSize)
    {
        var sizes = new List<Vector2Int>();
        var unique = new HashSet<Vector2Int>();
        if (modes != null)
        {
            foreach (var mode in modes)
            {
                if (mode.width <= 0 || mode.height <= 0) continue;
                if (System.Math.Abs((double)mode.width / mode.height - 16.0 / 9.0) >= 0.05) continue;
                var size = new Vector2Int(mode.width, mode.height);
                if (unique.Add(size)) sizes.Add(size);
            }
        }
        if (currentSize.x > 0 && currentSize.y > 0 && unique.Add(currentSize)) sizes.Add(currentSize);
        sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return sizes.ToArray();
    }

    public static int IndexOf(Vector2Int[] sizes, int width, int height)
    {
        if (sizes == null) return -1;
        for (int i = 0; i < sizes.Length; i++)
            if (sizes[i].x == width && sizes[i].y == height) return i;
        return -1;
    }
}
