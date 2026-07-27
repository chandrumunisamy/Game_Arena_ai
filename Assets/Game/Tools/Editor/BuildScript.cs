using UnityEditor;
using UnityEditor.Build.Reporting;

using System;

namespace Relicfall.Tools
{
    /// <summary>
    /// Build script for creating Windows executable builds.
    /// Can be run from command line or from Unity's Build menu.
    /// </summary>
    public static class BuildScript
    {
        static string[] Scenes = new string[]
        {
            "Assets/Scenes/Hub.unity",
            "Assets/Scenes/GameRun.unity",
            "Assets/Scenes/BossArena.unity"
        };

        static string BuildPath = "Builds/Windows/RELICFALL";

        [MenuItem("Build/Build RELICFALL Windows")]
        public static void BuildWindows()
        {
            BuildPlayerOptions playerOptions = new BuildPlayerOptions();
            playerOptions.scenes = Scenes;
            playerOptions.target = BuildTarget.StandaloneWindows64;
            playerOptions.locationPathName = BuildPath;
            playerOptions.options = BuildOptions.None;

            BuildResult result = BuildPipeline.BuildPlayer(playerOptions);

            if (result.steps.Length == 0)
            {
                Debug.LogError("Build failed!");
                return;
            }

            Debug.Log($"Build succeeded: {BuildPath}");
            Debug.Log($"Total steps: {result.steps.Length}");
        }

        [MenuItem("Build/Validate RELICFALL Build")]
        public static void ValidateBuild()
        {
            // Check that all scenes exist
            foreach (var scene in Scenes)
            {
                if (!System.IO.File.Exists(scene))
                {
                    Debug.LogError($"Scene not found: {scene}");
                    return;
                }
            }

            Debug.Log("All scenes present. Build validation passed.");
        }
    }
}
