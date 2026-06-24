using System;
using UnityEngine;

namespace TimelineSmash
{
    public enum CaptureImageFormat
    {
        PNG,
        EXR,
    }

    /// <summary>
    /// High-resolution capture settings for recording a cinematic to an image sequence. Resolution is
    /// not tied to the Game View — a specific camera is rendered at exactly <see cref="width"/> ×
    /// <see cref="height"/>, so 4K/6K/8K are all fine.
    /// </summary>
    [Serializable]
    public class CaptureSettings
    {
        [Tooltip("Output width in pixels (e.g. 3840, 7680). Not limited to the Game View resolution.")]
        public int width = 3840;

        [Tooltip("Output height in pixels (e.g. 2160, 4320).")]
        public int height = 2160;

        [Tooltip("Image-sequence format. PNG = tonemapped sRGB (ready to encode); EXR = linear HDR (for grading).")]
        public CaptureImageFormat format = CaptureImageFormat.PNG;

        [Tooltip("Supersampling factor used by the custom capture path (render larger, then downscale). 1 = off.")]
        public int supersample = 1;

        [Tooltip("Tag of the camera to capture: 'MainCamera' (default), a custom tag, or empty for the active camera.")]
        public string cameraTag = "MainCamera";
    }
}
