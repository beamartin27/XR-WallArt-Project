using UnityEngine;

public enum ArtworkOrientation
{
    Portrait = 0,
    Landscape = 1
}

public enum SizingSystem
{
    StandardA = 0,    // Standard poster aspect ratio (~1.414): A5, A4, A3, A2, A1
    GeneralScale = 1  // Non-standard aspect ratio (square, panorama, banner): Small, Medium, Big
}

public enum GeneralSizePreset
{
    Small = 0,
    Medium = 1,
    Big = 2
}

/// <summary>
/// Metadata component attached to artwork prefabs to store size preset, real-world dimensions, orientation, and sizing system.
/// </summary>
public class ArtworkProperties : MonoBehaviour
{
    [Tooltip("Current physical size preset of this artwork in the ISO A-series")]
    public ArtworkSizePreset currentPreset = ArtworkSizePreset.A3;

    [Tooltip("Current physical size preset of this artwork in the General scale system")]
    public GeneralSizePreset currentGeneralPreset = GeneralSizePreset.Medium;

    [Tooltip("Current orientation preset (Portrait = tall, Landscape = wide)")]
    public ArtworkOrientation currentOrientation = ArtworkOrientation.Portrait;

    [Tooltip("Base dimension in meters at scale 1.0 (e.g. A3: 0.297m x 0.420m)")]
    public Vector2 baseDimensionsMeters = new Vector2(0.297f, 0.420f);

    [Tooltip("Human readable title of the artwork")]
    public string artworkTitle = "Wall Art";

    [Tooltip("Artist name")]
    public string artist = "Unknown";

    /// <summary>
    /// True if the natural unrotated mesh dimensions have width > height.
    /// </summary>
    public bool IsNaturalLandscape => baseDimensionsMeters.x > baseDimensionsMeters.y;

    /// <summary>
    /// Determines whether this artwork's aspect ratio matches typical poster proportions (A system)
    /// or non-standard proportions (Small / Medium / Big system).
    /// Standard ISO 216 aspect ratio is sqrt(2) ~ 1.414. Standard posters range ~1.25 to 1.60.
    /// </summary>
    public SizingSystem ActiveSizingSystem
    {
        get
        {
            float w = baseDimensionsMeters.x;
            float h = baseDimensionsMeters.y;
            if (w <= 0.001f || h <= 0.001f) return SizingSystem.StandardA;
            float longer = Mathf.Max(w, h);
            float shorter = Mathf.Min(w, h);
            float ratio = longer / shorter;
            if (ratio >= 1.25f && ratio <= 1.60f)
            {
                return SizingSystem.StandardA;
            }
            return SizingSystem.GeneralScale;
        }
    }
}
