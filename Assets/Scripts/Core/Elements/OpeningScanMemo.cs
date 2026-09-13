using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class OpeningScanMemo
    {
        private readonly OpeningScanMemory _obstacles = new OpeningScanMemory();
        private OpeningScanKey _key;
        private bool _known;
        private float _safeProgress = 1f;

        public float SafeProgress => _known ? _safeProgress : 1f;

        public void Forget()
        {
            _known = false;
            _safeProgress = 1f;
            _obstacles.Forget();
        }

        public bool Repeats(in OpeningScanKey key) => _known && _key.Matches(key);

        public bool StillBlocksAt(in OpeningScanKey key, float progress) =>
            Repeats(key) && Mathf.Approximately(progress, _safeProgress);

        public float SafeProgressFor(KitchenElement self, in OpeningScanKey key,
            OpenBoxes getBoxes, List<KitchenElement>? exclude = null)
        {
            if (Repeats(key)) return _safeProgress;

            _safeProgress = OpeningCollision.FindMaxProgress(self, getBoxes, exclude,
                memory: _obstacles);
            _key = key;
            _known = true;
            return _safeProgress;
        }
    }
}
