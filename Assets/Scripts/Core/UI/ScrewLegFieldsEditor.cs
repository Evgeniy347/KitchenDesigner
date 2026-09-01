using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ScrewLegFieldsEditor : ElementFieldsEditor
    {
        private TMP_Dropdown? _thread;
        private TMP_InputField? _threadLength;
        private TMP_InputField? _insertion;
        private TMP_InputField? _baseDiameter;
        private TMP_InputField? _baseHeight;

        public ScrewLegFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is ScrewLegElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override bool HeightShownFromDimensions => false;

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.ScrewLeg);
            _thread = Rows.Dropdown("Резьба", new List<string>(ScrewLegSpec.Threads),
                OnThreadSelected, visibility, "CtxScrewThread");
            _threadLength = Rows.NumberField("Длина резьбы", visibility);
            _insertion = Rows.NumberField("Заход в корпус", visibility);
            _baseDiameter = Rows.NumberField("Ø основания", visibility);
            _baseHeight = Rows.NumberField("Высота основания", visibility);
        }

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _threadLength;
            yield return _insertion;
            yield return _baseDiameter;
            yield return _baseHeight;
        }

        public override void Show(KitchenElement element)
        {
            if (!(element is ScrewLegElement leg)) return;
            _thread?.SetValueWithoutNotify(IndexOf(leg.Thread));
            if (_threadLength != null) _threadLength.text = leg.ThreadLengthMM.ToString();
            if (_insertion != null) _insertion.text = leg.InsertionDepthMM.ToString();
            if (_baseDiameter != null) _baseDiameter.text = leg.BaseDiameterMM.ToString();
            if (_baseHeight != null) _baseHeight.text = leg.BaseHeightMM.ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (!(element is ScrewLegElement leg)) return;
            Fields.RefreshUnfocused(_threadLength, leg.ThreadLengthMM.ToString());
            Fields.RefreshUnfocused(_insertion, leg.InsertionDepthMM.ToString());
            Fields.RefreshUnfocused(_baseDiameter, leg.BaseDiameterMM.ToString());
            Fields.RefreshUnfocused(_baseHeight, leg.BaseHeightMM.ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (!(element is ScrewLegElement leg)) return;

            if (_baseDiameter != null)
                leg.BaseDiameterMM = Fields.ParseInt(_baseDiameter, leg.BaseDiameterMM);
            if (_baseHeight != null)
                leg.BaseHeightMM = Fields.ParseInt(_baseHeight, leg.BaseHeightMM);

            var height = Host.SizeFields.Height;
            int currentTotal = leg.BodyHeightMM;
            int requestedTotal = Fields.ParseInt(height, currentTotal);
            if (requestedTotal != currentTotal)
                leg.ThreadLengthMM = requestedTotal - leg.BaseHeightMM;
            else if (_threadLength != null)
                leg.ThreadLengthMM = Fields.ParseInt(_threadLength, leg.ThreadLengthMM);

            if (_insertion != null)
                leg.InsertionDepthMM = Fields.ParseInt(_insertion, leg.InsertionDepthMM);

            if (height != null) height.text = leg.BodyHeightMM.ToString();
            WriteBack(leg);
        }

        public override void AfterApply(KitchenElement element)
        {
            if (element is ScrewLegElement leg) WriteBack(leg);
        }

        public override void Track(KitchenElement element)
        {
            var leg = element as ScrewLegElement;
            Fields.Track(_threadLength, leg != null
                ? leg.ThreadLengthMM.ToString()
                : ScrewLegSpec.DEFAULT_THREAD_LENGTH_MM.ToString());
            Fields.Track(_insertion, leg != null
                ? leg.InsertionDepthMM.ToString()
                : ScrewLegSpec.DEFAULT_INSERTION_MM.ToString());
            Fields.Track(_baseDiameter, leg != null
                ? leg.BaseDiameterMM.ToString()
                : ScrewLegSpec.DEFAULT_BASE_DIAMETER_MM.ToString());
            Fields.Track(_baseHeight, leg != null
                ? leg.BaseHeightMM.ToString()
                : ScrewLegSpec.DEFAULT_BASE_HEIGHT_MM.ToString());
        }

        private void WriteBack(ScrewLegElement leg)
        {
            if (_threadLength != null) _threadLength.text = leg.ThreadLengthMM.ToString();
            if (_insertion != null) _insertion.text = leg.InsertionDepthMM.ToString();
            if (_baseDiameter != null) _baseDiameter.text = leg.BaseDiameterMM.ToString();
            if (_baseHeight != null) _baseHeight.text = leg.BaseHeightMM.ToString();
        }

        private static int IndexOf(string thread)
        {
            var normalized = ScrewLegSpec.NormalizeThread(thread);
            for (int i = 0; i < ScrewLegSpec.Threads.Length; i++)
                if (ScrewLegSpec.Threads[i] == normalized) return i;
            return 0;
        }

        private void OnThreadSelected(int index)
        {
            if (!(Host.Target is ScrewLegElement leg)) return;
            if (index < 0 || index >= ScrewLegSpec.Threads.Length) return;
            leg.Thread = ScrewLegSpec.Threads[index];
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(leg);
        }
    }
}
