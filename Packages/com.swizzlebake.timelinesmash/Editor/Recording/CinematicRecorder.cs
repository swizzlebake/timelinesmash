#if TIMELINESMASH_RECORDER
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;

namespace TimelineSmash.Editor.Recording
{
    /// <summary>
    /// Optional export path, compiled only when com.unity.recorder is installed. Registers itself with
    /// <see cref="RecorderBridge"/> so the composition inspector's Record button lights up. Renders a chosen
    /// camera through <c>CameraInputSettings</c> at an arbitrary resolution — NOT limited to the Game View.
    /// Per <see cref="CaptureSettings.output"/> it writes a HIGH-RESOLUTION image sequence (PNG/EXR) and/or,
    /// on macOS/Windows, encodes a ProRes 422 HQ <c>.mov</c> as part of the same record pass. ProRes (not
    /// H.264, which is capped at 4K) carries the 4K/6K/8K masters this tool targets. The image sequence is the
    /// cross-platform master; on Linux (no platform ProRes encoder) a video request degrades to it. The record
    /// length comes from <see cref="AssembleSettings.totalDuration"/>, or is auto-filled from the assembled
    /// master timeline's duration when that is 0.
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

            // Length = the composition's Total Duration, or — when it's left at 0 — auto-filled from the
            // assembled master timeline's own duration (the latest segment's end), so a full sequence is
            // captured without typing the length. Still 0 (no/empty master) falls back to manual record mode.
            double duration = composition.settings != null ? composition.settings.totalDuration : 0;
            if (duration <= 0)
                duration = MasterDuration(composition);

            var capture = composition.capture ?? new CaptureSettings();
            int frames = duration > 0 ? Mathf.Max(1, Mathf.CeilToInt((float)duration * fps)) : 0;
            bool exr = capture.format == CaptureImageFormat.EXR;

            // A video (ProRes .mov) is encoded only where the platform ProRes codec exists — macOS/Windows.
            // ProRes (not H.264) because H.264 is capped at 4K, but this tool targets 4K/6K/8K masters. On
            // Linux a video request degrades to the image sequence; if video-only was asked we still emit
            // frames, so Record never produces nothing.
            bool videoSupported = Application.platform == RuntimePlatform.OSXEditor
                                  || Application.platform == RuntimePlatform.WindowsEditor;
            bool wantVideo = capture.output != CaptureOutput.ImageSequence;
            bool wantImages = capture.output != CaptureOutput.Video;
            bool videoSkipped = wantVideo && !videoSupported;
            if (videoSkipped)
            {
                wantVideo = false;
                wantImages = true;
            }

            CameraInputSettings MakeCameraInput()
            {
                string tag = capture.cameraTag;
                return new CameraInputSettings
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
            }

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.FrameRate = fps;
            settings.CapFrameRate = true;
            if (frames > 0)
                settings.SetRecordModeToFrameInterval(0, frames - 1);
            else
                settings.SetRecordModeToManual();

            if (wantImages)
            {
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
                img.imageInputSettings = MakeCameraInput();
                img.OutputFile = $"Recordings/{name}/{name}";
                settings.AddRecorderSettings(img);
            }

            if (wantVideo)
            {
                var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name = name;
                movie.Enabled = true;
                // ProRes 422 HQ: high-quality intermediate with no 4K ceiling (unlike H.264), broadly
                // compatible, no alpha. macOS uses Apple's codec; on Windows all ProRes formats are available.
                movie.EncoderSettings = new ProResEncoderSettings
                {
                    Format = ProResEncoderSettings.OutputFormat.ProRes422HQ,
                };
                movie.ImageInputSettings = MakeCameraInput();
                if (movie.AudioInputSettings != null)
                    movie.AudioInputSettings.PreserveAudio = true;               // include any Timeline audio (stereo)
                movie.OutputFile = $"Recordings/{name}/{name}";
                settings.AddRecorderSettings(movie);
            }

            string ext = exr ? "EXR" : "PNG";
            var parts = new System.Collections.Generic.List<string>();
            if (wantImages) parts.Add($"{capture.width}x{capture.height} {ext} sequence");
            if (wantVideo) parts.Add($"{capture.width}x{capture.height} ProRes 422 HQ .mov");
            string mode = frames > 0
                ? $"at {fps} fps, {frames} frames (auto-stops)"
                : $"at {fps} fps (manual — exit Play Mode to stop)";
            string videoNote = videoSkipped ? " — video skipped: ProRes needs macOS/Windows" : "";
            summary = $"{string.Join(" + ", parts)} → Recordings/{name}/ {mode}{videoNote}";
            return settings;
        }

        // The assembled master timeline's length in seconds (latest segment end), used to auto-fill the record
        // range when the composition's Total Duration is left at 0.
        static double MasterDuration(CinematicComposition composition)
        {
            var master = AssetDatabase.LoadAssetAtPath<TimelineAsset>(
                CinematicAssembleService.MasterPath(composition));
            return master != null ? master.duration : 0;
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
