using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Lets the user choose a picture from their photo library.
///
/// iOS: opens the system photo picker (see Assets/Plugins/iOS/WallArtNative.mm). The picker needs
/// no photo-library permission. The result is the path of a temporary JPEG, already rotated
/// upright and downscaled to <see cref="MaxPixelSize"/>.
/// Editor: opens a file dialog so the flow can be tested on a computer.
/// </summary>
public static class NativePhotoPicker
{
    /// <summary>Longest side of an imported picture, in pixels.</summary>
    public const int MaxPixelSize = 1600;

    static Action<string, string> s_Callback;

    public static bool IsSupported
    {
        get
        {
#if UNITY_EDITOR || UNITY_IOS
            return true;
#else
            return false;
#endif
        }
    }

    public static bool IsBusy => s_Callback != null;

    /// <summary>
    /// Opens the picker. <paramref name="onDone"/> receives (path, error): a path when a picture
    /// was chosen, an error message when something failed, and (null, null) when cancelled.
    /// </summary>
    public static void PickImage(Action<string, string> onDone)
    {
        if (s_Callback != null) return;

#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Choose a picture", "", "png,jpg,jpeg");
        onDone?.Invoke(string.IsNullOrEmpty(path) ? null : path, null);
#elif UNITY_IOS
        s_Callback = onDone;
        PhotoPickerReceiver.Ensure();
        WallArt_PickImage(PhotoPickerReceiver.ObjectName, nameof(PhotoPickerReceiver.OnImagePicked), MaxPixelSize);
#else
        onDone?.Invoke(null, "Adding your own pictures is only available on iPhone for now");
#endif
    }

    internal static void Complete(string message)
    {
        Action<string, string> callback = s_Callback;
        s_Callback = null;
        if (callback == null) return;

        if (string.IsNullOrEmpty(message)) callback(null, null);
        else if (message.StartsWith("error:")) callback(null, message.Substring("error:".Length));
        else callback(message, null);
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void WallArt_PickImage(string gameObjectName, string callbackName, int maxPixelSize);
#endif
}

/// <summary>Receives the native picker's UnitySendMessage callback.</summary>
public class PhotoPickerReceiver : MonoBehaviour
{
    public const string ObjectName = "WallArtPhotoPickerReceiver";
    static PhotoPickerReceiver s_Instance;

    public static void Ensure()
    {
        if (s_Instance != null) return;
        var go = new GameObject(ObjectName);
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<PhotoPickerReceiver>();
    }

    // Called from native code
    public void OnImagePicked(string message)
    {
        NativePhotoPicker.Complete(message);
    }
}

/// <summary>Short taps of haptic feedback (iPhone only; does nothing elsewhere).</summary>
public static class Haptics
{
    public static void Light() { Play(0); }
    public static void Medium() { Play(1); }
    public static void Success() { Play(2); }

    static void Play(int style)
    {
#if UNITY_IOS && !UNITY_EDITOR
        WallArt_Haptic(style);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void WallArt_Haptic(int style);
#endif
}
