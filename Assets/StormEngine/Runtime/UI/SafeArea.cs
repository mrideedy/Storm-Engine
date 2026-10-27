using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// Screen insets the UI must stay clear of: notches, punch-hole cameras, rounded corners,
    /// status bars and gesture bars. The 3D world still renders edge to edge.
    /// </summary>
    public static class SafeArea
    {
        /// <summary>Insets for testing in the Editor, in pixels (left, top, right, bottom). Null uses the device.</summary>
        public static Vector4? DebugInsets;

        /// <summary>Insets in screen pixels: x = left, y = top, z = right, w = bottom.</summary>
        public static Vector4 Pixels()
        {
            if (DebugInsets.HasValue) return DebugInsets.Value;
            var sa = Screen.safeArea;
            float w = Screen.width, h = Screen.height;
            if (sa.width <= 0 || sa.height <= 0) return Vector4.zero;
            return new Vector4(
                Mathf.Max(0, sa.xMin),
                Mathf.Max(0, h - sa.yMax),
                Mathf.Max(0, w - sa.xMax),
                Mathf.Max(0, sa.yMin));
        }

        /// <summary>Insets converted to panel units (the UI's coordinate space).</summary>
        public static Vector4 Panel(IPanel panel)
        {
            var px = Pixels();
            if (panel == null || Screen.width <= 0 || Screen.height <= 0) return Vector4.zero;
            // Screen-to-panel uses a top-left origin.
            var origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            var size = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height)) - origin;
            if (size.x <= 0 || size.y <= 0) return Vector4.zero;
            float sx = size.x / Screen.width, sy = size.y / Screen.height;
            return new Vector4(px.x * sx, px.y * sy, px.z * sx, px.w * sy);
        }
    }
}
