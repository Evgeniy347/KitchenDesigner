using System;

namespace KitchenDesigner.Core.UI
{
    internal readonly struct RowVisibility
    {
        private readonly ElementFacet _facet;
        private readonly ElementFacet _except;
        private readonly Func<bool>? _when;

        private RowVisibility(ElementFacet facet, ElementFacet except, Func<bool>? when)
        {
            _facet = facet;
            _except = except;
            _when = when;
        }

        public static RowVisibility Always => new(ElementFacet.None, ElementFacet.None, null);

        public static RowVisibility When(Func<bool> when) =>
            new(ElementFacet.None, ElementFacet.None, when);

        public static RowVisibility For(ElementFacet facet) =>
            new(facet, ElementFacet.None, null);

        public static RowVisibility For(ElementFacet facet, Func<bool> when) =>
            new(facet, ElementFacet.None, when);

        public static RowVisibility ForExcept(ElementFacet facet, ElementFacet except) =>
            new(facet, except, null);

        public bool IsAlways => _facet == ElementFacet.None && _except == ElementFacet.None && _when == null;

        public bool IsVisibleFor(ElementFacet facets) =>
            (_facet == ElementFacet.None || (facets & _facet) != ElementFacet.None)
            && (facets & _except) == ElementFacet.None
            && (_when == null || _when());
    }
}
