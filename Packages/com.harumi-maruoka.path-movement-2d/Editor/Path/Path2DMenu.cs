using UnityEditor;
using UnityEngine;

namespace Game.PathMovement2D.Editor
{
    internal static class Path2DMenu
    {
        [MenuItem("GameObject/2D Object/Path Movement 2D/Path", false, 20)]
        private static void CreatePath(MenuCommand command)
        {
            GameObject gameObject = new("Path 2D");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Path 2D");
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            gameObject.AddComponent<Path2D>();
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("Component/Path Movement 2D/Path Follower 2D")]
        private static void AddFollower(MenuCommand command)
        {
            GameObject gameObject = (GameObject)command.context;
            Undo.AddComponent<PathFollower2D>(gameObject);
        }
    }
}
