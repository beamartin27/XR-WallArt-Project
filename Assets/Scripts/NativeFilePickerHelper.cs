using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Cross-platform native file picker helper for selecting image files (.png, .jpg, .jpeg).
/// Uses Win32 GetOpenFileName with multi-select support on Windows (Editor and Standalone build).
/// </summary>
public static class NativeFilePickerHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class OpenFileName
    {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string filter = null;
        public string customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public IntPtr file = IntPtr.Zero;
        public int maxFile = 0;
        public string fileTitle = null;
        public int maxFileTitle = 0;
        public string initialDir = null;
        public string title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public string defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    private const int OFN_EXPLORER = 0x00080000;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_ALLOWMULTISELECT = 0x00000200;
    private const int OFN_NOCHANGEDIR = 0x00000008;

    /// <summary>
    /// Opens the native Windows file picker dialog allowing single or multiple image file selection.
    /// </summary>
    public static List<string> PickImageFiles(string dialogTitle = "Select Poster Images")
    {
        List<string> selectedFiles = new List<string>();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const int bufferSize = 65536;
        IntPtr fileBuffer = Marshal.AllocHGlobal(bufferSize * Marshal.SystemDefaultCharSize);

        // Zero out the buffer
        for (int i = 0; i < bufferSize; i++)
        {
            Marshal.WriteInt16(fileBuffer, i * 2, 0);
        }

        OpenFileName ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = "Image Files (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0All Files (*.*)\0*.*\0\0";
        ofn.file = fileBuffer;
        ofn.maxFile = bufferSize;
        ofn.title = dialogTitle;
        ofn.initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        ofn.flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_ALLOWMULTISELECT | OFN_NOCHANGEDIR;

        bool success = false;
        Thread staThread = new Thread(() =>
        {
            try
            {
                success = GetOpenFileName(ofn);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("NativeFilePickerHelper: Win32 dialog exception: " + ex.Message);
            }
        });
        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        if (success)
        {
            List<string> strings = new List<string>();
            int offset = 0;
            while (offset < bufferSize)
            {
                IntPtr ptr = new IntPtr(fileBuffer.ToInt64() + offset * Marshal.SystemDefaultCharSize);
                string s = Marshal.PtrToStringAuto(ptr);
                if (string.IsNullOrEmpty(s)) break;
                strings.Add(s);
                offset += s.Length + 1;
            }

            if (strings.Count == 1)
            {
                if (File.Exists(strings[0]))
                {
                    selectedFiles.Add(strings[0]);
                }
            }
            else if (strings.Count > 1)
            {
                string directory = strings[0];
                for (int i = 1; i < strings.Count; i++)
                {
                    string fullPath = Path.Combine(directory, strings[i]);
                    if (File.Exists(fullPath))
                    {
                        selectedFiles.Add(fullPath);
                    }
                }
            }
        }

        Marshal.FreeHGlobal(fileBuffer);
#elif UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Select Poster Image", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            selectedFiles.Add(path);
        }
#endif

        return selectedFiles;
    }
}
