using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>How a wall hit was found, from most to least certain.</summary>
public enum WallHitSource
{
    None,
    /// <summary>Inside the boundary of a detected vertical plane.</summary>
    DetectedPlane,
    /// <summary>On a detected vertical plane, but beyond the part ARKit/ARCore has mapped so far.</summary>
    ExtendedPlane,
    /// <summary>On a surface the device estimated from depth (LiDAR) or feature points; no plane exists yet.</summary>
    EstimatedSurface
}

public struct WallHit
{
    public bool valid;
    public Vector3 point;
    /// <summary>Points out of the wall, into the room.</summary>
    public Vector3 normal;
    /// <summary>The detected plane, or null for <see cref="WallHitSource.EstimatedSurface"/>.</summary>
    public GameObject plane;
    public WallHitSource source;
}

/// <summary>
/// Finds the wall under a screen position.
///
/// Plain plane detection only lets you place on the patch of wall the device has already mapped,
/// and on blank walls that patch can take a long time to appear. This combines three sources so
/// placement works sooner and on the whole wall:
///
///  1. Detected vertical planes (inside their boundary) - the most stable.
///  2. Estimated surfaces: an ARKit/ARCore raycast against the surface the device estimates under
///     the finger. On LiDAR iPhones this uses the depth sensor, so it works immediately, even on a
///     plain white wall. If the estimate lies on a wall we already detected, we snap to that wall.
///  3. Detected vertical planes extended past their boundary: a wall is bigger than the patch that
///     has been mapped, so once any part of it is known you can place anywhere on it.
/// </summary>
public static class WallDetection
{
    const float k_MaxRayDistance = 12f;
    /// <summary>
    /// How far past a detected wall patch we still treat the surface as the same wall, at most.
    /// A small patch is extended less (see <see cref="ExtensionLimit"/>): it may be a cupboard
    /// front or a pillar rather than a wall, and a wall's patch keeps growing as it is scanned.
    /// </summary>
    const float k_MaxExtension = 2.5f;
    /// <summary>Doors, windows and furniture are small: barely extend them.</summary>
    const float k_MaxExtensionNonWall = 0.3f;
    /// <summary>A wall's normal is horizontal; allow this much |y| for noise.</summary>
    const float k_MaxNormalY = 0.3f;
    /// <summary>Minimum cosine between the view ray and the wall normal (rejects grazing angles).</summary>
    const float k_MinFacing = 0.2f;
    const float k_CoplanarDistance = 0.10f;
    const float k_CoplanarAngle = 12f;

    const PlaneClassifications k_NotAWall =
        PlaneClassifications.DoorFrame | PlaneClassifications.WindowFrame |
        PlaneClassifications.Couch | PlaneClassifications.Seat | PlaneClassifications.Table |
        PlaneClassifications.Floor | PlaneClassifications.Ceiling;
    const PlaneClassifications k_DefinitelyAWall =
        PlaneClassifications.WallFace | PlaneClassifications.InvisibleWallFace;

    /// <summary>Largest dimension of the plane's mapped rectangle, in meters.</summary>
    static float LargestSide(ARPlane plane)
    {
        Vector2 half = plane.extents;
        return 2f * Mathf.Max(half.x, half.y);
    }

    /// <summary>How far past its mapped patch this plane may be extended.</summary>
    static float ExtensionLimit(ARPlane plane)
    {
        if ((plane.classifications & k_NotAWall) != 0) return k_MaxExtensionNonWall;
        return Mathf.Min(k_MaxExtension, 0.5f + 1.5f * LargestSide(plane));
    }

    static ARRaycastManager s_RaycastManager;
    static ARPlaneManager s_PlaneManager;
    static float s_NextManagerLookup;
    static readonly List<ARRaycastHit> s_ARHits = new List<ARRaycastHit>();
    static readonly RaycastHit[] s_PhysicsHits = new RaycastHit[32];

    static Vector3 s_SmoothedNormal;
    static Vector3 s_SmoothedPoint;
    static float s_SmoothedTime = -10f;

    /// <summary>Source of the most recent successful <see cref="Raycast"/> (for diagnostics).</summary>
    public static WallHitSource LastSource { get; private set; }

    /// <summary>Set to false to fall back to detected planes only.</summary>
    public static bool UseEstimatedSurfaces = true;

    public static ARPlaneManager PlaneManager
    {
        get
        {
            FindManagers();
            return s_PlaneManager;
        }
    }

    static void FindManagers()
    {
        if (s_PlaneManager != null && s_RaycastManager != null) return;
        if (Time.unscaledTime < s_NextManagerLookup) return;
        s_NextManagerLookup = Time.unscaledTime + 1f;
        if (s_PlaneManager == null) s_PlaneManager = Object.FindFirstObjectByType<ARPlaneManager>();
        if (s_RaycastManager == null) s_RaycastManager = Object.FindFirstObjectByType<ARRaycastManager>();
    }

    public static bool IsWall(ARPlane plane)
    {
        return plane != null
            && plane.gameObject.activeInHierarchy
            && plane.alignment == PlaneAlignment.Vertical
            && plane.subsumedBy == null
            && plane.trackingState != TrackingState.None;
    }

    /// <summary>Number of walls detected so far.</summary>
    public static int WallCount
    {
        get
        {
            FindManagers();
            if (s_PlaneManager == null) return 0;
            int count = 0;
            foreach (var plane in s_PlaneManager.trackables)
                if (IsWall(plane)) count++;
            return count;
        }
    }

    /// <summary>Finds the wall under <paramref name="screenPosition"/> (pixels).</summary>
    public static WallHit Raycast(Vector2 screenPosition, Camera camera = null)
    {
        if (camera == null) camera = Camera.main;
        if (camera == null) return default;
        FindManagers();

        Ray ray = camera.ScreenPointToRay(screenPosition);

        // 1. Inside a detected wall
        WallHit hit = RaycastDetectedPlanes(ray);

        // 2. Estimated surface under the finger
        if (!hit.valid && UseEstimatedSurfaces)
        {
            WallHit estimated = RaycastEstimatedSurface(screenPosition, ray);
            if (estimated.valid)
            {
                // Prefer the (more stable) detected wall when the estimate lies on one
                ARPlane wall = FindCoplanarWall(estimated.point, estimated.normal);
                if (wall != null)
                {
                    Vector3 n = wall.transform.up;
                    float d = Vector3.Dot(estimated.point - wall.transform.position, n);
                    hit = new WallHit
                    {
                        valid = true,
                        point = estimated.point - n * d,
                        normal = n,
                        plane = wall.gameObject,
                        source = WallHitSource.ExtendedPlane
                    };
                }
                else
                {
                    hit = estimated;
                }
            }
        }

        // 3. A detected wall, extended past the mapped patch
        if (!hit.valid) hit = RaycastExtendedPlanes(ray);

        if (hit.valid) LastSource = hit.source;
        return hit;
    }

    static WallHit RaycastDetectedPlanes(Ray ray)
    {
        int count = Physics.RaycastNonAlloc(ray, s_PhysicsHits, k_MaxRayDistance);
        ARPlane best = null;
        Vector3 bestPoint = default;
        float bestDistance = Mathf.Infinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = s_PhysicsHits[i];
            if (h.distance >= bestDistance) continue;
            ARPlane plane = h.collider.GetComponentInParent<ARPlane>();
            if (!IsWall(plane)) continue;
            best = plane;
            bestPoint = h.point;
            bestDistance = h.distance;
        }
        if (best == null) return default;
        return new WallHit
        {
            valid = true,
            point = bestPoint,
            normal = best.transform.up.normalized,
            plane = best.gameObject,
            source = WallHitSource.DetectedPlane
        };
    }

    static WallHit RaycastEstimatedSurface(Vector2 screenPosition, Ray ray)
    {
        if (s_RaycastManager == null || !s_RaycastManager.isActiveAndEnabled || s_RaycastManager.subsystem == null)
            return default;

        s_ARHits.Clear();
        if (!s_RaycastManager.Raycast(screenPosition, s_ARHits, TrackableType.PlaneEstimated))
            return default;

        // Hits are sorted closest-first; the closest one is the surface the user sees.
        foreach (ARRaycastHit arHit in s_ARHits)
        {
            Vector3 point = arHit.pose.position;
            Vector3 toPoint = point - ray.origin;
            if (toPoint.sqrMagnitude < 0.01f || toPoint.magnitude > k_MaxRayDistance) continue;

            // The native raycast works from the camera image; make sure it agrees with the ray
            // through the same pixel of our (possibly orientation-corrected) virtual camera.
            if (Vector3.Angle(ray.direction, toPoint) > 5f) continue;

            Vector3 normal = arHit.pose.up;
            if (Mathf.Abs(normal.y) > k_MaxNormalY) return default; // floor, table, sofa... not a wall

            normal.y = 0f; // walls are vertical: drop the noisy tilt component
            normal.Normalize();
            if (Vector3.Dot(normal, ray.direction) > 0f) normal = -normal; // face the viewer
            if (Vector3.Dot(normal, -ray.direction) < k_MinFacing) return default;

            // Estimates jitter from frame to frame; smooth the normal while the finger stays on one wall
            if (Time.unscaledTime - s_SmoothedTime < 0.5f
                && Vector3.Angle(s_SmoothedNormal, normal) < 20f
                && Mathf.Abs(Vector3.Dot(point - s_SmoothedPoint, s_SmoothedNormal)) < 0.15f)
            {
                normal = Vector3.Slerp(s_SmoothedNormal, normal, 0.2f).normalized;
            }
            s_SmoothedNormal = normal;
            s_SmoothedPoint = point;
            s_SmoothedTime = Time.unscaledTime;

            return new WallHit
            {
                valid = true,
                point = point,
                normal = normal,
                plane = null,
                source = WallHitSource.EstimatedSurface
            };
        }
        return default;
    }

    static WallHit RaycastExtendedPlanes(Ray ray)
    {
        if (s_PlaneManager == null) return default;

        ARPlane best = null;
        Vector3 bestPoint = default;
        float bestScore = Mathf.Infinity;

        foreach (ARPlane plane in s_PlaneManager.trackables)
        {
            if (!IsWall(plane)) continue;

            Vector3 n = plane.transform.up;
            float denominator = Vector3.Dot(n, ray.direction);
            if (denominator > -k_MinFacing) continue; // behind the wall, or looking along it

            float t = Vector3.Dot(plane.transform.position - ray.origin, n) / denominator;
            if (t < 0.1f || t > k_MaxRayDistance) continue;

            Vector3 point = ray.GetPoint(t);
            float outside = DistanceOutsideBounds(plane, point);
            if (outside > ExtensionLimit(plane)) continue;

            // Without depth we cannot know which candidate is really under the finger. Prefer the
            // one that needs the least stretching relative to its size (big planes are walls, small
            // ones are often furniture), then the one the device itself calls a wall, then the nearer.
            float score = outside / (0.5f + LargestSide(plane)) + 0.1f * t;
            if ((plane.classifications & k_DefinitelyAWall) != 0) score *= 0.5f;
            if (score < bestScore)
            {
                bestScore = score;
                best = plane;
                bestPoint = point;
            }
        }

        if (best == null) return default;
        return new WallHit
        {
            valid = true,
            point = bestPoint,
            normal = best.transform.up.normalized,
            plane = best.gameObject,
            source = WallHitSource.ExtendedPlane
        };
    }

    /// <summary>Distance (meters, along the wall) from a point to the plane's mapped rectangle. 0 if inside.</summary>
    public static float DistanceOutsideBounds(ARPlane plane, Vector3 worldPoint)
    {
        Vector3 local = plane.transform.InverseTransformPoint(worldPoint);
        Vector2 center = plane.centerInPlaneSpace;
        Vector2 half = plane.extents;
        float dx = Mathf.Max(0f, Mathf.Abs(local.x - center.x) - half.x);
        float dz = Mathf.Max(0f, Mathf.Abs(local.z - center.y) - half.y);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// Finds a detected wall that the given point lies on (same surface, same facing), or null.
    /// </summary>
    public static ARPlane FindCoplanarWall(Vector3 point, Vector3 normal, float maxDistance = k_CoplanarDistance)
    {
        FindManagers();
        if (s_PlaneManager == null) return null;

        ARPlane best = null;
        float bestScore = Mathf.Infinity;
        foreach (ARPlane plane in s_PlaneManager.trackables)
        {
            if (!IsWall(plane)) continue;
            Vector3 n = plane.transform.up;
            if (Vector3.Angle(n, normal) > k_CoplanarAngle) continue;
            float distance = Mathf.Abs(Vector3.Dot(point - plane.transform.position, n));
            if (distance > maxDistance) continue;
            float outside = DistanceOutsideBounds(plane, point);
            if (outside > k_MaxExtension) continue; // generous: here real depth already says a surface exists
            float score = distance + 0.05f * outside;
            if (score < bestScore)
            {
                bestScore = score;
                best = plane;
            }
        }
        return best;
    }
}
