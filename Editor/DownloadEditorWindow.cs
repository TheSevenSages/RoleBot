using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoleBot.Editor
{
    public class DownloadEditorWindow : EditorWindow
    {
        void CreateGUI()
        {
            // rootVisualElement.Clear();
            // rootVisualElement.AddToClassList("unity-editor");
            // titleContent = new GUIContent("RoleBot - Download Files");
            // var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Src/UI/Network/ModelDownloaderWindow.uxml");
            // visualTreeAsset.CloneTree(rootVisualElement);
        }

        [MenuItem("Window/RoleBot/Download Models")]
        public static void OpenWindow()
        {
            var window = GetWindow<DownloadEditorWindow>();
            window.minSize = new Vector2(400, 300);
            window.Show();
        }
    }
}
