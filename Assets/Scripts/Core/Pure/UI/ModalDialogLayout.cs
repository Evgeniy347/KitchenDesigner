namespace KitchenDesigner.Core.UI
{
    public readonly struct ModalDialogLayout
    {
        private ModalDialogLayout(float titleTop, float bodyTop, float buttonsTop, float height)
        {
            TitleTop = titleTop;
            BodyTop = bodyTop;
            ButtonsTop = buttonsTop;
            Height = height;
        }

        public float TitleTop { get; }

        public float BodyTop { get; }

        public float ButtonsTop { get; }

        public float Height { get; }

        public static ModalDialogLayout For(float titleHeight, float bodyHeight, float buttonHeight, float pad,
            float gap, float gapBeforeButtons)
        {
            float titleTop = pad;
            float bodyTop = titleTop + titleHeight + gap;
            float buttonsTop = bodyTop + bodyHeight + gapBeforeButtons;
            return new ModalDialogLayout(titleTop, bodyTop, buttonsTop, buttonsTop + buttonHeight + pad);
        }
    }
}
