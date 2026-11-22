using UnityEditor;
using UnityEditor.Build;

namespace ApexUrpLab.Editor
{
    public static class ApexCloudBuild
    {
        // Public parameterless method for Unity Build Automation's pre-export hook.
        // The hosted worker exports and signs the APK after this method returns.
        public static void PreExport()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("The APEX cloud build target must be Android.");

            LabBuild.Prepare();
        }
    }
}
