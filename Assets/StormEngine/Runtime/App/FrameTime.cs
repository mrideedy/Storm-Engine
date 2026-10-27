using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// The step the game advances by each frame: the real frame time, or the fixed step while a video
    /// is recorded frame by frame (<see cref="Time.captureDeltaTime"/>), so recorded footage plays at
    /// true speed however long each frame takes to save.
    /// </summary>
    public static class FrameTime
    {
        public static float Delta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
    }
}
