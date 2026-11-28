using System.Collections.Generic;
using UnityEngine;

internal static class RailwayAdaptiveClipping
{
    internal const float MinimumNear = .01f;

    internal static Vector2 ForBounds(Camera camera, Bounds focus, IReadOnlyList<Bounds> context,
        float distance, float defaultFar, Plane[] frustum)
    {
        GeometryUtility.CalculateFrustumPlanes(camera, frustum);
        var position = camera.transform.position;
        var forward = camera.transform.forward;
        var absoluteForward = Abs(forward);
        var tangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
        var aspect = Mathf.Max(.1f, camera.aspect);
        float minimumFrustumCosine = 1f / Mathf.Sqrt(1 + tangent * tangent * (1 + aspect * aspect));
        float nearLimit = float.PositiveInfinity;
        float farLimit = 0;

        Include(focus, true);
        foreach (var bounds in context)
        {
            bool onscreen = true;
            for (int side = 0; side < 4 && onscreen; side++)
            {
                var plane = frustum[side];
                onscreen = plane.GetDistanceToPoint(bounds.center) + Vector3.Dot(Abs(plane.normal), bounds.extents) >= 0;
            }
            Include(bounds, onscreen);
        }

        float wanted = Mathf.Clamp(distance * .012f, MinimumNear, 4f);
        float near = Mathf.Max(MinimumNear, Mathf.Min(wanted, nearLimit * .45f));
        float far = farLimit > 0 ? Mathf.Max(32, farLimit + Mathf.Max(16, farLimit * .07f)) : defaultFar;
        return new Vector2(near, Mathf.Max(near + 16, far));

        void Include(Bounds bounds, bool protectsNear)
        {
            // Protect loaded geometry even during a turn or a cross-zone focus.
            // The radial bound covers each corner independently of view direction.
            farLimit = Mathf.Max(farLimit, Vector3.Distance(bounds.center, position) + bounds.extents.magnitude);
            if (!protectsNear) return;
            float middle = Vector3.Dot(bounds.center - position, forward);
            float radius = Vector3.Dot(absoluteForward, bounds.extents);
            if (middle + radius <= 0) return;
            // An oblique AABB can extend behind the eye and still be far away
            // from every visible ray. Both bounds are conservative as viewed.
            float radialLowerBound = Mathf.Sqrt(bounds.SqrDistance(position)) * minimumFrustumCosine;
            nearLimit = Mathf.Min(nearLimit, Mathf.Max(radialLowerBound, middle - radius));
        }
    }

    internal static void Apply(Camera camera, Vector2 requested, float elapsed)
    {
        float blend = 1f - Mathf.Exp(-12 * Mathf.Max(0, elapsed));
        // Retreat immediately when approaching an object or when another object
        // enters the far range. Tighten the other direction progressively.
        float near = requested.x < camera.nearClipPlane ? requested.x : Mathf.Lerp(camera.nearClipPlane, requested.x, blend);
        float far = requested.y > camera.farClipPlane ? requested.y : Mathf.Lerp(camera.farClipPlane, requested.y, blend);
        if (Mathf.Abs(near - camera.nearClipPlane) > .0001f) camera.nearClipPlane = near;
        if (Mathf.Abs(far - camera.farClipPlane) > .05f) camera.farClipPlane = far;
    }

    static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
}
