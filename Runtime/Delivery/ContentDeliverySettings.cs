using System;
using System.Collections.Generic;
using UnityEngine;

namespace DTech.AddressableManagement
{
    /// <summary>
    /// Where remote Addressables content is requested from. Every http(s) request Addressables sends is rewritten:
    /// the file name is appended to the active content root, then the origin is replaced by the active mirror.
    /// A 404 moves to the next content root; a connection failure, timeout or 5xx moves to the next mirror.
    /// Entries may use the {Platform} token and Addressables runtime tokens such as {UnityEngine.Application.version}.
    /// </summary>
    [Serializable]
    public sealed class ContentDeliverySettings
    {
        /// <summary>
        /// Absolute http(s) folder URLs that hold the remote catalog and bundles, tried in order. Empty keeps the
        /// folder baked into the catalog.
        /// </summary>
        public IReadOnlyList<string> ContentRoots => _contentRoots ?? Array.Empty<string>();

        /// <summary>
        /// Absolute http(s) origins such as https://cdn.example.com, tried in order. Empty keeps the origin of
        /// the request.
        /// </summary>
        public IReadOnlyList<string> Mirrors => _mirrors ?? Array.Empty<string>();

        /// <summary>
        /// Path requested from each mirror at initialization until one answers with 2xx. Empty skips the probe.
        /// </summary>
        public string ProbePath => _probePath ?? string.Empty;

        /// <summary>
        /// How long a probe request may take.
        /// </summary>
        public TimeSpan ProbeTimeout => TimeSpan.FromSeconds(Math.Max(1f, _probeTimeoutSeconds));

        [Tooltip("Absolute http(s) folder URLs with the remote catalog and bundles, tried in order. A 404 moves to the next one.")]
        [SerializeField] private string[] _contentRoots = Array.Empty<string>();

        [Tooltip("Absolute http(s) origins (scheme://host[:port]) tried in order. A connection failure, timeout or 5xx moves to the next one.")]
        [SerializeField] private string[] _mirrors = Array.Empty<string>();

        [Tooltip("Path requested from each mirror at initialization until one answers with 2xx. Empty skips the probe.")]
        [SerializeField] private string _probePath = string.Empty;

        [Tooltip("Seconds a probe request may take.")]
        [SerializeField, Min(1f)] private float _probeTimeoutSeconds = 3f;

        /// <summary>
        /// Creates settings that keep every request as Addressables built it.
        /// </summary>
        public ContentDeliverySettings()
        {
        }

        /// <summary>
        /// Creates settings in code, for example from remote configuration.
        /// </summary>
        /// <param name="contentRoots">Folder URLs tried in order; null for none.</param>
        /// <param name="mirrors">Origins tried in order; null for none.</param>
        /// <param name="probePath">Path probed on each mirror at initialization; null or empty to skip.</param>
        /// <param name="probeTimeout">How long a probe request may take.</param>
        public ContentDeliverySettings(IReadOnlyList<string> contentRoots, IReadOnlyList<string> mirrors,
                string probePath, TimeSpan probeTimeout)
        {
            _contentRoots = Copy(contentRoots);
            _mirrors = Copy(mirrors);
            _probePath = probePath ?? string.Empty;
            _probeTimeoutSeconds = (float)Math.Max(1d, probeTimeout.TotalSeconds);
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                copy[i] = values[i];
            }

            return copy;
        }
    }
}