using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StormEngine.EditorTools
{
    /// <summary>
    /// Text setup for translations. Arabic, Hindi and Thai need UI Toolkit's advanced text generator
    /// (proper shaping and right-to-left text), which is a project setting without a public API.
    /// </summary>
    public static class StormEngineText
    {
        const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [MenuItem("Storm Engine/Enable Advanced Text (Arabic, Hindi, Thai)")]
        public static void EnableAdvancedTextMenu() => Debug.Log("[StormEngine] " + EnableAdvancedText());

        public static string EnableAdvancedText()
        {
            System.Type type = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if ((type = asm.GetType("UnityEditor.UIElements.UIToolkitProjectSettings")) != null) break;
            if (type == null) return "UIToolkitProjectSettings was not found in this Unity version.";
            var prop = type.GetProperty("enableAdvancedText", Any);
            if (prop != null && prop.GetGetMethod(true).IsStatic)
            {
                prop.SetValue(null, true);
                return "Advanced text enabled: " + prop.GetValue(null);
            }
            var sb = new StringBuilder("Could not set it directly. Members: ");
            foreach (var m in type.GetMembers(Any)) sb.Append(m.MemberType).Append(' ').Append(m.Name).Append("; ");
            return sb.ToString();
        }
    }
}
