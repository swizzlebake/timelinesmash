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
    /// Optional export path, compiled only when com.unity.recorder is installed. Registers itself
    /// with <see cref="RecorderBridge"/> on load so the composition inspector's Record button lights
    /// up. Arms a movie recorder (GameView + audio) against the stage scene; the user enters Play
    /// Mode to capture. Kept intentionally light per the MVP.
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

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.FrameRate = fps;
            settings.SetRecordModeToManual();

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = name;
            movie.Enabled = true;
            movie.ImageInputSettings = new GameViewInputSettings();
            movie.AudioInputSettings.PreserveAudio = true;
            movie.OutputFile = Path.Combine("Recordings", name);
            settings.AddRecorderSettings(movie);

            var controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();

            Debug.Log($"[TimelineSmash] Recorder armed for '{name}' (→ Recordings/{name}, {fps} fps). " +
                      "Enter Play Mode to capture the stage; recording stops on exit Play Mode.");
        }
    }
}
#endif
