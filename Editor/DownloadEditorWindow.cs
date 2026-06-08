using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoleBot.Editor
{
    public class DownloadEditorWindow : EditorWindow
    {
        // Holds the live UI references for one resource row so it can be updated in place.
        private class Row
        {
            public DownloadResource resource;
            public Label status;
            public ProgressBar progress;
            public Button button;
        }

        private DownloadManifest m_Manifest;
        private readonly Dictionary<string, ResourceDownloadJob> m_Jobs = new Dictionary<string, ResourceDownloadJob>();
        private readonly List<Row> m_Rows = new List<Row>();

        void CreateGUI()
        {
            titleContent = new GUIContent("RoleBot - Download Files");
            BuildUI();
        }

        void OnFocus()
        {
            // Reflect any files added/removed outside the window (e.g. manual deletes).
            if (m_Rows.Count > 0)
                RefreshAllRows();
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            m_Rows.Clear();

            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            var title = new Label("RoleBot Resources")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14, marginBottom = 4 }
            };
            root.Add(title);

            root.Add(new Label("Download the AI models and voices required by RoleBot. Files are saved into the project Resources folder.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6 }
            });

            m_Manifest = ResourceDownloader.LoadManifest();
            if (m_Manifest == null || m_Manifest.resources == null || m_Manifest.resources.Count == 0)
            {
                root.Add(new HelpBox("Could not load the download manifest, or it is empty. Check download_manifest.json.", HelpBoxMessageType.Error));
                return;
            }

            // Toolbar
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginBottom = 6 } };
            var downloadAll = new Button(DownloadAllMissing) { text = "Download All Missing" };
            var refresh = new Button(RefreshAllRows) { text = "Refresh" };
            refresh.style.marginLeft = 4;
            toolbar.Add(downloadAll);
            toolbar.Add(refresh);
            root.Add(toolbar);

            // Grouped, scrollable list
            var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            root.Add(scroll);

            string currentCategory = null;
            foreach (var resource in m_Manifest.resources)
            {
                if (resource.category != currentCategory)
                {
                    currentCategory = resource.category;
                    scroll.Add(new Label(currentCategory)
                    {
                        style =
                        {
                            unityFontStyleAndWeight = FontStyle.Bold,
                            marginTop = 8,
                            marginBottom = 2,
                            borderBottomWidth = 1,
                            borderBottomColor = new Color(1, 1, 1, 0.15f)
                        }
                    });
                }

                scroll.Add(BuildRow(resource));
            }

            RefreshAllRows();
        }

        private VisualElement BuildRow(DownloadResource resource)
        {
            var rowElement = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2, paddingTop = 1, paddingBottom = 1 }
            };

            var name = new Label(resource.displayName) { style = { flexGrow = 1, flexShrink = 1 } };
            rowElement.Add(name);

            var status = new Label { style = { width = 110, unityTextAlign = TextAnchor.MiddleRight, marginRight = 6 } };
            rowElement.Add(status);

            var progress = new ProgressBar { style = { width = 110, marginRight = 6, display = DisplayStyle.None } };
            rowElement.Add(progress);

            var button = new Button { style = { width = 90 } };
            var row = new Row { resource = resource, status = status, progress = progress, button = button };
            button.clicked += () => StartDownload(row);
            rowElement.Add(button);

            m_Rows.Add(row);
            return rowElement;
        }

        private void StartDownload(Row row)
        {
            if (m_Jobs.TryGetValue(row.resource.id, out var existing) && existing.IsRunning)
                return;

            var job = new ResourceDownloadJob(m_Manifest, row.resource, () => RefreshRow(row));
            m_Jobs[row.resource.id] = job;
            job.Start();
            RefreshRow(row);
        }

        private void DownloadAllMissing()
        {
            foreach (var row in m_Rows)
            {
                if (!ResourceDownloader.IsInstalled(row.resource))
                    StartDownload(row);
            }
        }

        private void RefreshAllRows()
        {
            foreach (var row in m_Rows)
                RefreshRow(row);
        }

        private void RefreshRow(Row row)
        {
            m_Jobs.TryGetValue(row.resource.id, out var job);
            bool installed = ResourceDownloader.IsInstalled(row.resource);

            if (job != null && job.IsRunning)
            {
                row.progress.style.display = DisplayStyle.Flex;
                row.progress.value = job.Progress * 100f;
                row.progress.title = $"{Mathf.RoundToInt(job.Progress * 100f)}%";
                row.status.text = string.Empty;
                row.button.text = "Cancel";
                row.button.SetEnabled(true);
                return;
            }

            row.progress.style.display = DisplayStyle.None;

            if (job != null && job.IsDone && !job.Success)
                row.status.text = "Failed";
            else if (installed)
                row.status.text = "Installed";
            else
                row.status.text = "Not installed";

            row.status.style.color = installed
                ? new Color(0.4f, 0.8f, 0.4f)
                : (job != null && job.IsDone && !job.Success ? new Color(0.9f, 0.4f, 0.4f) : new Color(1, 1, 1, 0.5f));

            row.button.text = installed ? "Re-download" : "Download";
            row.button.SetEnabled(true);
        }

        [MenuItem("Window/RoleBot/Download Resources")]
        public static void OpenWindow()
        {
            var window = GetWindow<DownloadEditorWindow>();
            window.minSize = new Vector2(480, 360);
            window.Show();
        }
    }
}
