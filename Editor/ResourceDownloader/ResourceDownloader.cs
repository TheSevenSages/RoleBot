// Written for RoleBot - editor-side resource downloading.

using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace RoleBot.Editor
{
    /// <summary>
    /// Static helpers for locating the manifest, resolving paths/URLs and querying install state.
    /// The actual transfer is driven by <see cref="ResourceDownloadJob"/>.
    /// </summary>
    public static class ResourceDownloader
    {
        public const string PackageRoot = "Packages/com.thesevensages.rolebot";
        public const string ResourcesFolder = "Assets/Resources/RoleBot";
        public const string ManifestPath = PackageRoot + "/Editor/ResourceDownloader/download_manifest.json";

        /// <summary>Loads and parses the manifest, or returns null (and logs) on failure.</summary>
        public static DownloadManifest LoadManifest()
        {
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
                string json = asset != null ? asset.text : File.ReadAllText(Path.GetFullPath(ManifestPath));
                return JsonUtility.FromJson<DownloadManifest>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoleBot] Failed to load download manifest at {ManifestPath}\n{ex.Message}");
                return null;
            }
        }

        /// <summary>Project-relative asset path (e.g. "Packages/.../Resources/voices/af.bin").</summary>
        public static string GetAssetPath(DownloadResource r)
            => $"{ResourcesFolder}/{r.destination}";

        /// <summary>Absolute file-system path of where the resource is/will be saved.</summary>
        public static string GetAbsolutePath(DownloadResource r)
            => Path.GetFullPath(GetAssetPath(r));

        public static bool IsInstalled(DownloadResource r)
            => File.Exists(GetAbsolutePath(r));

        /// <summary>Combines the resource's source base URL with its relative url (absolute urls pass through).</summary>
        public static string ResolveUrl(DownloadManifest manifest, DownloadResource r)
        {
            if (string.IsNullOrEmpty(r.url))
                return string.Empty;
            if (r.url.StartsWith("http://") || r.url.StartsWith("https://"))
                return r.url;

            string baseUrl = (manifest != null ? manifest.GetBaseUrl(r.source) : null) ?? string.Empty;
            if (baseUrl.Length > 0 && !baseUrl.EndsWith("/"))
                baseUrl += "/";
            return baseUrl + r.url.TrimStart('/');
        }

        /// <summary>True if any resource still resolves to an unedited placeholder (or empty) URL.</summary>
        public static bool HasPlaceholderUrls(DownloadManifest manifest)
        {
            if (manifest == null || manifest.resources == null)
                return true;
            foreach (var r in manifest.resources)
            {
                string url = ResolveUrl(manifest, r);
                if (string.IsNullOrEmpty(url) || url.Contains("YOUR_USERNAME") || url.Contains("YOUR_REPO"))
                    return true;
            }
            return false;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "—";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
            return $"{size:0.#} {units[unit]}";
        }
    }

    /// <summary>
    /// Drives a single streaming download in the editor. Progress is pumped from
    /// <see cref="EditorApplication.update"/> so the UI can poll <see cref="Progress"/> each frame.
    /// </summary>
    public class ResourceDownloadJob
    {
        public DownloadResource Resource { get; }
        public float Progress { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsDone { get; private set; }
        public bool Success { get; private set; }
        public string Error { get; private set; }

        readonly string _url;
        readonly string _assetPath;
        readonly string _absolutePath;
        readonly string _tempPath;
        readonly Action _onChanged;

        UnityWebRequest _request;
        UnityWebRequestAsyncOperation _operation;

        public ResourceDownloadJob(DownloadManifest manifest, DownloadResource resource, Action onChanged)
        {
            Resource = resource;
            _onChanged = onChanged;
            _url = ResourceDownloader.ResolveUrl(manifest, resource);
            _assetPath = ResourceDownloader.GetAssetPath(resource);
            _absolutePath = ResourceDownloader.GetAbsolutePath(resource);
            _tempPath = _absolutePath + ".download";
        }

        public void Start()
        {
            if (IsRunning) return;

            if (string.IsNullOrEmpty(_url))
            {
                Finish(false, "No URL configured for this resource.");
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_absolutePath));

                _request = new UnityWebRequest(_url, UnityWebRequest.kHttpVerbGET);
                _request.downloadHandler = new DownloadHandlerFile(_tempPath) { removeFileOnAbort = true };
                _operation = _request.SendWebRequest();

                IsRunning = true;
                IsDone = false;
                Progress = 0f;
                EditorApplication.update += Tick;
                _onChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Finish(false, ex.Message);
            }
        }

        public void Cancel()
        {
            if (!IsRunning) return;
            _request?.Abort();
            Finish(false, "Cancelled.");
        }

        void Tick()
        {
            if (_request == null) { Finish(false, "Request was lost."); return; }

            Progress = _request.downloadProgress;
            _onChanged?.Invoke();

            if (!_operation.isDone)
                return;

#if UNITY_2020_2_OR_NEWER
            bool failed = _request.result != UnityWebRequest.Result.Success;
#else
            bool failed = _request.isNetworkError || _request.isHttpError;
#endif
            if (failed)
            {
                SafeDeleteTemp();
                Finish(false, _request.error);
                return;
            }

            if (!string.IsNullOrEmpty(Resource.sha256) && !VerifyHash(_tempPath, Resource.sha256, out string hashError))
            {
                SafeDeleteTemp();
                Finish(false, hashError);
                return;
            }

            try
            {
                if (File.Exists(_absolutePath))
                    File.Delete(_absolutePath);
                File.Move(_tempPath, _absolutePath);

                AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceUpdate);
                Progress = 1f;
                Finish(true, null);
            }
            catch (Exception ex)
            {
                SafeDeleteTemp();
                Finish(false, ex.Message);
            }
        }

        void Finish(bool success, string error)
        {
            EditorApplication.update -= Tick;
            _request?.Dispose();
            _request = null;
            _operation = null;

            IsRunning = false;
            IsDone = true;
            Success = success;
            Error = error;

            if (!success && !string.IsNullOrEmpty(error))
                Debug.LogError($"[RoleBot] Download failed for '{Resource.displayName}' ({_url})\n{error}");

            _onChanged?.Invoke();
        }

        void SafeDeleteTemp()
        {
            try { if (File.Exists(_tempPath)) File.Delete(_tempPath); }
            catch { /* best effort */ }
        }

        static bool VerifyHash(string path, string expected, out string error)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var sha = SHA256.Create();
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, expected.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                {
                    error = $"Checksum mismatch (expected {expected}, got {actual}).";
                    return false;
                }
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to verify checksum: {ex.Message}";
                return false;
            }
        }
    }
}
