using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Timba.Database.Editor
{
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    public class SerializableDictionaryDrawer : PropertyDrawer
    {
        private const float RowHeight = 20f;
        private const float HandleWidth = 16f;

        private int draggingIndex = -1;
        private int dropIndex = -1;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var keysProp = property.FindPropertyRelative("keys");
            return (keysProp.arraySize + 2) * RowHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var keysProp = property.FindPropertyRelative("keys");
            var valuesProp = property.FindPropertyRelative("values");

            position.height = RowHeight;
            EditorGUI.LabelField(position, label.text, EditorStyles.boldLabel);
            position.y += RowHeight;

            EditorGUI.indentLevel++;

            for (int i = 0; i < keysProp.arraySize; i++)
            {
                Rect rowRect = new Rect(position.x, position.y, position.width, RowHeight);

                // Dibujar el drop highlight si corresponde
                if (draggingIndex != -1 && dropIndex == i && draggingIndex != dropIndex)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.7f, 1f, 0.3f)); // Cyan semitransparente
                }

                DrawRow(rowRect, keysProp, valuesProp, i);
                position.y += RowHeight;
            }

            Rect addRect = new Rect(position.x, position.y, position.width, RowHeight);
            if (GUI.Button(addRect, "Add Entry"))
            {
                keysProp.InsertArrayElementAtIndex(keysProp.arraySize);
                valuesProp.InsertArrayElementAtIndex(valuesProp.arraySize);
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private void DrawRow(Rect rowRect, SerializedProperty keysProp, SerializedProperty valuesProp, int index)
        {
            var keyProp = keysProp.GetArrayElementAtIndex(index);
            var valueProp = valuesProp.GetArrayElementAtIndex(index);

            float spacing = 5f;
            float fieldWidth = (rowRect.width - HandleWidth - spacing * 3) / 2f;

            // Área del drag handle
            Rect dragRect = new Rect(rowRect.x, rowRect.y + 2f, HandleWidth, rowRect.height - 4f);
            DrawDragHandle(dragRect);

            // Campos clave y valor
            Rect keyRect = new Rect(dragRect.xMax + spacing, rowRect.y, fieldWidth, rowRect.height);
            Rect valueRect = new Rect(keyRect.xMax + spacing, rowRect.y, fieldWidth, rowRect.height);

            EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
            EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);

            HandleDragEvents(dragRect, index, keysProp, valuesProp);
        }

        private void DrawDragHandle(Rect rect)
        {
            Color originalColor = GUI.color;
            GUI.color = new Color(0.8f, 0.8f, 0.8f); // Gris claro

            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            float lineHeight = 2f;
            float spacing = 4f;
            float startY = rect.y + 4f;

            for (int i = 0; i < 3; i++)
            {
                Rect lineRect = new Rect(rect.x + 3f, startY + i * spacing, rect.width - 6f, lineHeight);
                EditorGUI.DrawRect(lineRect, Color.gray);
            }

            GUI.color = originalColor;
        }

        private void HandleDragEvents(Rect dragRect, int index, SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            Event evt = Event.current;

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (dragRect.Contains(evt.mousePosition) && evt.button == 0)
                    {
                        draggingIndex = index;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (draggingIndex != -1)
                    {
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (draggingIndex != -1 && dropIndex != -1 && dropIndex != draggingIndex)
                    {
                        MoveElement(keysProp, valuesProp, draggingIndex, dropIndex);
                        draggingIndex = -1;
                        dropIndex = -1;
                        GUI.changed = true;
                        evt.Use();
                    } else
                    {
                        draggingIndex = -1;
                        dropIndex = -1;
                    }
                    break;

                case EventType.Repaint:
                    if (dragRect.Contains(evt.mousePosition))
                    {
                        EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.Pan);
                        dropIndex = index;
                    }
                    break;
            }
        }

        private void MoveElement(SerializedProperty keysProp, SerializedProperty valuesProp, int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0 ||
                fromIndex >= keysProp.arraySize || toIndex >= keysProp.arraySize)
                return;

            keysProp.MoveArrayElement(fromIndex, toIndex);
            valuesProp.MoveArrayElement(fromIndex, toIndex);
        }
    }
}
