using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ApexUrpLab.Editor
{
    public static class LabBuild
    {
        private const string ScenePath = "Assets/APEX/Scenes/ForestRidgeLab.unity";
        private const string Materials = "Assets/Resources/APEX/Materials";
        private const string Pipeline = "Assets/APEX/Generated/URP_Mobile.asset";
        private const string Renderer = "Assets/APEX/Generated/ForwardRenderer.asset";
        private const string PilotId = "id.apexracing.waterfront.urp.lab";

        [MenuItem("Tools/APEX/Prepare URP Migration Lab")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Materials);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(Path.GetDirectoryName(Pipeline));
            AssetDatabase.Refresh();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer);
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, Renderer);
            }
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
            if (!urp)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, Pipeline);
            }
            urp.supportsHDR = false;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            urp.renderScale = .80f;
            urp.msaaSampleCount = 1;
            urp.mainLightShadowmapResolution = 1024;
            urp.shadowCascadeCount = 1;
            urp.shadowDistance = 42f;
            urp.maxAdditionalLightsCount = 0;
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
            int selected = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(selected, false);

            // glTFast selects and caches its material generator during first import.
            // The shipped meshes stay outside Assets until URP is assigned and saved.
            AssetDatabase.SaveAssets();
            ImportGlbInputs();
            SetupMaterials();
            PlayerSettings.productName = "APEX Racing URP Lab";
            PlayerSettings.companyName = "APEX Racing";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PilotId);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                SetAndroid();
            if (!File.Exists(ScenePath))
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    throw new OperationCanceledException("Save or close the active scene before creating the lab.");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("APEX URP Lab").AddComponent<LabSession>();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode && SceneManager.GetActiveScene().path != ScenePath &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        private static void ImportGlbInputs()
        {
            const string sourceRoot = "SourceMeshes";
            const string assetRoot = "Assets/Resources/APEX";
            string[] inputs = Directory.GetFiles(sourceRoot, "*.glb", SearchOption.AllDirectories);
            if (inputs.Length != 9) throw new InvalidOperationException("The lab requires nine migration GLB inputs.");
            foreach (string source in inputs)
            {
                string relative = source.Substring(sourceRoot.Length + 1).Replace('\\', '/');
                string target = assetRoot + "/" + relative;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target)) File.Copy(source, target);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(target);
                if (!prefab || prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException("Could not import " + relative + " as a Unity prefab.");
            }
        }

        private static void SetAndroid()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.buildAppBundle = false;
        }

        private static Material Upsert(string name, Shader shader)
        {
            if (!shader) throw new InvalidOperationException("URP shader unavailable for " + name);
            string path = $"{Materials}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat)
            {
                mat.shader = shader;
                return mat;
            }
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Texture2D Scan(string name, bool srgb, bool isNormal = false)
        {
            string path = "Assets/Resources/APEX/Scans/" + name;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing " + path);
            var kind = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != kind || importer.sRGBTexture != srgb || !importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.textureType = kind;
                importer.sRGBTexture = srgb;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void SetupMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var road = Upsert("Road", lit);
            road.SetTexture("_BaseMap", Scan("RoadColor.jpg", true));
            road.SetTexture("_BumpMap", Scan("RoadNormal.png", false, true));
            road.SetTexture("_MetallicGlossMap", Scan("RoadMask.png", false));
            road.SetFloat("_Smoothness", .83f);
            road.SetFloat("_BumpScale", .65f);
            road.EnableKeyword("_NORMALMAP");
            road.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(road);

            foreach (var entry in new[] { (name: "Verge", scan: "VergeColor.jpg", tint: new Color(.88f, 1f, .78f)),
                (name: "Floor", scan: "FloorColor.jpg", tint: new Color(.87f, 1f, .73f)),
                (name: "Apron", scan: "ApronColor.jpg", tint: Color.white) })
            {
                var ground = Upsert(entry.name, lit);
                ground.SetTexture("_BaseMap", Scan(entry.scan, true));
                ground.SetColor("_BaseColor", entry.tint);
                ground.SetFloat("_Smoothness", .20f);
                EditorUtility.SetDirty(ground);
            }

            var stream = Shader.Find("APEX/URP Stream");
            var water = Upsert("Water", stream);
            water.SetFloat("_Fall", 0f);
            EditorUtility.SetDirty(water);
            var waterfall = Upsert("Falls", stream);
            waterfall.SetFloat("_Fall", 1f);
            waterfall.SetFloat("_Current", 2.5f);
            EditorUtility.SetDirty(waterfall);
        }

        [MenuItem("Tools/APEX/Build Lab APK")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Unity Android Build Support with SDK, NDK, and OpenJDK is required.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Could not activate the Android target. Start batchmode with -buildTarget Android.");
            Prepare();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/APEX-URP-Lab.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Android build result " + report.summary.result);
        }
    }
}
