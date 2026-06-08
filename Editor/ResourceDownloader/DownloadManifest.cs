// Written for RoleBot - resource download manifest model.

using System;
using System.Collections.Generic;

namespace RoleBot.Editor
{
    /// <summary>
    /// A single downloadable resource described by the manifest.
    /// </summary>
    [Serializable]
    public class DownloadResource
    {
        /// <summary>Stable, unique identifier (used as a dictionary key in the UI).</summary>
        public string id;
        /// <summary>Human-readable name shown in the window.</summary>
        public string displayName;
        /// <summary>Grouping header shown in the window (e.g. "Voices (Kokoro)").</summary>
        public string category;
        /// <summary>Optional key of a named entry in the manifest <see cref="DownloadManifest.sources"/> list. Empty uses the default baseUrl.</summary>
        public string source;
        /// <summary>Download path appended to the chosen base URL. May also be an absolute http(s) URL (in which case the base is ignored).</summary>
        public string url;
        /// <summary>Save location relative to the package Resources folder (so it is loadable via Resources.Load).</summary>
        public string destination;
        /// <summary>Expected size in bytes, or 0 if unknown (used only for display).</summary>
        public long sizeBytes;
        /// <summary>Optional lowercase hex SHA-256 used to verify the download. Empty to skip verification.</summary>
        public string sha256;
    }

    /// <summary>
    /// A named base URL that resources can reference via <see cref="DownloadResource.source"/>,
    /// allowing different resources to be hosted on different repos/servers.
    /// </summary>
    [Serializable]
    public class DownloadSource
    {
        public string key;
        public string baseUrl;
    }

    /// <summary>
    /// Root manifest describing every resource the download window can fetch.
    /// </summary>
    [Serializable]
    public class DownloadManifest
    {
        /// <summary>Default base URL used when a resource has no (or an unknown) <see cref="DownloadResource.source"/>.</summary>
        public string baseUrl;
        /// <summary>Named base URLs for resources hosted in different locations.</summary>
        public List<DownloadSource> sources = new List<DownloadSource>();
        public List<DownloadResource> resources = new List<DownloadResource>();

        /// <summary>Returns the base URL for the given source key, falling back to <see cref="baseUrl"/>.</summary>
        public string GetBaseUrl(string sourceKey)
        {
            if (!string.IsNullOrEmpty(sourceKey) && sources != null)
            {
                foreach (var s in sources)
                {
                    if (s != null && s.key == sourceKey)
                        return s.baseUrl;
                }
            }
            return baseUrl;
        }
    }
}
