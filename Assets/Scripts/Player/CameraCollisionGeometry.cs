using UnityEngine;

internal static class CameraCollisionGeometry
{
    public static bool PushSphereOutOfBox(Vector3 point, Vector3 halfSize, float radius,
        Vector3 preferredDirection, out Vector3 result)
    {
        Vector3 closest = new Vector3(
            Mathf.Clamp(point.x, -halfSize.x, halfSize.x),
            Mathf.Clamp(point.y, -halfSize.y, halfSize.y),
            Mathf.Clamp(point.z, -halfSize.z, halfSize.z));
        Vector3 delta = point - closest;
        float distance = delta.magnitude;
        result = point;
        if (distance >= radius) return false;
        if (distance > 0.000001f)
        {
            result = closest + delta * (radius / distance);
            return true;
        }

        float bestDistance = float.PositiveInfinity;
        float bestAlignment = float.NegativeInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            float sign = Mathf.Abs(point[axis]) > 0.000001f
                ? Mathf.Sign(point[axis]) : (preferredDirection[axis] < 0f ? -1f : 1f);
            float pushDistance = halfSize[axis] + radius - Mathf.Abs(point[axis]);
            float alignment = sign * preferredDirection[axis];
            if (pushDistance < bestDistance - 0.000001f ||
                (Mathf.Abs(pushDistance - bestDistance) <= 0.000001f && alignment > bestAlignment))
            {
                bestDistance = pushDistance;
                bestAlignment = alignment;
                result = point;
                result[axis] = sign * (halfSize[axis] + radius);
            }
        }
        return true;
    }

    public static bool SegmentEntersBox(Vector3 start, Vector3 end, Vector3 halfSize, out float entry)
    {
        entry = 0f;
        Vector3 direction = end - start;
        bool inside = true;
        bool strictlyInside = true;
        for (int axis = 0; axis < 3; axis++)
        {
            inside &= Mathf.Abs(start[axis]) <= halfSize[axis];
            strictlyInside &= Mathf.Abs(start[axis]) < halfSize[axis] - 0.000001f;
        }
        if (strictlyInside) return false;
        if (inside)
            for (int axis = 0; axis < 3; axis++)
                if (Mathf.Abs(start[axis]) >= halfSize[axis] - 0.000001f && start[axis] * direction[axis] >= 0f)
                    return false;

        float exit = 1f;
        for (int axis = 0; axis < 3; axis++)
        {
            if (Mathf.Abs(direction[axis]) < 0.000001f)
            {
                if (Mathf.Abs(start[axis]) > halfSize[axis]) return false;
                continue;
            }
            float a = (-halfSize[axis] - start[axis]) / direction[axis];
            float b = (halfSize[axis] - start[axis]) / direction[axis];
            entry = Mathf.Max(entry, Mathf.Min(a, b));
            exit = Mathf.Min(exit, Mathf.Max(a, b));
            if (entry > exit) return false;
        }
        return entry >= 0f && entry <= 1f;
    }
}
