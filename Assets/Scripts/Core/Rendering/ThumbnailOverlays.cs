using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Lighting;
using KitchenDesigner.Core.Measure;

namespace KitchenDesigner.Core
{
    public static class ThumbnailOverlays
    {
        public static IDisposable Suppress() => new Scope();

        private sealed class Scope : IDisposable
        {
            private readonly List<Behaviour> _switchedOff = new List<Behaviour>();
            private bool _disposed;

            public Scope()
            {
                SwitchOff<EdgeOutlineRenderer>();
                SwitchOff<SpatialGridRenderer>();
                SwitchOff<MeasureRenderer>();
                SwitchOff<LightPickRenderer>();
            }

            private void SwitchOff<T>() where T : Behaviour
            {
                var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
                foreach (var overlay in found)
                {
                    if (overlay == null || !overlay.enabled) continue;
                    overlay.enabled = false;
                    _switchedOff.Add(overlay);
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                foreach (var overlay in _switchedOff)
                    if (overlay != null) overlay.enabled = true;
                _switchedOff.Clear();
            }
        }
    }
}
