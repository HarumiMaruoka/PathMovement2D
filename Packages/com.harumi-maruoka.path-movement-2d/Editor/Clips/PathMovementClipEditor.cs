using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Game.PathMovement2D.Editor
{
    [CustomEditor(typeof(PathMovementClip2D))]
    public sealed class PathMovementClipEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) ((PathMovementClip2D)target).Rebuild();
            if (GUILayout.Button("Open Path Movement Editor")) PathMovementEditorWindow.Open((PathMovementClip2D)target);
            if (GUILayout.Button("Duplicate Clip")) Duplicate((PathMovementClip2D)target);
        }

        [OnOpenAsset]
#if UNITY_6000_5_OR_NEWER
        private static bool OnOpenAsset(EntityId instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject(instanceId) is not PathMovementClip2D clip) return false;
#else
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not PathMovementClip2D clip) return false;
#endif
            PathMovementEditorWindow.Open(clip);
            return true;
        }

        internal static void Duplicate(PathMovementClip2D source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string path = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!string.IsNullOrEmpty(sourcePath) && AssetDatabase.CopyAsset(sourcePath, path))
            {
                AssetDatabase.SaveAssets();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<PathMovementClip2D>(path);
            }
        }

        [MenuItem("CONTEXT/Path2D/Create Movement Clip")]
        private static void FromPath(MenuCommand command)
        {
            var path = (Path2D)command.context;
            string destination = EditorUtility.SaveFilePanelInProject("Create Movement Clip", path.name + "Movement", "asset", "Save movement clip");
            if (string.IsNullOrEmpty(destination)) return;
            var clip = ScriptableObject.CreateInstance<PathMovementClip2D>();
            clip.CopyFrom(path);
            AssetDatabase.CreateAsset(clip, destination);
            AssetDatabase.SaveAssets();
            Selection.activeObject = clip;
            PathMovementEditorWindow.Open(clip);
        }
    }

    [CustomEditor(typeof(PathMovementPlayer2D))]
    public sealed class PathMovementPlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Edit / Preview Movement")) PathMovementEditorWindow.OpenForSelection();
        }
    }
}
