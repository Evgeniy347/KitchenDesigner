using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class UnknownTypeMarker : MonoBehaviour
    {
        [SerializeField] private string _typeId = "";
        [SerializeField] private string _rawRecord = "";

        public string TypeId => _typeId;

        public string RawRecord => _rawRecord;

        public static void Attach(GameObject target, string typeId, string? rawRecord)
        {
            if (target == null) return;
            var marker = target.GetComponent<UnknownTypeMarker>();
            if (marker == null) marker = target.AddComponent<UnknownTypeMarker>();
            marker._typeId = typeId ?? "";
            marker._rawRecord = rawRecord ?? "";
        }

        public static UnknownTypeMarker? On(KitchenElement element) =>
            element == null ? null : element.GetComponent<UnknownTypeMarker>();
    }
}
