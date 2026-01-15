using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace LuiStudio.Utilities.BlendShapeClamper
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [DisallowMultipleComponent]
    public class BlendShapeClamper : MonoBehaviour, IEditorOnly
    {
        public List<BlendShapeClampItem> ClampItems = new();
    }

    [Serializable]
    public class BlendShapeClampItem
    {
        /// <summary>
        /// Name of the shapekey
        /// </summary>
        public string BlendShape;

        /// <summary>
        /// The maximum value to clamp
        /// </summary>
        public float ClampTo;
    }
}
