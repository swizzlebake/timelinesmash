#if TIMELINESMASH_RECORDER
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimelineSmash.Editor.Recording
{
    /// <summary>
    /// Optional export path, compiled only when com.unity.recorder is installed. Registers itself with
    /// <see cref="RecorderBridge"/> so the composition inspector's Record button lights up. Outputs a
    /// HIGH-RESOLUTION image sequence (PNG/EXR) by rendering a chosen camera through
    /// <c>CameraInputSettings</c> at an arbitrary resolution — it is NOT limited to the Game View or the
    /// built-in H.264 ~4K ceiling. (ProRes is macOS/Windows only, so on Linux the high-res path is image
    /// sequences.)
    ///
    /// The Recorder's scripted API requires <c>PrepareRecording</c>/<c>StartRecording</c> to run in Play
    /// Mode, while opening the stage scene must happen in Edit Mode — so Record() arms (opens the stage,
    /// remembers which composition to record) and enters Play Mode, and recording actually starts in the
    /// <see cref="PlayModeStateChange.EnteredPlayMode"/> callback. It auto-stops at the last frame and
    /// leaves Play Mode; a manual-mode capture stops when you exit Play Mode yourself.
    /// </summary>
    [InitializeOnLoad]
    public static class CinematicRecorder
    {
        const string PendingKey = "TimelineSmash.Record.Pending";
        const string CompGuidKey = "TimelineSmash.Record.CompGuid";

        static RecorderController s_Controller;
        static bool s_Started;

        static CinematicRecorder()
        {
            RecorderBridge.RecordAction = Record;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // Edit-mode entry point: open the stage, remember the composition, enter Play Mode. Recording is
        // started from OnPlayModeChanged(EnteredPlayMode) because the scripted Recorder API is Play-Mode-only.
        public static void Record(CinematicComposition composition, string masterPath, string stagePath, double duration)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TimelineSmash] Exit Play Mode before recording — Record enters Play Mode for you.");
                return;
            }
            if (composition == null)
            {
                Debug.LogError("[TimelineSmash] No composition to record.");
                return;
            }
            if (!System.IO.File.Exists(stagePath))
            {
                Debug.LogError($"[TimelineSmash] Stage scene not found at '{stagePath}'. Assemble (master + stage) first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(stagePath, OpenSceneMode.Single);

            SessionState.SetString(CompGuidKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(composition)));
            SessionState.SetBool(PendingKey, true);

            Debug.Log($"[TimelineSmash] Entering Play Mode to record '{composition.cinematicName}'…");
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.EraseBool(PendingKey);
                BeginRecording();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && s_Controller != null)
            {
                if (s_Controller.IsRecording())
                    s_Controller.StopRecording();
                Cleanup();
            }
        }

        static void BeginRecording()
        {
            var path = AssetDatabase.GUIDToAssetPath(SessionState.GetString(CompGuidKey, ""));
            var composition = AssetDatabase.LoadAssetAtPath<CinematicComposition>(path);
            if (composition == null)
            {
                Debug.LogError("[TimelineSmash] Could not reload the composition to record.");
                return;
            }

            var settings = BuildSettings(composition, out string summary);
            s_Controller = new RecorderController(settings);
            s_Controller.PrepareRecording();
            s_Controller.StartRecording();
            s_Started = false;
            EditorApplication.update += Update;

            Debug.Log($"[TimelineSmash] Recording '{composition.cinematicName}' — {summary}.");
        }

        static RecorderControllerSettings BuildSettings(CinematicComposition composition, out string summary)
        {
            string name = !string.IsNullOrEmpty(composition.cinematicName) ? composition.cinematicName : "Cinematic";
            float fps = (float)(composition.settings != null && composition.settings.frameRate > 0
                ? composition.settings.frameRate : 30.0);
            double duration = composition.settings != null ? composition.settings.totalDuration : 0;
            var capture = composition.capture ?? new CaptureSettings();
            int frames = duration > 0 ? Mathf.Max(1, Mathf.CeilToInt((float)duration * fps)) : 0;
            bool exr = capture.format == CaptureImageFormat.EXR;

            var img = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            img.name = name;
            img.Enabled = true;
            img.OutputFormat = exr
                ? ImageRecorderSettings.ImageRecorderOutputFormat.EXR
                : ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            img.OutputColorSpace = exr
                ? ImageRecorderSettings.ColorSpaceType.Unclamped_linear_sRGB  // linear HDR for grading
                : ImageRecorderSettings.ColorSpaceType.sRGB_sRGB;             // tonemapped, ready to encode
            if (exr)
                img.EXRCompression = CompressionUtility.EXRCompressionType.Zip;
            img.CaptureAlpha = false;

            string tag = capture.cameraTag;
            img.imageInputSettings = new CameraInputSettings
            {
                Source = string.IsNullOrEmpty(tag) ? ImageSource.ActiveCamera
                    : tag == "MainCamera" ? ImageSource.MainCamera
                    : ImageSource.TaggedCamera,
                CameraTag = tag,
                OutputWidth = Mathf.Max(2, capture.width),
                OutputHeight = Mathf.Max(2, capture.height),
                RecordTransparency = false,
                FlipFinalOutput = false,
            };
            img.OutputFile = $"Recordings/{name}/{name}";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.FrameRate = fps;
            settings.CapFrameRate = true;
            if (frames > 0)
                settings.SetRecordModeToFrameInterval(0, frames - 1);
            else
                settings.SetRecordModeToManual();
            settings.AddRecorderSettings(img);

            string ext = exr ? "EXR" : "PNG";
            summary = frames > 0
                ? $"{capture.width}x{capture.height} {ext} → Recordings/{name}/ at {fps} fps, {frames} frames (auto-stops)"
                : $"{capture.width}x{capture.height} {ext} → Recordings/{name}/ at {fps} fps (manual — exit Play Mode to stop)";
            return settings;
        }

        static void Update()
        {
            if (s_Controller == null)
            {
                EditorApplication.update -= Update;
                return;
            }
            if (s_Controller.IsRecording())
            {
                s_Started = true;
                return;
            }
            if (!s_Started)
                return; // give it a frame to spin up

            Debug.Log("[TimelineSmash] Recording complete.");
            Cleanup();
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }

        static void Cleanup()
        {
            EditorApplication.update -= Update;
            s_Controller = null;
            s_Started = false;
        }
    }
}
#endif
