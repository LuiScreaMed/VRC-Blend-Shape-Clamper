using System;
using System.Collections.Generic;
using System.Linq;
using LuiStudio.Utilities.BlendShapeClamper.Editor.UnityEditor.IMGUI.Controls;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using static LuiStudio.Utilities.BlendShapeClamper.Editor.Localization;

namespace LuiStudio.Utilities.BlendShapeClamper.Editor
{
    [CustomEditor(typeof(BlendShapeClamper))]
    public class BlendShapeClamperEditor : global::UnityEditor.Editor
    {
        // Components and references
        private BlendShapeClamper _target;
        private SkinnedMeshRenderer _renderer;
        private Mesh _mesh;
        private bool _hasBlendShape = false;
        private Dictionary<Mesh, string[]> _blendshapeNames = new();

        // Preview
        private int _previewing = -1;
        private int _lastPreviewing = -1;

        // Styles
        static private Color _previewColor = new(1, 0, 0, 0.25f);

        private ReorderableList reorderableList;

        // Stop previewing when disabled
        private void OnDisable()
        {
            SetPreview(-1);
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            SetPreview(-1);
            Repaint();
        }

        // Init references and reorderable list
        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            _target = (BlendShapeClamper)target;
            _renderer = _target.GetComponent<SkinnedMeshRenderer>();
            _mesh = _renderer.sharedMesh;
            _hasBlendShape = _mesh.blendShapeCount > 0;

            if (!_renderer || !_mesh || !_hasBlendShape) return;

            SerializedProperty clampItems = serializedObject.FindProperty(nameof(BlendShapeClamper.ClampItems));
            reorderableList = new(serializedObject, clampItems)
            {
                displayAdd = true,
                displayRemove = true,
                draggable = true,
                drawHeaderCallback = (rect) =>
                {
                    EditorGUI.LabelField(rect, L("item_list.title"));
                },
                onChangedCallback = (_) =>
                {
                    SetPreview(-1);
                },
                onAddCallback = (list) =>
                {
                    Undo.RecordObject(target, "Adding blendshape");
                    _target.ClampItems.Add(new());
                    EditorUtility.SetDirty(target);
                },
                drawElementCallback = (rect, index, _, _) =>
                {
                    bool previewing = _previewing == index;
                    rect.height -= 2;
                    rect.y += 1;

                    if (previewing)
                    {
                        EditorGUI.DrawRect(rect, _previewColor);
                    }

                    Rect blendshapeNameRect = new(rect);
                    Rect clampToRect = new(rect);
                    blendshapeNameRect.xMax -= (rect.width * .65f) + 1;
                    clampToRect.xMin += (rect.width * .35f) + 1;

                    SerializedProperty item = clampItems.GetArrayElementAtIndex(index);

                    SerializedProperty blendshape = item.FindPropertyRelative(nameof(BlendShapeClampItem.BlendShape));
                    SerializedProperty clampTo = item.FindPropertyRelative(nameof(BlendShapeClampItem.ClampTo));

                    EditorGUI.BeginChangeCheck();

                    if (previewing)
                    {
                        if (GUI.Button(blendshapeNameRect, L("stop_previewing.label")))
                        {
                            SetPreview(-1);
                        }
                    }
                    else
                    {
                        DrawBlendshapePopup(_renderer.sharedMesh, blendshapeNameRect, blendshape, index);
                    }

                    if (string.IsNullOrEmpty(blendshape.stringValue)) return;

                    EditorGUI.BeginChangeCheck();
                    float value = EditorGUI.Slider(clampToRect, clampTo.floatValue, 0, 100);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Repaint();
                        Undo.RecordObject(target, "Set emote blendshape weight");
                        _target.ClampItems[index].ClampTo = value;
                        SetPreview(index);
                        EditorUtility.SetDirty(target);
                    }
                }
            };
            reorderableList.elementHeight += 2;
        }

        /// <summary>
        /// Draw a fake popup button to display blendshapes from given mesh
        /// </summary>
        /// <param name="targetMesh"></param>
        /// <param name="rect"></param>
        /// <param name="prop"></param>
        /// <param name="index"></param>
        private void DrawBlendshapePopup(Mesh targetMesh, Rect rect, SerializedProperty prop, int index)
        {
            GUIStyle style = new(other: EditorStyles.popup)
            {
                fixedHeight = rect.height
            };

            string[] selections = GetBlendshapeNames(targetMesh);

            // skip blendshapes already added
            selections = selections.Where((shape) =>
            {
                bool result = true;
                foreach (BlendShapeClampItem item in _target.ClampItems)
                {
                    if (item.BlendShape != shape) continue;
                    result = prop.stringValue == item.BlendShape;
                    break;
                }
                return result;
            }).ToArray();

            // if the clamp item is newly added, select the first blendshape on the selections above by default,
            // if there is no more selection, display "no blendshape"
            if (string.IsNullOrEmpty(prop.stringValue) && selections.Length > 0)
            {
                prop.stringValue = selections[0];
                _target.ClampItems[index].BlendShape = selections[0];
            }

            int shapeIndex = Array.FindIndex(selections, s => s == prop.stringValue);

            string blendshapeName = shapeIndex < 0 ? L("blendshape_popup.no_blendshape") : selections[shapeIndex];

            // fake popup button
            bool clicked = GUI.Button(rect, blendshapeName, style);
            if (clicked)
            {
                // display dropdown
                BlendShapeDropDown dropDown = new(new(), selections, (index) =>
                {
                    SetPreview(-1);
                    prop.stringValue = selections[index];
                    serializedObject.ApplyModifiedProperties();
                });
                dropDown.Show(new(Event.current.mousePosition, Vector2.zero), 400);
            }
        }

        /// <summary>
        /// Get blendshape names, if already got, get them from dicionary
        /// </summary>
        /// <param name="targetMesh"></param>
        /// <returns></returns>
        private string[] GetBlendshapeNames(Mesh targetMesh)
        {
            if (!_blendshapeNames.TryGetValue(targetMesh, out var selections))
            {
                selections = new string[targetMesh.blendShapeCount];
                for (int i = 0; i < targetMesh.blendShapeCount; i++)
                {
                    selections[i] = targetMesh.GetBlendShapeName(i);
                }

                _blendshapeNames[targetMesh] = selections;
            }

            return selections;
        }

        public override void OnInspectorGUI()
        {
            LanguageSelect();
            EditorGUILayout.Separator();
            if (!_renderer)
            {
                EditorGUILayout.HelpBox(L("warning.no_renderer"), MessageType.Warning);
                return;
            }
            if (!_mesh)
            {
                EditorGUILayout.HelpBox(L("warning.no_mesh"), MessageType.Warning);
                return;
            }
            if (!_hasBlendShape)
            {
                EditorGUILayout.HelpBox(L("warning.no_blendshape"), MessageType.Warning);
                return;
            }
            serializedObject.Update();
            reorderableList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Set animation mode to preview when blendshape changed
        /// </summary>
        /// <param name="index"></param>
        private void SetPreview(int index)
        {
            _previewing = index;
            bool inAnimationMode = AnimationMode.InAnimationMode();
            if (!_renderer || !_mesh || !_hasBlendShape)
            {
                _lastPreviewing = _previewing = -1;
                if (inAnimationMode) AnimationMode.StopAnimationMode();
                return;
            }
            if (index < 0)
            {
                _lastPreviewing = _previewing;
                if (inAnimationMode) AnimationMode.StopAnimationMode();
                return;
            }
            if (_target.ClampItems.Count <= index)
            {
                _lastPreviewing = _previewing = -1;
                if (inAnimationMode) AnimationMode.StopAnimationMode();
                return;
            }

            string[] meshBlendshapNames = GetBlendshapeNames(_renderer.sharedMesh);
            BlendShapeClampItem clampItem = _target.ClampItems[index];
            string blendShapeName = clampItem.BlendShape;
            int blendshapeIndex = -1;
            for (int i = 0; i < meshBlendshapNames.Length; i++)
            {
                if (meshBlendshapNames[i] == blendShapeName)
                {
                    blendshapeIndex = i;
                    break;
                }
            }
            if (blendshapeIndex == -1)
            {
                _lastPreviewing = _previewing = -1;
                if (inAnimationMode) AnimationMode.StopAnimationMode();
                return;
            }

            if (_lastPreviewing != _previewing)
            {
                _lastPreviewing = _previewing;
                if (inAnimationMode) AnimationMode.StopAnimationMode();
                AnimationMode.StartAnimationMode();

                EditorCurveBinding binding = new()
                {
                    path = "",
                    propertyName = $"blendshape.{meshBlendshapNames[blendshapeIndex]}",
                    type = typeof(SkinnedMeshRenderer)
                };
                PropertyModification modification = new()
                {
                    target = _renderer,
                    propertyPath = $"m_BlendShapeWeights.Array.data[{blendshapeIndex}]",
                    value = _renderer.GetBlendShapeWeight(blendshapeIndex).ToString(),
                    objectReference = null
                };
                AnimationMode.AddPropertyModification(binding, modification, false);
            }

            _renderer.SetBlendShapeWeight(blendshapeIndex, clampItem.ClampTo);
        }

        internal class BlendShapeDropDown : AdvancedDropdown
        {
            private string[] _blendshapes;

            private Action<int> _onSelected;

            public BlendShapeDropDown(AdvancedDropdownState state, string[] blendshapes, Action<int> onSelected) : base(state)
            {
                _blendshapes = blendshapes;
                _onSelected = onSelected;
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                AdvancedDropdownItem root = new("BlendShape");

                for (int i = 0; i < _blendshapes.Length; i++)
                {
                    string blendshape = _blendshapes[i];
                    AdvancedDropdownItem item = new(blendshape)
                    {
                        id = i
                    };
                    root.AddChild(item);
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                _onSelected.Invoke(item.id);
                base.ItemSelected(item);
            }
        }
    }

    // AdavancedDropdown max height hack from https://discussions.unity.com/t/add-maximum-window-size-to-advanceddropdown-control/753671/3
    namespace UnityEditor.IMGUI.Controls
    {
        public static class AdvancedDropdownExtensions
        {
            public static void Show(this AdvancedDropdown dropdown, Rect buttonRect, float maxHeight)
            {
                dropdown.Show(buttonRect);
                SetMaxHeightForOpenedPopup(buttonRect, maxHeight);
            }

            private static void SetMaxHeightForOpenedPopup(Rect buttonRect, float maxHeight)
            {
                var window = EditorWindow.focusedWindow;

                if (window == null)
                {
                    Debug.LogWarning("EditorWindow.focusedWindow was null.");
                    return;
                }

                if (!string.Equals(window.GetType().Namespace, typeof(AdvancedDropdown).Namespace))
                {
                    Debug.LogWarning("EditorWindow.focusedWindow " + EditorWindow.focusedWindow.GetType().FullName + " was not in expected namespace.");
                    return;
                }

                var position = window.position;
                if (position.height <= maxHeight)
                {
                    return;
                }

                position.height = maxHeight;
                window.minSize = position.size;
                window.maxSize = position.size;
                window.position = position;
                window.ShowAsDropDown(GUIUtility.GUIToScreenRect(buttonRect), position.size);
            }
        }
    }
}