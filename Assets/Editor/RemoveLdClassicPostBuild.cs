/*using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public static class RemoveLdClassicPostBuild
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        var pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var pbx = new PBXProject();
        pbx.ReadFromFile(pbxPath);

#if UNITY_2019_3_OR_NEWER
        string targetGuid = pbx.GetUnityFrameworkTargetGuid();
#else
        string targetGuid = pbx.TargetGuidByName("Unity-iPhone");
#endif

        // 获取当前 flags
        var flags = pbx.GetBuildPropertyForAnyConfig(targetGuid, "OTHER_LDFLAGS");

        if (flags != null && flags.Contains("-ld_classic"))
        {
            // 移除旧的 ld_classic
            pbx.UpdateBuildProperty(targetGuid, "OTHER_LDFLAGS", null, new string[] { "-ld_classic" });
            pbx.WriteToFile(pbxPath);
            // Debug.Log("✅ Removed '-ld_classic' from Xcode project automatically.");
        }
        else
        {
            // Debug.Log("ℹ️ No '-ld_classic' found; nothing to remove.");
        }
    }
}
*/