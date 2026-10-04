namespace KitchenDesigner.Core.UI
{
    public readonly struct ModalDialogLayout
    {
        private ModalDialogLayout(float titleTop, float bodyTop, float extraTop, float noteTop,
            float buttonsTop, float height)
        {
            TitleTop = titleTop;
            BodyTop = bodyTop;
            ExtraTop = extraTop;
            NoteTop = noteTop;
            ButtonsTop = buttonsTop;
            Height = height;
        }

        public float TitleTop { get; }

        public float BodyTop { get; }

        public float ExtraTop { get; }

        public float NoteTop { get; }

        public float ButtonsTop { get; }

        public float Height { get; }

        public static ModalDialogLayout For(float titleHeight, float bodyHeight, float extraHeight,
            float noteHeight, float buttonHeight, float pad, float gap, float gapBeforeButtons)
        {
            float titleTop = pad;
            float bodyTop = titleTop + titleHeight + gap;
            float extraTop = bodyTop + bodyHeight + (extraHeight > 0f ? gap : 0f);
            float noteTop = extraTop + extraHeight + (noteHeight > 0f ? gap : 0f);
            float buttonsTop = noteTop + noteHeight + gapBeforeButtons;
            return new ModalDialogLayout(titleTop, bodyTop, extraTop, noteTop, buttonsTop,
                buttonsTop + buttonHeight + pad);
        }
    }
}
