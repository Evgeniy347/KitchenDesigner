using System;

namespace KitchenDesigner.Core.UI
{
    public sealed class ModalDialogContent
    {
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string PrimaryCaption { get; set; } = "";
        public Action? OnPrimary { get; set; }
        public string? SecondaryCaption { get; set; }
        public Action? OnSecondary { get; set; }
        public bool Danger { get; set; }
    }
}
