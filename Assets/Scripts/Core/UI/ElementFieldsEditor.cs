using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal enum DimensionPolicy
    {
        FromFields,
        KeepDepth,
        Computed,
    }

    internal abstract class ElementFieldsEditor
    {
        public const string DefaultHeightLabel = "Высота";

        protected ElementFieldsEditor(IContextMenuHost host) => Host = host;

        protected IContextMenuHost Host { get; }

        protected ContextMenuFieldTracker Fields => Host.Fields;

        protected ContextMenuRowFactory Rows => Host.Rows;

        public abstract bool Handles(KitchenElement element);

        public abstract void Build();

        public virtual DimensionPolicy Dimensions => DimensionPolicy.FromFields;

        public virtual bool WidthEditable => true;

        public virtual bool HeightEditable => true;

        public virtual bool DepthEditable => true;

        public virtual bool HeightShownFromDimensions => true;

        public virtual string HeightLabel => DefaultHeightLabel;

        public virtual void Show(KitchenElement element) { }

        public virtual void Apply(KitchenElement element) { }

        public virtual void ApplyAfterPosition(KitchenElement element) { }

        public virtual void Refresh(KitchenElement element) { }

        public virtual void Track(KitchenElement element) { }

        public virtual void AfterApply(KitchenElement element) { }

        public virtual IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield break;
        }
    }
}
