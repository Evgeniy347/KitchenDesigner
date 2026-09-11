using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public readonly struct ElementFront
    {
        private static readonly string[] NoParts = Array.Empty<string>();

        private readonly string? _reason;
        private readonly string[]? _partNames;

        private ElementFront(string? reason, string[]? partNames)
        {
            _reason = reason;
            _partNames = partNames;
        }

        public static ElementFront NoSeparateFacePart(string reason) =>
            new ElementFront(reason, NoParts);

        public static ElementFront Parts(params string[] partNames) =>
            new ElementFront(null, partNames);

        public string Reason => _reason ?? "";

        public IReadOnlyList<string> PartNames => _partNames ?? NoParts;

        public bool ShowsNamedParts => PartNames.Count > 0;
    }
}
