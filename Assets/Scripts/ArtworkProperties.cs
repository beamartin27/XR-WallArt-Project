using UnityEngine;

public enum ArtworkOrientation
{
    Portrait = 0,
    Landscape = 1
}

/// <summary>
/// Metadata component attached to artwork prefabs to store size preset, real-world dimensions, and orientation.
/// </summary>
public class ArtworkProperties : MonoBehaviour
{
    [Tooltip("Current physical size preset of this artwork")]
    public ArtworkSizePreset currentPreset = ArtworkSizePreset.A3;

    [Tooltip("Current orientation preset (Portrait = 0 deg, Landscape = 90 deg roll)")]
    public ArtworkOrientation currentOrientation = ArtworkOrientation.Portrait;

    [Tooltip("Base dimension in meters at scale 1.0 (e.g. A3: 0.297m x 0.420m)")]
    public Vector2 baseDimensionsMeters = new Vector2(0.297f, 0.420f);

    [Tooltip("Human readable title of the artwork")]
    public string artworkTitle = "Wall Art";

    [Tooltip("Artist name")]
    public string artist = "Unknown";
}
