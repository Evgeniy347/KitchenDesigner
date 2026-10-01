namespace KitchenDesigner.Core
{
    public readonly struct LanguageInfo
    {
        public readonly string Code;
        public readonly string NativeName;
        public readonly bool IsRightToLeft;

        public LanguageInfo(string code, string nativeName, bool isRightToLeft)
        {
            Code = code;
            NativeName = nativeName;
            IsRightToLeft = isRightToLeft;
        }
    }
}
