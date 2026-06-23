using System.IO;
using UnityEditor.SceneManagement;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// High-level entry point that derives artifact paths from a composition and runs the
    /// master + stage build. Used by the inspectors and menu items. Tests call
    /// <see cref="CinematicAssembler"/> / <see cref="StageSceneBuilder"/> directly to avoid the
    /// scene-save prompt.
    /// </summary>
    public static class CinematicAssembleService
    {
        public static string OutputFolder(CinematicComposition c)
        {
            var folder = c != null && !string.IsNullOrEmpty(c.outputFolder)
                ? c.outputFolder
                : "Assets/Cinematics/Generated";
            return folder.Replace('\\', '/').TrimEnd('/');
        }

        public static string SafeName(CinematicComposition c)
        {
            var name = c != null && !string.IsNullOrEmpty(c.cinematicName) ? c.cinematicName : (c != null ? c.name : "Cinematic");
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name.Replace(' ', '_');
        }

        public static string MasterPath(CinematicComposition c) => $"{OutputFolder(c)}/{SafeName(c)}_Master.playable";
        public static string StagePath(CinematicComposition c) => $"{OutputFolder(c)}/{SafeName(c)}_Stage.unity";

        /// <summary>Build the master timeline and, optionally, the stage scene. Returns the result,
        /// or null if the user cancelled the scene-save prompt before the stage build.</summary>
        public static AssembleResult Assemble(CinematicComposition composition, bool buildStage)
        {
            if (composition == null)
                return null;

            var result = CinematicAssembler.BuildMaster(composition, MasterPath(composition));

            if (buildStage)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return result; // user cancelled; master is still generated

                StageSceneBuilder.BuildStage(result, composition.bindingManifest, StagePath(composition));
            }

            return result;
        }
    }
}
