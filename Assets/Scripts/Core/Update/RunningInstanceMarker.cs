#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Threading;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    public static class RunningInstanceMarker
    {
        private static Mutex? _held;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HoldForTheWholeProcessLifetime()
        {
            if (_held != null) return;
            try { _held = new Mutex(false, RunningInstanceMutex.Resolve(Environment.GetCommandLineArgs())); }
            catch (Exception e)
            {
                Debug.Log("[Update] мьютекс запущенной копии не создан: " + e.Message);
            }
        }
    }
}
#endif
