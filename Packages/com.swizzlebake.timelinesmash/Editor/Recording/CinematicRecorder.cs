#if TIMELINESMASH_RECORDER
using System.IO;
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
    /// built-in H.264 ~4K ceiling. Enter Play Mode to capture; recording auto-stops at the last frame.
    /// (ProRes is macOS/Windows only, so on Linux the high-res path is image sequences.)
    /// </summary>
    [InitializeOnLoad]
    public static class CinematicRecorder
    {
        static CinematicRecorder()
        {
            RecorderBridge.RecordAction = Record;
        }

        public static void Record(CinematicComposition composition, string masterPath, string stagePath, double duration)
        {
            if (!File.Exists(stagePath))
            {
                Debug.LogError($"[TimelineSmash] Stage scene not found at '{stagePath}'. Assemble (master + stage) first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(stagePath, OpenSceneMode.Single);

            string name = composition != null && !string.IsNullOrEmpty(composition.cinematicName)
                ? composition.cinematicName
                : "Cinematic";
            float fps = (float)(composition != null && composition.settings != null && composition.settings.frameRate > 0
                ? composition.settings.frameRate
                : 30.0);
            var capture = composition != null && composition.capture != null ? composition.capture : new CaptureSettings();
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

            var controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();

            string ext = exr ? "EXR" : "PNG";
            string range = frames > 0 ? $"{frames} frames (auto-stops)" : "until you Stop (manual)";
            Debug.Log($"[TimelineSmash] Recorder armed for '{name}': {capture.width}x{capture.height} {ext} " +
                      $"sequence → Recordings/{name}/ at {fps} fps, {range}. Enter Play Mode to capture — " +
                      "ensure the capture camera is present in the open scene(s).");
        }
    }
}
#endif
