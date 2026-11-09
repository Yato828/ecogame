using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wattshift.Editor
{
    public static class GateFourBuild
    {
        [Serializable] sealed class Summary
        {
            public string result, utc, unity, target = "StandaloneOSX", architecture = "ARM64", output, error;
            public double seconds;
            public ulong bytes;
            public int warnings, errors;
            public string[] scenes;
        }

        [MenuItem("WATTSHIFT/Build G4 macOS development")]
        public static void BuildMac()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string evidence = Path.Combine(project, "docs/evidence/g4");
            Directory.CreateDirectory(evidence);
            var summary = new Summary { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                output = Path.Combine(project, "Builds/G4/macOS/WATTSHIFT.app"), result = "Blocked" };
            var previous = UnityEditor.OSXStandalone.UserBuildSettings.architecture;
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                    throw new InvalidOperationException("Installed editor has no supported macOS build module.");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                    throw new InvalidOperationException("Active target must already be StandaloneOSX; this job does not switch platforms.");
                summary.scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (summary.scenes.Length != 1 || summary.scenes[0] != CitySliceBuilder.ScenePath)
                    throw new InvalidOperationException("Expected only the existing enabled CitySlice scene.");
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
                Directory.CreateDirectory(Path.GetDirectoryName(summary.output));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = summary.scenes, locationPathName = summary.output, target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.Development, subtarget = (int)StandaloneBuildSubtarget.Player
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
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = previous;
                File.WriteAllText(Path.Combine(evidence, "mac-build.json"), JsonUtility.ToJson(summary, true));
            }
            if (summary.result != BuildResult.Succeeded.ToString()) throw new InvalidOperationException("G4 macOS build did not succeed: " + summary.result);
            Debug.Log("WATTSHIFT_G4_MAC_BUILD: " + summary.output);
        }
    }
}
