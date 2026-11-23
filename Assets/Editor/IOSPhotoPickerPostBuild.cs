#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Links PhotosUI.framework into the Xcode project. The photo picker in
/// Assets/Plugins/iOS/WallArtNative.mm (PHPickerViewController) needs it.
/// </summary>
public static class IOSPhotoPickerPostBuild
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);

        // Plugins are compiled into the UnityFramework target
        string frameworkTarget = project.GetUnityFrameworkTargetGuid();
        project.AddFrameworkToProject(frameworkTarget, "PhotosUI.framework", false);

        project.WriteToFile(projectPath);
    }
}
#endif
