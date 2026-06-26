using System;
using UnityEngine;

namespace TimelineSmash
{
    public enum CaptureImageFormat
    {
        PNG,
        EXR,
    }

    /// <summary>What a Record run produces. A video (ProRes 422 HQ <c>.mov</c>) is encoded only on
    /// macOS/Windows; on Linux a video request falls back to the image sequence (no platform ProRes
    /// encoder). ProRes — not H.264, which is capped at 4K — so the video carries the same 4K/6K/8K
    /// resolution as the image sequence, which remains the cross-platform master.</summary>
    public enum CaptureOutput
    {
        ImageSequence,
        Video,
        ImageSequenceAndVideo,
    }

    /// <summary>
    /// High-resolution capture settings for recording a cinematic to an image sequence. Resolution is
    /// not tied to the Game View — a specific camera is rendered at exactly <see cref="width"/> ×
    /// <see cref="height"/>, so 4K/6K/8K are all fine.
    /// </summary>
    [Serializable]
    public class CaptureSettings
    {
        [Tooltip("What Record produces. Default: Video — a ProRes 422 HQ .mov is encoded as part of Record " +
                 "on macOS/Windows (ProRes, not H.264, so no 4K ceiling). On Linux there is no platform " +
                 "ProRes encoder, so a video request falls back to the PNG/EXR image sequence — the " +
                 "cross-platform, highest-resolution master. Pick ImageSequenceAndVideo to get both.")]
        public CaptureOutput output = CaptureOutput.Video;

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
