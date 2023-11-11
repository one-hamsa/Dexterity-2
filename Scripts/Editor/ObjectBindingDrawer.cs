using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OneHamsa.Dexterity
{
    [CustomPropertyDrawer(typeof(ObjectBinding), useForChildren: true)]
    public class ObjectBindingDrawer : PropertyDrawer
    {
        private const string kNoFunctionString = "No Function";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // header
            {
                var headerRect = position;
                headerRect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(headerRect, label, EditorStyles.boldLabel);
            }
            
            position.y += EditorGUIUtility.singleLineHeight;
            
            var objectBinding = (ObjectBinding)Utils.GetTargetObjectOfProperty(property);
            
            Rect[] subRects = GetRowRects(position);
            Rect goRect = subRects[0];
            Rect functionRect = subRects[1];
 
            // find the current event target...
            var targetProp = property.FindPropertyRelative(nameof(ObjectBinding.target));
            var methodNameProp = property.FindPropertyRelative(nameof(ObjectBinding.methodName));
 
            Color c = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;
 
            var targetToUse = targetProp.objectReferenceValue;
            if (targetToUse is Component comp)
                targetToUse = comp.gameObject;
 
            EditorGUI.BeginProperty(goRect, GUIContent.none, targetProp);
            EditorGUI.BeginChangeCheck();
            {
                GUI.Box(goRect, GUIContent.none);
                var newObj = EditorGUI.ObjectField(goRect, targetToUse, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck() && newObj != targetToUse)
                {
                    methodNameProp.stringValue = null;
                    targetProp.objectReferenceValue = newObj;
                }
            }
            EditorGUI.EndProperty();
 
            using (new EditorGUI.DisabledScope(targetProp.objectReferenceValue == null))
            {
                EditorGUI.BeginProperty(functionRect, GUIContent.none, methodNameProp);
                {
                    GUIContent buttonContent;
                    if (EditorGUI.showMixedValue)
                    {
                        buttonContent = mixedValueContent;
                    }
                    else
                    {
                        var buttonLabel = new StringBuilder();
                        if (targetProp.objectReferenceValue == null || string.IsNullOrEmpty(methodNameProp.stringValue))
                        {
                            buttonLabel.Append(kNoFunctionString);
                        }
                        else
                        {
                            buttonLabel.Append(targetProp.objectReferenceValue.GetType().Name);
 
                            if (!string.IsNullOrEmpty(methodNameProp.stringValue))
                            {
                                buttonLabel.Append(".");
                                if (methodNameProp.stringValue.StartsWith("set_"))
                                    buttonLabel.Append(methodNameProp.stringValue.Substring(4));
                                else
                                    buttonLabel.Append(methodNameProp.stringValue);
                            }
                        }
                        buttonContent = Temp(buttonLabel.ToString());
                    }

                    if (EditorGUI.DropdownButton(functionRect, buttonContent, FocusType.Passive, EditorStyles.popup))
                    {
                        BuildPopupList(targetProp, methodNameProp, GetReflectedOptions(targetToUse, objectBinding.supportedTypes, "")).DropDown(functionRect);
                    }
                        // BuildPopupList(listenerTarget.objectReferenceValue, m_DummyEvent, pListener).DropDown(functionRect);
                }
                EditorGUI.EndProperty();
            }
            GUI.backgroundColor = c;
        }

        private GenericMenu BuildPopupList(SerializedProperty target, SerializedProperty methodName, IEnumerable<ReflectedOption> options)
        {
             //special case for components... we want all the game objects targets there!
             var targetToUse = target.objectReferenceValue;
             if (targetToUse is Component c)
                 targetToUse = c.gameObject;
 
             // find the current event target...
            var menu = new GenericMenu();
                         menu.AddItem(new GUIContent(kNoFunctionString),
                             string.IsNullOrEmpty(methodName.stringValue),
                             () =>
                             {
                                 target.objectReferenceValue = targetToUse;
                                 methodName.stringValue = null;
                                 methodName.serializedObject.ApplyModifiedProperties();
                             });
             
             if (targetToUse == null)
                 return menu;

             var seen = new HashSet<string>();
             foreach (var option in options)
             {
                 if (!seen.Add(option.path))
                     continue;
                 
                 menu.AddItem(new GUIContent(option.path), option.obj == target.objectReferenceValue && option.memberInfo.Name == methodName.stringValue, () =>
                 {
                     target.objectReferenceValue = option.obj;
                     methodName.stringValue = option.memberInfo.Name;
                     methodName.serializedObject.ApplyModifiedProperties();
                 });
             }

             return menu;
        }

        private static IEnumerable<ReflectedOption> GetReflectedOptions(Object obj, ObjectBinding.ValueType supportedTypes, string rootPath)
        {
            var objType = obj.GetType();

            var path = string.IsNullOrEmpty(rootPath) ? "GameObject/" : rootPath;
            foreach (var method in objType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.GetParameters().Length == 0 && supportedTypes.Supports(method.ReturnType))
                {
                    var name = method.Name;
                    if (method.Name.StartsWith("get_"))
                        name = method.Name.Substring(4);
                    yield return new ReflectedOption
                    {
                        obj = obj,
                        path = path + name,
                        memberInfo = method
                    };
                }
            }

            foreach (var prop in objType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (supportedTypes.Supports(prop.PropertyType))
                {
                    yield return new ReflectedOption
                    {
                        obj = obj,
                        path = path + prop.Name,
                        memberInfo = prop
                    };
                }
            }

            if (obj is GameObject gameObject)
            {
                foreach (var component in gameObject.GetComponents<Component>())
                {
                    if (component == null)
                        continue;

                    foreach (var option in GetReflectedOptions(component, supportedTypes, rootPath + component.GetType().Name + "/"))
                        yield return option;
                }
            }
        }

        private struct ReflectedOption
        {
            public string path;
            public Object obj;
            public MemberInfo memberInfo;
        }

        Rect[] GetRowRects(Rect rect)
        {
            Rect[] rects = new Rect[2];

            rect.height = EditorGUIUtility.singleLineHeight;
            Rect goRect = rect;
            goRect.width *= 0.3f;

            Rect functionRect = rect;
            functionRect.xMin = goRect.xMax + kSpacing;

            rects[0] = goRect;
            rects[1] = functionRect;
            return rects;
        }
        
        private static readonly GUIContent mixedValueContent = EditorGUIUtility.TrTextContent("—", "Mixed Values");
        private static readonly GUIContent s_Text = new GUIContent();
        private const float kSpacing = 5f;

        private static GUIContent Temp(string t)
        {
            s_Text.text = t;
            s_Text.tooltip = string.Empty;
            return s_Text;
        }
    }
}