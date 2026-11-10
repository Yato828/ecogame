using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wattshift.Editor
{
    public static class GateFiveBuild
    {
        [Serializable] sealed class Summary
        {
            public string result = "Blocked", utc, unity, target, architecture, output, error;
            public bool development;
            public double seconds;
            public ulong bytes;
            public int warnings, errors;
            public string[] scenes;
        }

        [MenuItem("WATTSHIFT/Build G5 macOS release")]
        public static void BuildMacRelease() => Build(BuildTarget.StandaloneOSX, false);
        [MenuItem("WATTSHIFT/Build G5 Web development")]
        public static void BuildWebDevelopment() => Build(BuildTarget.WebGL, true);
        [MenuItem("WATTSHIFT/Build G5 Web release")]
        public static void BuildWebRelease() => Build(BuildTarget.WebGL, false);

        static void Build(BuildTarget target, bool development)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool mac = target == BuildTarget.StandaloneOSX;
            string name = mac ? "mac-release" : development ? "web-development" : "web-release";
            string evidence = Path.Combine(project, "docs/evidence/g5", name + "-build.json");
            var summary = new Summary { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                target = target.ToString(), architecture = mac ? "ARM64" : "WebAssembly", development = development,
                output = Path.Combine(project, mac ? "Builds/G5/macOS/WATTSHIFT.app" :
                    development ? "Builds/G5/WebDevelopment" : "Builds/G5/WebRelease") };
            var previousArchitecture = UnityEditor.OSXStandalone.UserBuildSettings.architecture;
            string previousTemplate = null;
            var previousCompression = PlayerSettings.WebGL.compressionFormat;
            bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
            bool previousHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            bool previousCaching = PlayerSettings.WebGL.dataCaching;
            bool architectureChanged = false;
            try
            {
                var group = mac ? BuildTargetGroup.Standalone : BuildTargetGroup.WebGL;
                if (!BuildPipeline.IsBuildTargetSupported(group, target))
                    throw new InvalidOperationException("Installed editor lacks the " + target + " build module. No build attempted.");
                if (EditorUserBuildSettings.activeBuildTarget != target)
                    throw new InvalidOperationException("Active target must already be " + target + "; this job never switches targets.");
                summary.scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (summary.scenes.Length != 1 || summary.scenes[0] != CitySliceBuilder.ScenePath)
                    throw new InvalidOperationException("Expected only the existing enabled CitySlice scene.");
                if (mac)
                {
                    UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
                    architectureChanged = true;
                }
                else
                {
                    previousTemplate = PlayerSettings.WebGL.template;
                    PlayerSettings.WebGL.template = "PROJECT:WATTSHIFT";
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                    PlayerSettings.WebGL.decompressionFallback = false;
                    PlayerSettings.WebGL.nameFilesAsHashes = true;
                    PlayerSettings.WebGL.dataCaching = true;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(summary.output));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = summary.scenes, locationPathName = summary.output, target = target,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    subtarget = mac ? (int)StandaloneBuildSubtarget.Player : 0
                });
                summary.result = report.summary.result.ToString();
                summary.seconds = report.summary.totalTime.TotalSeconds;
                summary.bytes = report.summary.totalSize;
                summary.warnings = report.summary.totalWarnings;
                summary.errors = report.summary.totalErrors;
            }
            catch (Exception exception) { summary.error = exception.ToString(); Debug.LogError(summary.error); }
            finally
            {
                if (architectureChanged) UnityEditor.OSXStandalone.UserBuildSettings.architecture = previousArchitecture;
                if (previousTemplate != null)
                {
                    PlayerSettings.WebGL.template = previousTemplate;
                    PlayerSettings.WebGL.compressionFormat = previousCompression;
                    PlayerSettings.WebGL.decompressionFallback = previousFallback;
                    PlayerSettings.WebGL.nameFilesAsHashes = previousHashes;
                    PlayerSettings.WebGL.dataCaching = previousCaching;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(evidence));
                File.WriteAllText(evidence, JsonUtility.ToJson(summary, true));
            }
            if (summary.result != BuildResult.Succeeded.ToString())
                throw new InvalidOperationException("G5 build did not succeed: " + summary.result + ". See " + evidence);
            Debug.Log("WATTSHIFT_G5_BUILD: " + summary.output);
        }
    }
}
