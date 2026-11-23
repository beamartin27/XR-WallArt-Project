using System.Collections.Generic;
using UnityEngine;

public enum FrameStyle
{
    None = 0,
    ModernBlack = 1,
    GalleryWhite = 2,
    Walnut = 3,
    ClassicGold = 4
}

/// <summary>Describes one frame the user can choose. All sizes are in meters at artwork scale 1 (A3).</summary>
public class FrameStyleInfo
{
    public FrameStyle style;
    public string displayName;
    /// <summary>Width of the molding, across its face.</summary>
    public float moldingWidth;
    /// <summary>How far the front of the frame stands off the wall.</summary>
    public float depth;
    /// <summary>Width of the mat board between picture and frame. 0 = no mat.</summary>
    public float matWidth;
    public Color color;
    /// <summary>Use the generated wood-grain texture, tinted by <see cref="color"/>.</summary>
    public bool wood;
    public float metallic;
    public float smoothness;
    /// <summary>Stepped, ornamental profile instead of a flat one.</summary>
    public bool stepped;
    /// <summary>Colour shown for this frame in the picker UI.</summary>
    public Color swatch;
}

public static class FrameCatalog
{
    public static readonly FrameStyleInfo[] Styles =
    {
        new FrameStyleInfo
        {
            style = FrameStyle.None, displayName = "No frame",
            swatch = new Color(0f, 0f, 0f, 0f)
        },
        new FrameStyleInfo
        {
            style = FrameStyle.ModernBlack, displayName = "Black",
            moldingWidth = 0.014f, depth = 0.028f,
            color = new Color(0.035f, 0.035f, 0.04f), smoothness = 0.35f,
            swatch = new Color(0.08f, 0.08f, 0.09f)
        },
        new FrameStyleInfo
        {
            style = FrameStyle.GalleryWhite, displayName = "White + mat",
            moldingWidth = 0.018f, depth = 0.03f, matWidth = 0.04f,
            color = new Color(0.93f, 0.93f, 0.91f), smoothness = 0.3f,
            swatch = new Color(0.95f, 0.95f, 0.93f)
        },
        new FrameStyleInfo
        {
            style = FrameStyle.Walnut, displayName = "Walnut",
            moldingWidth = 0.026f, depth = 0.026f,
            color = Color.white, wood = true, smoothness = 0.25f,
            swatch = new Color(0.36f, 0.22f, 0.13f)
        },
        new FrameStyleInfo
        {
            style = FrameStyle.ClassicGold, displayName = "Gold",
            moldingWidth = 0.04f, depth = 0.034f, stepped = true,
            color = new Color(0.86f, 0.66f, 0.25f), metallic = 0.45f, smoothness = 0.5f,
            swatch = new Color(0.83f, 0.66f, 0.28f)
        }
    };

    public static FrameStyleInfo Get(FrameStyle style)
    {
        foreach (FrameStyleInfo info in Styles)
            if (info.style == style) return info;
        return Styles[0];
    }
}

/// <summary>
/// Creates lit materials at runtime by cloning a material the project already ships (so the
/// shader variant is guaranteed to be in the build) and changing its colour/texture.
/// </summary>
public static class ArtMaterials
{
    static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int k_MainTex = Shader.PropertyToID("_MainTex");
    static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int k_Color = Shader.PropertyToID("_Color");
    static readonly int k_Metallic = Shader.PropertyToID("_Metallic");
    static readonly int k_Smoothness = Shader.PropertyToID("_Smoothness");

    static Material s_Template;
    static Texture2D s_WoodTexture;
    static bool s_SearchedScene;

    /// <summary>A small walnut wood-grain texture, generated once. The grain runs along U.</summary>
    public static Texture WoodTexture
    {
        get
        {
            if (s_WoodTexture == null) s_WoodTexture = GenerateWoodGrain();
            return s_WoodTexture;
        }
    }

    static Texture2D GenerateWoodGrain()
    {
        const int width = 256, height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = "WalnutGrain (generated)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4
        };

        Color dark = new Color(0.22f, 0.12f, 0.065f);
        Color light = new Color(0.50f, 0.31f, 0.17f);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;
                // Grain lines run along U and wander a little so the wood does not look ruled.
                // Whole numbers of sine cycles keep the texture seamless along U.
                float wander = 0.035f * Mathf.Sin(u * Mathf.PI * 4f + v * 9f) + 0.02f * Mathf.Sin(u * Mathf.PI * 10f + 1.7f);
                float bands = Mathf.PerlinNoise(3.1f, (v + wander) * 22f);
                float streaks = Mathf.PerlinNoise(11.3f, (v + wander) * 90f);
                pixels[y * width + x] = Color.Lerp(dark, light, Mathf.Clamp01(0.65f * bands + 0.35f * streaks));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }

    /// <summary>Lets an artwork offer its own materials as templates (posters use URP Lit).</summary>
    public static void LearnFrom(Transform artworkRoot)
    {
        if (artworkRoot == null) return;

        if (s_Template == null)
        {
            Transform canvas = artworkRoot.Find("ArtworkCanvas");
            Renderer renderer = canvas != null ? canvas.GetComponent<Renderer>() : null;
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material != null && material.HasProperty(k_BaseMap)) s_Template = material;
        }
    }

    static void EnsureTemplates()
    {
        if (s_Template != null || s_SearchedScene) return;
        s_SearchedScene = true;

        // The artwork thumbnails reference the poster prefabs, whose materials make good templates
        foreach (UISelectionButton tile in Object.FindObjectsByType<UISelectionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tile.targetPrefab != null) LearnFrom(tile.targetPrefab.transform);
        }
    }

    public static Material CreateLit(Color color, Texture texture, float metallic, float smoothness)
    {
        EnsureTemplates();

        Material material;
        if (s_Template != null)
        {
            material = new Material(s_Template);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader);
        }

        if (material.HasProperty(k_BaseMap)) material.SetTexture(k_BaseMap, texture);
        if (material.HasProperty(k_MainTex)) material.SetTexture(k_MainTex, texture);
        if (material.HasProperty(k_BaseColor)) material.SetColor(k_BaseColor, color);
        if (material.HasProperty(k_Color)) material.SetColor(k_Color, color);
        if (material.HasProperty(k_Metallic)) material.SetFloat(k_Metallic, metallic);
        if (material.HasProperty(k_Smoothness)) material.SetFloat(k_Smoothness, smoothness);
        return material;
    }
}

/// <summary>
/// Builds a picture frame (and optional mat board) around an artwork's "ArtworkCanvas" child and
/// lets the style be swapped at runtime. Added automatically when an artwork is spawned.
///
/// The frame is generated as a mesh - a molding profile swept around the picture with mitred
/// corners - so it fits any picture size and aspect ratio, including the user's own photos.
/// </summary>
public class ArtworkFrame : MonoBehaviour
{
    /// <summary>Frame given to newly placed artwork. Follows the user's last choice.</summary>
    public static FrameStyle DefaultStyle = FrameStyle.Walnut;

    const string k_CanvasName = "ArtworkCanvas";
    const string k_LegacyFrameName = "OuterFrame";
    const float k_Rebate = 0.003f;       // how much the frame overlaps the picture edge
    const float k_MatOverlap = 0.004f;
    const float k_MatThickness = 0.0008f;

    static readonly Dictionary<FrameStyle, Material> s_MoldingMaterials = new Dictionary<FrameStyle, Material>();
    static Material s_MatMaterial;

    [SerializeField] FrameStyle m_Style = FrameStyle.Walnut;
    bool m_Applied;

    Transform m_Canvas;
    GameObject m_LegacyFrame;
    GameObject m_FrameObject;
    GameObject m_MatObject;
    Mesh m_FrameMesh;
    Mesh m_MatMesh;

    public FrameStyle Style => m_Style;

    /// <summary>False for artwork without a flat picture (e.g. the relief sculpture).</summary>
    public bool SupportsFrames
    {
        get
        {
            Resolve();
            return m_Canvas != null;
        }
    }

    public static ArtworkFrame GetOrAdd(GameObject artwork)
    {
        if (artwork == null) return null;
        ArtworkFrame frame = artwork.GetComponent<ArtworkFrame>();
        if (frame == null) frame = artwork.AddComponent<ArtworkFrame>();
        return frame;
    }

    /// <summary>True if this artwork has a flat picture that can be framed.</summary>
    public static bool CanFrame(GameObject artwork)
    {
        return artwork != null && artwork.transform.Find(k_CanvasName) != null;
    }

    void Resolve()
    {
        if (m_Canvas == null) m_Canvas = transform.Find(k_CanvasName);
        if (m_LegacyFrame == null)
        {
            Transform legacy = transform.Find(k_LegacyFrameName);
            if (legacy != null) m_LegacyFrame = legacy.gameObject;
        }
    }

    /// <summary>Applies the frame if none has been applied to this instance yet.</summary>
    public void EnsureApplied(FrameStyle fallbackStyle)
    {
        if (!m_Applied) Apply(fallbackStyle);
    }

    public void Apply(FrameStyle style)
    {
        Resolve();
        m_Style = style;
        m_Applied = true;
        if (m_Canvas == null) return;

        ArtMaterials.LearnFrom(transform);

        // The selection outline is an extra material on every renderer: take it off while we
        // change renderers and materials, then put it back.
        ARInteractable interactable = GetComponent<ARInteractable>();
        bool outlined = interactable != null && IsOutlined(interactable);
        if (outlined) interactable.SetSelfEmission(false);

        FrameStyleInfo info = FrameCatalog.Get(style);
        bool hasFrame = style != FrameStyle.None;
        bool hasMat = hasFrame && info.matWidth > 0f;

        // The prefab's flat backing plate is replaced by the generated frame
        if (m_LegacyFrame != null) m_LegacyFrame.SetActive(false);

        Vector3 canvasPos = m_Canvas.localPosition;
        Vector3 canvasSize = m_Canvas.localScale;
        float halfW = canvasSize.x * 0.5f;
        float halfH = canvasSize.y * 0.5f;
        Vector2 center = new Vector2(canvasPos.x, canvasPos.y);
        float pictureZ = canvasPos.z + canvasSize.z * 0.5f; // front surface of the picture
        float matTopZ = pictureZ + k_MatThickness;

        float outerHalfW = halfW;
        float outerHalfH = halfH;
        float frontZ = pictureZ;

        if (hasMat)
        {
            EnsureChild(ref m_MatObject, "Mat");
            if (m_MatMesh == null) m_MatMesh = new Mesh { name = "MatMesh" };
            var matProfile = new List<Vector2>
            {
                new Vector2(-k_MatOverlap, pictureZ - 0.0005f),
                new Vector2(-k_MatOverlap, matTopZ),
                new Vector2(info.matWidth + k_Rebate + 0.001f, matTopZ)
            };
            BuildRing(m_MatMesh, halfW, halfH, center, matProfile, 4f);
            m_MatObject.GetComponent<MeshFilter>().sharedMesh = m_MatMesh;
            m_MatObject.GetComponent<MeshRenderer>().sharedMaterial = GetMatMaterial();
            m_MatObject.SetActive(true);
        }
        else if (m_MatObject != null)
        {
            m_MatObject.SetActive(false);
        }

        if (hasFrame)
        {
            EnsureChild(ref m_FrameObject, "Frame");
            if (m_FrameMesh == null) m_FrameMesh = new Mesh { name = "FrameMesh" };

            float innerHalfW = halfW + info.matWidth;
            float innerHalfH = halfH + info.matWidth;
            float lipBaseZ = (hasMat ? matTopZ : pictureZ) - 0.0005f;
            float depth = Mathf.Max(info.depth, lipBaseZ + 0.008f);
            float width = info.moldingWidth;

            var profile = new List<Vector2>();
            if (info.stepped)
            {
                float lipZ = Mathf.Max(depth * 0.55f, lipBaseZ + 0.003f);
                profile.Add(new Vector2(-k_Rebate, lipBaseZ));
                profile.Add(new Vector2(-k_Rebate, lipZ));
                profile.Add(new Vector2(width * 0.18f, lipZ));
                profile.Add(new Vector2(width * 0.36f, depth));
                profile.Add(new Vector2(width * 0.72f, depth));
                profile.Add(new Vector2(width, depth * 0.6f));
                profile.Add(new Vector2(width, 0f));
            }
            else
            {
                const float chamfer = 0.002f;
                profile.Add(new Vector2(-k_Rebate, lipBaseZ));
                profile.Add(new Vector2(-k_Rebate, depth - chamfer));
                profile.Add(new Vector2(-k_Rebate + chamfer, depth));
                profile.Add(new Vector2(width - chamfer, depth));
                profile.Add(new Vector2(width, depth - chamfer));
                profile.Add(new Vector2(width, 0f));
            }

            BuildRing(m_FrameMesh, innerHalfW, innerHalfH, center, profile, 3f);
            m_FrameObject.GetComponent<MeshFilter>().sharedMesh = m_FrameMesh;
            m_FrameObject.GetComponent<MeshRenderer>().sharedMaterial = GetMoldingMaterial(info);
            m_FrameObject.SetActive(true);

            outerHalfW = innerHalfW + width;
            outerHalfH = innerHalfH + width;
            frontZ = depth;
        }
        else if (m_FrameObject != null)
        {
            m_FrameObject.SetActive(false);
        }

        // Keep the touch target in step with what is visible
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.size = new Vector3(outerHalfW * 2f, outerHalfH * 2f, frontZ);
            box.center = new Vector3(center.x, center.y, frontZ * 0.5f);
        }

        if (outlined) interactable.SetSelfEmission(true);
    }

    static bool IsOutlined(ARInteractable interactable)
    {
        ScreenRayInteractor interactor = ScreenRayInteractor.Instance;
        if (interactor == null) return false;
        return interactor.currentSelectedObject == interactable
            || (interactor.previewInstance != null && interactor.previewInstance == interactable.gameObject);
    }

    void EnsureChild(ref GameObject child, string childName)
    {
        if (child != null) return;
        child = new GameObject(childName);
        child.layer = gameObject.layer;
        child.tag = gameObject.tag;
        child.transform.SetParent(transform, false);
        child.AddComponent<MeshFilter>();
        child.AddComponent<MeshRenderer>();
    }

    void OnDestroy()
    {
        if (m_FrameMesh != null) Destroy(m_FrameMesh);
        if (m_MatMesh != null) Destroy(m_MatMesh);
    }

    static Material GetMoldingMaterial(FrameStyleInfo info)
    {
        if (s_MoldingMaterials.TryGetValue(info.style, out Material material) && material != null)
            return material;

        Texture texture = info.wood ? ArtMaterials.WoodTexture : null;
        material = ArtMaterials.CreateLit(info.color, texture, info.metallic, info.smoothness);
        material.name = "Frame_" + info.style;
        s_MoldingMaterials[info.style] = material;
        return material;
    }

    static Material GetMatMaterial()
    {
        if (s_MatMaterial == null)
        {
            s_MatMaterial = ArtMaterials.CreateLit(new Color(0.96f, 0.95f, 0.92f), null, 0f, 0.08f);
            s_MatMaterial.name = "Frame_MatBoard";
        }
        return s_MatMaterial;
    }

    /// <summary>
    /// Sweeps a profile around a rectangle with mitred corners.
    /// Profile points are (distance outward from the rectangle's edge, height off the wall),
    /// listed from the inner edge over the front to the outer edge.
    /// </summary>
    public static void BuildRing(Mesh mesh, float halfW, float halfH, Vector2 center, IList<Vector2> profile, float uvPerMeter)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        // Corners, counter-clockwise seen from the front: top-right, top-left, bottom-left, bottom-right
        Vector2[] cornerSigns = { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        // Outward direction of the side that starts at each corner: top, left, bottom, right
        Vector2[] sideOutward = { new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0) };

        for (int side = 0; side < 4; side++)
        {
            Vector2 signA = cornerSigns[side];
            Vector2 signB = cornerSigns[(side + 1) % 4];
            Vector2 outward = sideOutward[side];
            Vector3 along = new Vector3(signB.x - signA.x, signB.y - signA.y, 0f).normalized;

            float profileLength = 0f;
            for (int j = 0; j < profile.Count - 1; j++)
            {
                Vector2 p0 = profile[j];
                Vector2 p1 = profile[j + 1];
                Vector2 delta = p1 - p0;
                float segmentLength = delta.magnitude;
                if (segmentLength < 1e-6f) continue;

                Vector2 n2 = new Vector2(-delta.y, delta.x) / segmentLength;
                Vector3 normal = new Vector3(outward.x * n2.x, outward.y * n2.x, n2.y);

                Vector3 a0 = Corner(center, halfW, halfH, signA, p0);
                Vector3 a1 = Corner(center, halfW, halfH, signA, p1);
                Vector3 b0 = Corner(center, halfW, halfH, signB, p0);
                Vector3 b1 = Corner(center, halfW, halfH, signB, p1);

                int start = vertices.Count;
                vertices.Add(a0); vertices.Add(a1); vertices.Add(b1); vertices.Add(b0);
                for (int k = 0; k < 4; k++) normals.Add(normal);

                float v0 = profileLength * uvPerMeter;
                float v1 = (profileLength + segmentLength) * uvPerMeter;
                uvs.Add(new Vector2(Vector3.Dot(a0, along) * uvPerMeter, v0));
                uvs.Add(new Vector2(Vector3.Dot(a1, along) * uvPerMeter, v1));
                uvs.Add(new Vector2(Vector3.Dot(b1, along) * uvPerMeter, v1));
                uvs.Add(new Vector2(Vector3.Dot(b0, along) * uvPerMeter, v0));

                // Unity front faces wind clockwise: cross(v1 - v0, v2 - v0) points along the normal
                bool flip = Vector3.Dot(Vector3.Cross(a1 - a0, b1 - a0), normal) < 0f;
                if (!flip)
                {
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
                else
                {
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                    triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
                }

                profileLength += segmentLength;
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    static Vector3 Corner(Vector2 center, float halfW, float halfH, Vector2 sign, Vector2 profilePoint)
    {
        return new Vector3(
            center.x + sign.x * (halfW + profilePoint.x),
            center.y + sign.y * (halfH + profilePoint.x),
            profilePoint.y);
    }
}
