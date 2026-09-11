using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public readonly struct RegionClimate
    {
        public const string Source = "СП 131.13330.2020, таблица 5.1";
        public const int MonthsPerYear = 12;

        public readonly ConstructionRegion Region;
        public readonly string ReferenceStation;
        public readonly IReadOnlyList<float> MonthlyMeanDeg;

        public RegionClimate(ConstructionRegion region, string referenceStation,
            IReadOnlyList<float> monthlyMeanDeg)
        {
            Region = region;
            ReferenceStation = referenceStation;
            MonthlyMeanDeg = monthlyMeanDeg;
        }

        public double NegativeMonthSumDeg
        {
            get
            {
                double sum = 0d;
                var months = MonthlyMeanDeg;
                if (months == null) return sum;
                for (int i = 0; i < months.Count; i++)
                    if (months[i] < 0f)
                        sum -= months[i];
                return sum;
            }
        }

        public static readonly IReadOnlyList<RegionClimate> Table = new[]
        {
            new RegionClimate(ConstructionRegion.Centre, "Москва", new[]
            {
                -7.8f, -6.9f, -1.3f, 6.5f, 13.3f, 17.0f, 19.1f, 17.1f, 11.3f, 5.2f, -0.8f, -5.2f,
            }),
            new RegionClimate(ConstructionRegion.NorthWest, "Санкт-Петербург", new[]
            {
                -6.5f, -6.1f, -1.4f, 4.6f, 11.3f, 15.8f, 18.6f, 16.9f, 11.6f, 5.8f, 0.5f, -3.6f,
            }),
            new RegionClimate(ConstructionRegion.South, "Ростов-на-Дону", new[]
            {
                -3.8f, -3.0f, 2.4f, 10.9f, 17.1f, 21.3f, 23.5f, 22.8f, 16.8f, 9.6f, 3.4f, -1.2f,
            }),
            new RegionClimate(ConstructionRegion.Volga, "Самара", new[]
            {
                -11.1f, -10.4f, -3.7f, 7.2f, 15.3f, 19.2f, 21.3f, 19.5f, 13.4f, 5.4f, -2.1f, -8.3f,
            }),
            new RegionClimate(ConstructionRegion.Urals, "Екатеринбург", new[]
            {
                -13.8f, -11.7f, -4.1f, 4.5f, 11.4f, 16.6f, 18.6f, 15.8f, 10.0f, 2.5f, -5.5f, -11.2f,
            }),
            new RegionClimate(ConstructionRegion.Siberia, "Новосибирск", new[]
            {
                -17.6f, -15.8f, -8.0f, 2.7f, 11.0f, 17.3f, 19.4f, 16.3f, 10.2f, 2.6f, -7.3f, -14.4f,
            }),
            new RegionClimate(ConstructionRegion.FarEast, "Хабаровск", new[]
            {
                -20.2f, -16.0f, -6.6f, 4.6f, 12.4f, 18.0f, 21.4f, 19.7f, 13.6f, 4.9f, -7.2f, -17.7f,
            }),
            new RegionClimate(ConstructionRegion.FarNorth, "Воркута", new[]
            {
                -20.4f, -20.0f, -14.2f, -9.4f, -2.1f, 7.3f, 13.0f, 9.5f, 4.4f, -4.2f, -12.8f, -16.7f,
            }),
        };

        public static bool TryOf(ConstructionRegion region, out RegionClimate climate)
        {
            for (int i = 0; i < Table.Count; i++)
            {
                if (Table[i].Region != region) continue;
                climate = Table[i];
                return true;
            }

            climate = default;
            return false;
        }
    }
}
