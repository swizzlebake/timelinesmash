using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

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
        public static string BindingsPath(CinematicComposition c) => $"{OutputFolder(c)}/{SafeName(c)}_Bindings.asset";

        /// <summary>Project path of the scene cloned as the generated stage's base, or null when none is set
        /// (or the stored GUID no longer resolves).</summary>
        public static string StageSourceScenePath(CinematicComposition c)
        {
            if (c == null || string.IsNullOrEmpty(c.stageSourceSceneGuid))
                return null;
            var path = AssetDatabase.GUIDToAssetPath(c.stageSourceSceneGuid);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        /// <summary>Build the master timeline and, optionally, the stage scene. Returns the result,
        /// or null if the user cancelled the scene-save prompt before the stage build.</summary>
        public static AssembleResult Assemble(CinematicComposition composition, bool buildStage)
        {
            if (composition == null)
                return null;

            var result = CinematicAssembler.BuildMaster(composition, MasterPath(composition));

            // Compile the manifest tree into one master lookup and write the regenerable compiled asset.
            var compiled = BindingCompiler.Compile(composition.bindingManifest, result.warnings);
            BindingCompiler.WriteAsset(compiled, BindingsPath(composition));

            if (buildStage)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return result; // user cancelled; master is still generated

                // Populate the stage from the composition's source scene / actor prefab when set, so the
                // generated stage carries real actors and can be recorded on its own.
                StageSceneBuilder.BuildStage(result, compiled, StagePath(composition),
                    StageSourceScenePath(composition), composition.stageActorPrefab);
            }

            return result;
        }

        /// <summary>Build the master and wire the cinematic into the <b>currently open scene</b>, instead of
        /// regenerating an empty stage. The host directors + bindings land alongside whatever actors the
        /// scene already holds; manifest keys the manifest does not resolve fall back to scene GameObjects
        /// of that name, so a committed manifest can target live actors. Idempotent — re-running replaces
        /// the master it added rather than stacking a second one. The scene is left dirty for the user to
        /// save; their actors are never destroyed.</summary>
        public static AssembleResult AssembleIntoActiveScene(CinematicComposition composition)
        {
            if (composition == null)
                return null;

            var result = CinematicAssembler.BuildMaster(composition, MasterPath(composition));

            var compiled = BindingCompiler.Compile(composition.bindingManifest, result.warnings);
            BindingCompiler.WriteAsset(compiled, BindingsPath(composition));

            var scene = SceneManager.GetActiveScene();
            StageSceneBuilder.Populate(scene, result, compiled, resolveBySceneName: true);
            EditorSceneManager.MarkSceneDirty(scene);

            return result;
        }
    }
}
