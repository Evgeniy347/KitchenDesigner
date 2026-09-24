using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class RafterSectionTable
    {
        public const string Source =
            "СП 64.13330.2017 «Деревянные конструкции», подбор сечения стропильной ноги по "
            + "пролёту (пункт не подтверждён исполнителем — см. NormativeUnverified)";

        public static readonly IReadOnlyList<RafterSection> Table = new[]
        {
            new RafterSection(3000f, 50f, 150f, Source),
            new RafterSection(4500f, 50f, 200f, Source),
            new RafterSection(6000f, 50f, 250f, Source),
        };

        public static RafterSection ForSpan(float spanMm)
        {
            foreach (var section in Table)
                if (spanMm <= section.MaxSpanMm) return section;
            return Table[Table.Count - 1];
        }
    }
}
