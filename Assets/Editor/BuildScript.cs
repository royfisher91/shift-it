using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using KT.ShiftIt.Game;

namespace KT.ShiftIt.EditorTools
{
    /// <summary>
    /// Scene creation and the two builds, driven from the command line so a build is never a
    /// sequence of clicks somebody has to remember.
    ///
    /// Quality lesson D8: compile and launch the RELEASE build early and keep doing it. Both of
    /// these produce non-development builds for that reason.
    /// </summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string OutRoot = @"D:\KT\Builds\ShiftIt";

        // =====================================================================
        //  Scene
        // =====================================================================

        /// <summary>
        /// Builds the one scene the game has. It holds a single GameObject: everything else in the
        /// game is constructed at runtime by GameRoot, so there is no scene content to drift out of
        /// sync with the code and nothing to merge-conflict over.
        /// </summary>
        [MenuItem("SHIFT IT!/Create Game Scene")]
        public static void CreateGameScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("GameRoot");
            go.AddComponent<GameRoot>();

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[SHIFTIT] scene saved=" + ok + " -> " + ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SHIFTIT] build settings now list " + EditorBuildSettings.scenes.Length + " scene(s)");
        }

        // =====================================================================
        //  Shared player settings
        // =====================================================================

        static void CommonPlayerSettings()
        {
            PlayerSettings.productName = "SHIFT IT!";
            PlayerSettings.companyName = "KT Game Development";
            PlayerSettings.colorSpace = ColorSpace.Gamma;      // 2D cartoon: gamma is correct and cheaper
            PlayerSettings.runInBackground = false;            // audio must stop when unfocused
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.gcIncremental = true;
        }

        static string[] Scenes()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[SHIFTIT] " + ScenePath + " does not exist. Run Create Game Scene first.");
                return Array.Empty<string>();
            }
            return new[] { ScenePath };
        }

        // =====================================================================
        //  WebGL - the primary target (itch.io)
        // =====================================================================

        [MenuItem("SHIFT IT!/Build WebGL")]
        public static void BuildWebGL()
        {
            CommonPlayerSettings();

            // itch.io serves static files with no server-side header control, so Unity's
            // decompression fallback must be ON or a gzip-compressed build will not load.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.WebGL.memorySize = 256;

            string dir = Path.Combine(OutRoot, "WebGL");
            Directory.CreateDirectory(dir);
            Run(new BuildPlayerOptions
            {
                scenes = Scenes(),
                locationPathName = dir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,          // release, not development
            }, "WebGL");
        }

        // =====================================================================
        //  Windows - the second target
        // =====================================================================

        [MenuItem("SHIFT IT!/Build Windows")]
        public static void BuildWindows()
        {
            CommonPlayerSettings();
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);

            string dir = Path.Combine(OutRoot, "Windows");
            Directory.CreateDirectory(dir);
            Run(new BuildPlayerOptions
            {
                scenes = Scenes(),
                locationPathName = Path.Combine(dir, "ShiftIt.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            }, "Windows");
        }

        // =====================================================================

        static void Run(BuildPlayerOptions opts, string label)
        {
            if (opts.scenes == null || opts.scenes.Length == 0)
            {
                Debug.LogError("[SHIFTIT] BUILD " + label + " ABORTED: no scenes.");
                EditorApplication.Exit(3);
                return;
            }

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;

            // Report size per build so a WebGL payload cannot quietly balloon.
            Debug.Log(string.Format("[SHIFTIT] BUILD {0} result={1} errors={2} warnings={3} " +
                                    "size={4:0.0}MB time={5:0}s out={6}",
                label, s.result, s.totalErrors, s.totalWarnings,
                s.totalSize / (1024f * 1024f), s.totalTime.TotalSeconds, s.outputPath));

            if (s.result != BuildResult.Succeeded || s.totalErrors > 0)
            {
                Debug.LogError("[SHIFTIT] BUILD " + label + " FAILED.");
                EditorApplication.Exit(2);
                return;
            }
            Debug.Log("[SHIFTIT] BUILD " + label + " OK");
        }

        /// <summary>Scene plus both builds, for one command-line invocation.</summary>
        public static void BuildAll()
        {
            CreateGameScene();
            BuildWebGL();
            BuildWindows();
        }
    }
}
