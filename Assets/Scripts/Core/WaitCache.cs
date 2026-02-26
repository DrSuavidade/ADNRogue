using UnityEngine;
using System.Collections.Generic;

namespace Geneforge.Core.Utils
{
    /// <summary>
    /// Utility that caches WaitForSeconds objects to prevent GC Allocations.
    /// Use: yield return WaitCache.Get(0.1f);
    /// </summary>
    public static class WaitCache
    {
        private static readonly Dictionary<float, WaitForSeconds> _waitDict = new Dictionary<float, WaitForSeconds>();

        public static WaitForSeconds Get(float seconds)
        {
            if (seconds <= 0f) return null;

            if (!_waitDict.TryGetValue(seconds, out var wait))
            {
                wait = new WaitForSeconds(seconds);
                _waitDict.Add(seconds, wait);
            }
            return wait;
        }
    }
}
