using UnityEngine;
using UnityEditor;
using HiddenCats.Core;

namespace HiddenCats.Core.Editor
{
    /// <summary>
    /// Editor tool for configuring CursorManager in Edit mode.
    /// This allows you to configure cursor textures and settings even when the game is not running.
    /// </summary>
    public class CursorManagerEditor : EditorWindow
    {
        [MenuItem("GameObject/Hidden Cats/Cursor Manager", false, 10)]
        private static void CreateCursorManagerFromMenu()
        {
            // Check if CursorManager already exists
            CursorManager existing = Object.FindFirstObjectByType<CursorManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("CursorManager Exists", 
                    "CursorManager already exists in the scene. Selecting the existing one.", 
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create new GameObject with CursorManager
            GameObject cursorManagerObj = new GameObject("CursorManager");
            CursorManager cursorManager = cursorManagerObj.AddComponent<CursorManager>();
            
            // Mark as dirty so it gets saved
            EditorUtility.SetDirty(cursorManagerObj);
            
            // Register undo
            Undo.RegisterCreatedObjectUndo(cursorManagerObj, "Create Cursor Manager");
            
            // Select the new object
            Selection.activeGameObject = cursorManagerObj;
            
            Debug.Log("[CursorManagerEditor] Created CursorManager GameObject in scene.");
        }

        private CursorManager cursorManager;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Cursor Manager Configuration")]
        public static void ShowWindow()
        {
            CursorManagerEditor window = GetWindow<CursorManagerEditor>("Cursor Manager Config");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            FindCursorManager();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Cursor Manager Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Configure cursor textures and settings here. " +
                "If CursorManager doesn't exist in the scene, click 'Create CursorManager' to add it.", 
                MessageType.Info);
            EditorGUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Find or Create CursorManager
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find CursorManager in Scene", GUILayout.Height(30)))
            {
                FindCursorManager();
            }
            if (GUILayout.Button("Create CursorManager", GUILayout.Height(30)))
            {
                CreateCursorManager();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (cursorManager == null)
            {
                EditorGUILayout.HelpBox("CursorManager not found in scene. Click 'Create CursorManager' to add it.", 
                    MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            // Draw Inspector for CursorManager
            EditorGUILayout.LabelField("Current CursorManager Settings:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Use SerializedObject to edit the component
            SerializedObject serializedObject = new SerializedObject(cursorManager);
            
            // Draw all serialized fields with better organization
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            bool firstProperty = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                // Skip script field
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                // Add spacing before first property
                if (firstProperty)
                {
                    EditorGUILayout.Space(5);
                    firstProperty = false;
                }

                EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }

            serializedObject.ApplyModifiedProperties();
            
            // Auto-save when values change
            if (GUI.changed)
            {
                EditorUtility.SetDirty(cursorManager);
            }

            EditorGUILayout.Space(10);

            // Info section
            EditorGUILayout.LabelField("Configuration Help:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Drag cursor textures to 'Normal Cursor' and 'Large Cursor' fields, OR\n" +
                "2. Set 'Normal Cursor Resource Path' and 'Large Cursor Resource Path' to load from Resources folder.\n\n" +
                "Resource paths are relative to Resources folder (e.g., 'Cursor/MouseX1' or 'MyFolder/Cursor1').\n" +
                "Leave paths empty to use default 'Cursor/MouseX1' and 'Cursor/MouseX2'.", 
                MessageType.None);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Apply button
            if (GUILayout.Button("Apply Settings", GUILayout.Height(30)))
            {
                EditorUtility.SetDirty(cursorManager);
                AssetDatabase.SaveAssets();
                Debug.Log("[CursorManagerEditor] Settings saved!");
            }
        }

        private void FindCursorManager()
        {
            cursorManager = FindFirstObjectByType<CursorManager>();
            if (cursorManager == null)
            {
                Debug.Log("[CursorManagerEditor] CursorManager not found in scene. Use 'Create CursorManager' to add it.");
            }
            else
            {
                Debug.Log($"[CursorManagerEditor] Found CursorManager: {cursorManager.gameObject.name}");
            }
        }

        private void CreateCursorManager()
        {
            // Check if CursorManager already exists
            CursorManager existing = FindFirstObjectByType<CursorManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("CursorManager Exists", 
                    "CursorManager already exists in the scene. Please use the existing one.", 
                    "OK");
                cursorManager = existing;
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create new GameObject with CursorManager
            GameObject cursorManagerObj = new GameObject("CursorManager");
            cursorManager = cursorManagerObj.AddComponent<CursorManager>();
            
            // Mark as dirty so it gets saved
            EditorUtility.SetDirty(cursorManagerObj);
            
            // Register undo
            Undo.RegisterCreatedObjectUndo(cursorManagerObj, "Create Cursor Manager");
            
            // Select the new object
            Selection.activeGameObject = cursorManagerObj;
            
            Debug.Log("[CursorManagerEditor] Created CursorManager GameObject in scene.");
            EditorUtility.DisplayDialog("CursorManager Created", 
                "CursorManager has been created in the scene. You can now configure it in the Inspector or in this window.", 
                "OK");
        }
    }

    /// <summary>
    /// Custom Inspector for CursorManager to show helpful information.
    /// </summary>
    [CustomEditor(typeof(CursorManager))]
    public class CursorManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "💡 Tip: You can drag cursor textures directly here, OR set Resource Paths to load from Resources folder.\n\n" +
                "Resource paths are relative to Resources folder (e.g., 'Cursor/MouseX1' or 'MyFolder/Cursor1').\n" +
                "Leave paths empty to use default 'Cursor/MouseX1' and 'Cursor/MouseX2'.", 
                MessageType.Info);
            
            EditorGUILayout.Space(5);
            if (GUILayout.Button("Open Cursor Manager Configuration Window", GUILayout.Height(25)))
            {
                CursorManagerEditor.ShowWindow();
            }
        }
    }
}
