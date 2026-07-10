using System.Collections;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SnapVisualizer : MonoBehaviour
    {
        private LineRenderer _line;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.startWidth = 0.002f;
            _line.endWidth = 0.002f;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.textureMode = LineTextureMode.Tile;
            _line.enabled = false;
        }

        public void ShowProximity(Vector3 from, Vector3 to)
        {
            _line.enabled = true;
            _line.startColor = Color.yellow;
            _line.endColor = Color.yellow;
            _line.SetPosition(0, from);
            _line.SetPosition(1, to);
        }

        public void ShowSnap(Vector3 from, Vector3 to)
        {
            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashGreen(from, to));
        }

        private IEnumerator FlashGreen(Vector3 from, Vector3 to)
        {
            _line.enabled = true;
            _line.startColor = Color.green;
            _line.endColor = Color.green;
            _line.SetPosition(0, from);
            _line.SetPosition(1, to);

            yield return new WaitForSeconds(0.5f);

            _line.enabled = false;
            _flashRoutine = null;
        }

        public void Hide()
        {
            if (_flashRoutine != null)
                return;

            _line.enabled = false;
        }
    }
}
