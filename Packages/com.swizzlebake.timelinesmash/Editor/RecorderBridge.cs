using System;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// Decouples the core editor from the optional <c>com.unity.recorder</c> package. When the
    /// recorder integration assembly compiles (package present), its <c>[InitializeOnLoad]</c>
    /// registers <see cref="RecordAction"/>; otherwise the inspector shows install guidance. This
    /// keeps the core package compiling with or without Recorder, and avoids a hard dependency.
    /// </summary>
    public static class RecorderBridge
    {
        /// <summary>Set by the recorder integration assembly.
        /// Args: composition, masterPath, stagePath, duration (seconds).</summary>
        public static Action<CinematicComposition, string, string, double> RecordAction;

        public static bool Available => RecordAction != null;
    }
}
