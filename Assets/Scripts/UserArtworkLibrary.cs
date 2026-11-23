using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The user's own pictures.
///
/// Adds an "Add photo" tile to the artwork drawer. A chosen picture is copied into the app's
/// storage (so it is still there next launch), gets a thumbnail in the drawer, and can be hung,
/// framed, resized and swapped exactly like the built-in artwork. The picture keeps its own
/// aspect ratio: at size "A3" its longest side is 42 cm.
/// </summary>
public class UserArtworkLibrary : MonoBehaviour
{
    public static UserArtworkLibrary Instance { get; private set; }

    /// <summary>Pictures are held uncompressed in memory, so keep the collection modest.</summary>
    public const int MaxPictures = 12;

    const string k_Folder = "UserArt";
    const float k_LongSide = 0.42f;       // long side of A3, meters
    const float k_MinShortSide = 0.12f;   // very long panoramas are cropped to this
    const float k_CanvasThickness = 0.014f;
    const float k_CanvasOffset = 0.010f;

    class Entry
    {
        public string path;
        public Texture2D texture;
        public Sprite sprite;
        public Material material;
        public GameObject template;
        public GameObject tile;
    }

    readonly List<Entry> m_Entries = new List<Entry>();
    ObjectListController m_Drawer;
    Transform m_Templates;
    GameObject m_TileSource;
    GameObject m_AddTile;
    Mesh m_CubeMesh;
    bool m_Ready;

    public int Count => m_Entries.Count;

    static string LibraryFolder => Path.Combine(Application.persistentDataPath, k_Folder);

    public static UserArtworkLibrary Ensure(ObjectListController drawer)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("UserArtworkLibrary");
        Instance = go.AddComponent<UserArtworkLibrary>();
        Instance.m_Drawer = drawer;
        return Instance;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    IEnumerator Start()
    {
        // Templates live under an inactive parent: they are never drawn, but clones made with
        // Instantiate (which are not parented here) come out active.
        var holder = new GameObject("Templates");
        holder.transform.SetParent(transform, false);
        holder.SetActive(false);
        m_Templates = holder.transform;

        // Let the drawer register the built-in tiles first
        yield return null;
        if (m_Drawer == null || m_Drawer.buttonsContent == null) yield break;

        Transform content = m_Drawer.buttonsContent.transform;
        for (int i = 0; i < content.childCount; i++)
        {
            UISelectionButton tile = content.GetChild(i).GetComponent<UISelectionButton>();
            if (tile == null) continue;
            if (m_TileSource == null) m_TileSource = tile.gameObject;
            if (m_CubeMesh == null && tile.targetPrefab != null)
            {
                Transform canvas = tile.targetPrefab.transform.Find("ArtworkCanvas");
                MeshFilter filter = canvas != null ? canvas.GetComponent<MeshFilter>() : null;
                if (filter != null) m_CubeMesh = filter.sharedMesh;
            }
        }

        BuildAddTile(content);
        m_Drawer.RegisterAllButtons();
        m_Ready = true;

        // Bring back the pictures added in earlier sessions, oldest first so the newest ends up in front
        string[] files = Array.Empty<string>();
        try
        {
            if (Directory.Exists(LibraryFolder)) files = Directory.GetFiles(LibraryFolder, "*.jpg");
        }
        catch (Exception e)
        {
            Debug.LogWarning("UserArtworkLibrary: could not read saved pictures: " + e.Message);
        }
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string file in files)
        {
            if (m_Entries.Count >= MaxPictures) break;
            Texture2D texture = LoadTexture(file);
            if (texture != null) AddEntry(texture, file);
            yield return null; // one picture per frame keeps startup smooth
        }
    }

    // ---------------------------------------------------------------- adding

    /// <summary>Opens the photo picker and adds the chosen picture.</summary>
    public void PickAndAdd()
    {
        if (!m_Ready || NativePhotoPicker.IsBusy) return;

        if (m_Entries.Count >= MaxPictures)
        {
            WallArtCoach.Toast("Remove a photo first (max " + MaxPictures + ")", true);
            return;
        }

        NativePhotoPicker.PickImage((path, error) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                WallArtCoach.Toast(error, true, 4f);
                return;
            }
            if (string.IsNullOrEmpty(path)) return; // cancelled

            bool isTemporary = path.StartsWith(Application.temporaryCachePath) || path.Contains("wallart_pick_");
            GameObject template = ImportFile(path);
            if (isTemporary)
            {
                try { File.Delete(path); } catch (Exception) { /* the OS clears temp files anyway */ }
            }

            if (template == null)
            {
                WallArtCoach.Toast("That picture could not be opened", true, 4f);
                return;
            }
            ArmForPlacement(template);
        });
    }

    /// <summary>Imports an image file (JPEG/PNG). Returns the artwork template, or null on failure.</summary>
    public GameObject ImportFile(string sourcePath)
    {
        Texture2D texture = LoadTexture(sourcePath);
        return texture != null ? ImportTexture(texture) : null;
    }

    /// <summary>Adds a picture from a readable texture. The library takes ownership of the texture.</summary>
    public GameObject ImportTexture(Texture2D texture)
    {
        if (texture == null) return null;
        texture = LimitSize(texture, NativePhotoPicker.MaxPixelSize);

        string path = null;
        try
        {
            Directory.CreateDirectory(LibraryFolder);
            path = Path.Combine(LibraryFolder, "photo_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".jpg");
            File.WriteAllBytes(path, texture.EncodeToJPG(90));
        }
        catch (Exception e)
        {
            // Still usable this session, just not remembered
            Debug.LogWarning("UserArtworkLibrary: could not save picture: " + e.Message);
            path = null;
        }

        Entry entry = AddEntry(texture, path);
        return entry != null ? entry.template : null;
    }

    static Texture2D LoadTexture(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!texture.LoadImage(bytes, false) || texture.width < 8 || texture.height < 8)
            {
                Destroy(texture);
                return null;
            }
            return texture;
        }
        catch (Exception e)
        {
            Debug.LogWarning("UserArtworkLibrary: could not load " + path + ": " + e.Message);
            return null;
        }
    }

    static Texture2D LimitSize(Texture2D source, int maxSize)
    {
        int longest = Mathf.Max(source.width, source.height);
        if (longest <= maxSize) return source;

        float factor = (float)maxSize / longest;
        int width = Mathf.Max(8, Mathf.RoundToInt(source.width * factor));
        int height = Mathf.Max(8, Mathf.RoundToInt(source.height * factor));

        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;
        var result = new Texture2D(width, height, TextureFormat.RGBA32, true);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply(true);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Destroy(source);
        return result;
    }

    Entry AddEntry(Texture2D texture, string path)
    {
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Trilinear;
        texture.anisoLevel = 4;
        texture.name = "UserPhoto";
        // Upload and drop the CPU copy: a photo is several megabytes we do not need twice
        texture.Apply(true, true);

        var entry = new Entry { path = path, texture = texture };
        entry.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect);
        entry.material = ArtMaterials.CreateLit(Color.white, texture, 0f, 0.15f);
        entry.material.name = "UserPhoto";
        entry.template = BuildTemplate(entry);
        entry.tile = BuildTile(entry);
        m_Entries.Add(entry);

        if (m_Drawer != null) m_Drawer.RegisterAllButtons();
        return entry;
    }

    // ---------------------------------------------------------------- artwork

    GameObject BuildTemplate(Entry entry)
    {
        Texture2D texture = entry.texture;
        float aspect = (float)texture.width / texture.height;

        float width, height;
        if (aspect >= 1f)
        {
            width = k_LongSide;
            height = Mathf.Max(k_MinShortSide, width / aspect);
        }
        else
        {
            height = k_LongSide;
            width = Mathf.Max(k_MinShortSide, height * aspect);
        }

        // If the short side was clamped (extreme panorama), crop instead of squashing the image
        float canvasAspect = width / height;
        Vector2 uvScale = Vector2.one;
        if (aspect > canvasAspect * 1.001f) uvScale.x = canvasAspect / aspect;
        else if (aspect < canvasAspect * 0.999f) uvScale.y = aspect / canvasAspect;
        entry.material.mainTextureScale = uvScale;
        entry.material.mainTextureOffset = (Vector2.one - uvScale) * 0.5f;

        var root = new GameObject("UserPhoto");
        root.transform.SetParent(m_Templates, false);
        root.tag = "ARObject";

        ARInteractable interactable = root.AddComponent<ARInteractable>();
        interactable.AnchorPointOfObject = Vector3.zero;
        interactable.NormalDirectionOfObject = Vector3.forward;
        interactable.objectName = "My Photo";

        ArtworkProperties properties = root.AddComponent<ArtworkProperties>();
        properties.currentPreset = ArtworkSizePreset.A3;
        properties.baseDimensionsMeters = new Vector2(width, height);
        properties.artworkTitle = "My Photo";
        properties.artist = "You";

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(width, height, k_CanvasOffset + k_CanvasThickness * 0.5f);
        box.center = new Vector3(0f, 0f, box.size.z * 0.5f);

        GameObject canvas;
        if (m_CubeMesh != null)
        {
            canvas = new GameObject("ArtworkCanvas");
            canvas.AddComponent<MeshFilter>().sharedMesh = m_CubeMesh;
            canvas.AddComponent<MeshRenderer>();
        }
        else
        {
            canvas = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canvas.name = "ArtworkCanvas";
            Collider primitiveCollider = canvas.GetComponent<Collider>();
            if (primitiveCollider != null) DestroyImmediate(primitiveCollider);
        }
        canvas.tag = "ARObject";
        canvas.transform.SetParent(root.transform, false);
        canvas.transform.localPosition = new Vector3(0f, 0f, k_CanvasOffset);
        canvas.transform.localScale = new Vector3(width, height, k_CanvasThickness);
        canvas.GetComponent<MeshRenderer>().sharedMaterial = entry.material;

        return root;
    }

    // ---------------------------------------------------------------- drawer tiles

    void BuildAddTile(Transform content)
    {
        float tileWidth = 360f;
        if (m_TileSource != null) tileWidth = ((RectTransform)m_TileSource.transform).sizeDelta.x;

        // An empty slot the size of an artwork tile, with the visible button inset inside it
        RectTransform slot = WallArtUI.CreateRect("Button_AddPhoto", content);
        slot.sizeDelta = new Vector2(tileWidth, 0f);
        slot.SetSiblingIndex(0);
        m_AddTile = slot.gameObject;

        Button button = WallArtUI.CreateButton("Card", slot, null, 0f, 36);
        WallArtUI.Stretch((RectTransform)button.transform, 16f, 16f, 8f, 8f);
        button.onClick.AddListener(PickAndAdd);

        RectTransform plus = WallArtUI.CreateCross(button.transform, 84f, 12f, WallArtUI.Accent, false);
        plus.anchoredPosition = new Vector2(0f, 34f);

        var label = WallArtUI.CreateLabel("Label", button.transform, "Add photo", 34f, WallArtUI.Text);
        WallArtUI.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(280f, 50f));
    }

    GameObject BuildTile(Entry entry)
    {
        if (m_TileSource == null || m_Drawer == null) return null;

        Transform content = m_Drawer.buttonsContent.transform;
        GameObject tile = Instantiate(m_TileSource, content);
        tile.name = "Button_UserPhoto";

        // Drop whatever the built-in tile carried (e.g. its title label)
        for (int i = tile.transform.childCount - 1; i >= 0; i--)
            Destroy(tile.transform.GetChild(i).gameObject);

        UISelectionButton selection = tile.GetComponent<UISelectionButton>();
        selection.targetPrefab = entry.template;
        selection.objectListController = m_Drawer;

        Image image = tile.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = entry.sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        // Newest first, right after the "Add photo" tile
        tile.transform.SetSiblingIndex(m_AddTile != null ? 1 : 0);

        // Small remove button in the corner
        Button remove = WallArtUI.CreateButton("Remove", tile.transform, null, 0f, 32);
        var removeRect = (RectTransform)remove.transform;
        WallArtUI.Place(removeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(68f, 68f));
        remove.targetGraphic.color = new Color(0f, 0f, 0f, 0.62f);
        WallArtUI.CreateCross(remove.transform, 30f, 6f, Color.white, true);
        remove.onClick.AddListener(() => Remove(entry));

        return tile;
    }

    void ArmForPlacement(GameObject template)
    {
        ScreenRayInteractor interactor = ScreenRayInteractor.Instance;
        if (interactor != null)
        {
            interactor.ClearSelection();
            interactor.SetTargetPrefab(template);
        }

        foreach (Entry entry in m_Entries)
        {
            if (entry.template != template || entry.tile == null) continue;
            if (m_Drawer != null) m_Drawer.currentSelectedIndex = entry.tile.transform.GetSiblingIndex();
        }

        // Scroll back to the start so the new picture is in view
        ScrollRect scroll = m_Drawer != null && m_Drawer.itemList != null ? m_Drawer.itemList.GetComponent<ScrollRect>() : null;
        if (scroll != null) scroll.horizontalNormalizedPosition = 0f;

        Haptics.Success();
        WallArtCoach.Toast("Photo added. Drag it onto your wall");
    }

    // ---------------------------------------------------------------- removing

    void Remove(Entry entry)
    {
        if (!m_Entries.Remove(entry)) return;

        ScreenRayInteractor interactor = ScreenRayInteractor.Instance;
        if (interactor != null && interactor.currentChosenPrefab == entry.template)
            interactor.SetTargetPrefab(null);

        if (!string.IsNullOrEmpty(entry.path))
        {
            try { File.Delete(entry.path); }
            catch (Exception e) { Debug.LogWarning("UserArtworkLibrary: could not delete picture: " + e.Message); }
        }

        if (entry.tile != null)
        {
            // Unparent first so the drawer's tile list is correct straight away
            entry.tile.transform.SetParent(null, false);
            Destroy(entry.tile);
        }
        if (entry.template != null) Destroy(entry.template);
        // The texture and material stay alive: pictures already on the wall still use them.

        if (m_Drawer != null)
        {
            m_Drawer.ClearSelectionHighlight();
            m_Drawer.RegisterAllButtons();
        }
        WallArtCoach.Toast("Photo removed");
    }
}
