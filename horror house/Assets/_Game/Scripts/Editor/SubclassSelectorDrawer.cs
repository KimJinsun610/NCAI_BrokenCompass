using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// <see cref="SubclassSelectorAttribute"/>가 붙은 <c>[SerializeReference]</c> 필드에 종류 선택 드롭다운을 그린다.
    /// 메뉴 이름은 <see cref="ConditionMenuAttribute"/>를 쓰고, 없으면 타입 이름을 쓴다.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public sealed class SubclassSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, List<Type>> Cache = new Dictionary<Type, List<Type>>();

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect button = new Rect(
                position.x + EditorGUIUtility.labelWidth + 2f,
                position.y,
                Mathf.Max(0f, position.width - EditorGUIUtility.labelWidth - 2f),
                EditorGUIUtility.singleLineHeight);

            object current = property.managedReferenceValue;
            string caption = current == null ? "(없음)" : MenuName(current.GetType());

            if (EditorGUI.DropdownButton(button, new GUIContent(caption), FocusType.Keyboard))
            {
                ShowMenu(property, BaseType());
            }

            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndProperty();
        }

        private Type BaseType()
        {
            Type type = fieldInfo.FieldType;
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        private static void ShowMenu(SerializedProperty property, Type baseType)
        {
            SerializedObject owner = property.serializedObject;
            string path = property.propertyPath;
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("(없음)"), property.managedReferenceValue == null, () => Assign(owner, path, null));

            List<Type> types = Candidates(baseType);
            for (int i = 0; i < types.Count; i++)
            {
                Type t = types[i];
                bool selected = property.managedReferenceValue != null && property.managedReferenceValue.GetType() == t;
                menu.AddItem(new GUIContent(MenuName(t)), selected, () => Assign(owner, path, Activator.CreateInstance(t)));
            }

            menu.ShowAsContext();
        }

        private static void Assign(SerializedObject owner, string path, object value)
        {
            owner.Update();
            SerializedProperty target = owner.FindProperty(path);
            if (target == null)
            {
                return;
            }

            target.managedReferenceValue = value;
            owner.ApplyModifiedProperties();
        }

        private static List<Type> Candidates(Type baseType)
        {
            if (Cache.TryGetValue(baseType, out List<Type> list))
            {
                return list;
            }

            list = new List<Type>();
            foreach (Type t in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                {
                    continue;
                }

                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                if (t.GetCustomAttribute<SerializableAttribute>() == null)
                {
                    continue;
                }

                list.Add(t);
            }

            list.Sort((a, b) => string.CompareOrdinal(MenuName(a), MenuName(b)));
            Cache[baseType] = list;
            return list;
        }

        private static string MenuName(Type type)
        {
            ConditionMenuAttribute menu = type.GetCustomAttribute<ConditionMenuAttribute>();
            return menu != null ? menu.Path : type.Name;
        }
    }
}
