using UnityEditor;

namespace LuiStudio.Utilities.BlendShapeClamper.Editor
{
    [InitializeOnLoad]
    internal static class Util
    {
        static Util()
        {
            EditorApplication.delayCall += DisableDTMGizmoIcons;
        }

        static void DisableDTMGizmoIcons()
        {
            GizmoUtility.SetIconEnabled(typeof(BlendShapeClamper), false);
        }
    }
}