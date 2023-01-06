/* MIT License

 * Copyright (c) 2021-2022 Skurdt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. */

using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

////TODO: support multi-object editing

namespace SK.Libretro.Examples.Editor
{
    /// <summary>
    /// A custom inspector for <see cref="RebindActionUI"/> which provides a more convenient way for
    /// picking the binding which to rebind.
    /// </summary>
    [CustomEditor(typeof(RebindActionUI))]
    internal sealed class RebindActionUIEditor : UnityEditor.Editor
    {
        private static class Styles
        {
            public static GUIStyle boldLabel = new("MiniBoldLabel");
        }

        private SerializedProperty _actionProperty;
        private SerializedProperty _bindingIdProperty;
        private SerializedProperty _actionLabelProperty;
        private SerializedProperty _bindingTextProperty;
        private SerializedProperty _rebindOverlayProperty;
        private SerializedProperty _rebindTextProperty;
        private SerializedProperty _rebindStartEventProperty;
        private SerializedProperty _rebindStopEventProperty;
        private SerializedProperty _updateBindingUIEventProperty;
        private SerializedProperty _displayStringOptionsProperty;

        private readonly GUIContent _bindingLabel        = new("Binding");
        private readonly GUIContent _displayOptionsLabel = new("Display Options");
        private readonly GUIContent _uILabel             = new("UI");
        private readonly GUIContent _eventsLabel         = new("Events");
        private GUIContent[] _bindingOptions;
        private string[] _bindingOptionValues;
        private int _selectedBindingOption;

        private void OnEnable()
        {
            _actionProperty               = serializedObject.FindProperty("_action");
            _bindingIdProperty            = serializedObject.FindProperty("_bindingId");
            _actionLabelProperty          = serializedObject.FindProperty("_actionLabel");
            _bindingTextProperty          = serializedObject.FindProperty("_bindingText");
            _rebindOverlayProperty        = serializedObject.FindProperty("_rebindOverlay");
            _rebindTextProperty           = serializedObject.FindProperty("_rebindText");
            _updateBindingUIEventProperty = serializedObject.FindProperty("_updateBindingUIEvent");
            _rebindStartEventProperty     = serializedObject.FindProperty("_rebindStartEvent");
            _rebindStopEventProperty      = serializedObject.FindProperty("_rebindStopEvent");
            _displayStringOptionsProperty = serializedObject.FindProperty("_displayStringOptions");

            RefreshBindingOptions();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            // Binding section.
            EditorGUILayout.LabelField(_bindingLabel, Styles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                _ = EditorGUILayout.PropertyField(_actionProperty);

                int newSelectedBinding = EditorGUILayout.Popup(_bindingLabel, _selectedBindingOption, _bindingOptions);
                if (newSelectedBinding != _selectedBindingOption)
                {
                    string bindingId = _bindingOptionValues[newSelectedBinding];
                    _bindingIdProperty.stringValue = bindingId;
                    _selectedBindingOption = newSelectedBinding;
                }

                InputBinding.DisplayStringOptions optionsOld = (InputBinding.DisplayStringOptions)_displayStringOptionsProperty.intValue;
                InputBinding.DisplayStringOptions optionsNew = (InputBinding.DisplayStringOptions)EditorGUILayout.EnumFlagsField(_displayOptionsLabel, optionsOld);
                if (optionsOld != optionsNew)
                    _displayStringOptionsProperty.intValue = (int)optionsNew;
            }

            // UI section.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(_uILabel, Styles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                _ = EditorGUILayout.PropertyField(_actionLabelProperty);
                _ = EditorGUILayout.PropertyField(_bindingTextProperty);
                _ = EditorGUILayout.PropertyField(_rebindOverlayProperty);
                _ = EditorGUILayout.PropertyField(_rebindTextProperty);
            }

            // Events section.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(_eventsLabel, Styles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                _ = EditorGUILayout.PropertyField(_rebindStartEventProperty);
                _ = EditorGUILayout.PropertyField(_rebindStopEventProperty);
                _ = EditorGUILayout.PropertyField(_updateBindingUIEventProperty);
            }

            if (EditorGUI.EndChangeCheck())
            {
                _ = serializedObject.ApplyModifiedProperties();
                RefreshBindingOptions();
            }
        }

        private void RefreshBindingOptions()
        {
            InputAction action = null;

            InputActionReference actionReference = (InputActionReference)_actionProperty.objectReferenceValue;
            if (actionReference != null)
                 action = actionReference.action;

            if (action == null)
            {
                _bindingOptions        = new GUIContent[0];
                _bindingOptionValues   = new string[0];
                _selectedBindingOption = -1;
                return;
            }

            UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputBinding> bindings = action.bindings;
            int bindingCount = bindings.Count;

            _bindingOptions        = new GUIContent[bindingCount];
            _bindingOptionValues   = new string[bindingCount];
            _selectedBindingOption = -1;

            string currentBindingId = _bindingIdProperty.stringValue;
            for (int i = 0; i < bindingCount; ++i)
            {
                InputBinding binding   = bindings[i];
                string bindingId       = binding.id.ToString();
                bool haveBindingGroups = !string.IsNullOrEmpty(binding.groups);

                // If we don't have a binding groups (control schemes), show the device that if there are, for example,
                // there are two bindings with the display string "A", the user can see that one is for the keyboard
                // and the other for the gamepad.
                InputBinding.DisplayStringOptions displayOptions = InputBinding.DisplayStringOptions.DontUseShortDisplayNames
                                                                 | InputBinding.DisplayStringOptions.IgnoreBindingOverrides;
                if (!haveBindingGroups)
                    displayOptions |= InputBinding.DisplayStringOptions.DontOmitDevice;

                // Create display string.
                string displayString = action.GetBindingDisplayString(i, displayOptions);

                // If binding is part of a composite, include the part name.
                if (binding.isPartOfComposite)
                    displayString = $"{ObjectNames.NicifyVariableName(binding.name)}: {displayString}";

                // Some composites use '/' as a separator. When used in popup, this will lead to to submenus. Prevent
                // by instead using a backlash.
                displayString = displayString.Replace('/', '\\');

                // If the binding is part of control schemes, mention them.
                if (haveBindingGroups)
                {
                    InputActionAsset asset = action.actionMap?.asset;
                    if (asset != null)
                    {
                        string controlSchemes = string.Join(", ", binding.groups.Split(InputBinding.Separator)
                                                                                .Select(x => asset.controlSchemes.FirstOrDefault(c => c.bindingGroup == x).name));
                        displayString         = $"{displayString} ({controlSchemes})";
                    }
                }

                _bindingOptions[i]      = new GUIContent(displayString);
                _bindingOptionValues[i] = bindingId;

                if (currentBindingId == bindingId)
                    _selectedBindingOption = i;
            }
        }
    }
}
