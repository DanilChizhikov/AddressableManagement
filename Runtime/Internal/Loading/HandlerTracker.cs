using System;
using System.Collections.Generic;
using System.Text;
using DTech.AddressableManagement.Internal.Diagnostics;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal sealed class HandlerTracker
    {
        private readonly Dictionary<string, List<ITrackedHandler>> _handlers = new (StringComparer.Ordinal);

        public int Count { get; private set; }

        public void Track(ITrackedHandler handler)
        {
            if (!_handlers.TryGetValue(handler.RuntimeKey, out List<ITrackedHandler> list))
            {
                list = new List<ITrackedHandler>();
                _handlers.Add(handler.RuntimeKey, list);
            }

            list.Add(handler);
            Count++;
        }

        public void Untrack(ITrackedHandler handler)
        {
            if (!_handlers.TryGetValue(handler.RuntimeKey, out List<ITrackedHandler> list) || !list.Remove(handler))
            {
                return;
            }

            Count--;
            if (list.Count == 0)
            {
                _handlers.Remove(handler.RuntimeKey);
            }
        }

        public void ReleaseAll(string runtimeKey)
        {
            if (!_handlers.TryGetValue(runtimeKey, out List<ITrackedHandler> list))
            {
                return;
            }

            foreach (ITrackedHandler handler in list.ToArray())
            {
                handler.Release();
            }
        }

        public void ReleaseLeaked(AssetLog log)
        {
            if (Count == 0)
            {
                return;
            }

            var leaked = new List<ITrackedHandler>(Count);
            foreach (List<ITrackedHandler> list in _handlers.Values)
            {
                leaked.AddRange(list);
            }

            var report = new StringBuilder();
            report.Append(leaked.Count).Append(" handler(s) were not released before the AssetService was disposed:");
            foreach (ITrackedHandler handler in leaked)
            {
                report.Append("\n- ").Append(handler.RuntimeKey);
            }

            if (leaked[0].CreationTrace != null)
            {
                report.Append("\nThe first one was created at:\n").Append(leaked[0].CreationTrace);
            }

            log.Warning(report.ToString());
            foreach (ITrackedHandler handler in leaked)
            {
                handler.Release();
            }
        }
    }
}
