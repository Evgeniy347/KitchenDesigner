using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Ports;
using KitchenDesigner.Core.Ventilation;

namespace KitchenDesigner.Core.Analysis
{
    public static class SceneDuctSnapshot
    {
        public static DuctSurvey Survey(IReadOnlyList<KitchenElement> all)
        {
            var ports = new List<Port>();
            var ducts = new List<DuctRun>();
            var grilles = new List<GrilleRun>();
            var rooms = new List<RoomFootprint>();
            if (all == null) return DuctSurvey.Of(ports, ducts, grilles, rooms);

            foreach (var e in all)
            {
                if (e == null) continue;

                if (e is DuctElement duct)
                {
                    string profileId = duct.Profile.ProfileId;
                    var a = ToMm(duct.EndAUnits);
                    var b = ToMm(duct.EndBUnits);
                    var along = AxisOf(duct.RunAxis);

                    int portAIndex = ports.Count;
                    ports.Add(new Port(duct.PartName, 0, a, AxisOf(-duct.RunAxis), profileId));
                    int portBIndex = ports.Count;
                    ports.Add(new Port(duct.PartName, 1, b, along, profileId));

                    ducts.Add(new DuctRun(duct.PartName, duct.Profile, duct.AirflowM3PerHour,
                        portAIndex, portBIndex));
                }
                else if (e is GrilleElement grille)
                {
                    grilles.Add(new GrilleRun(grille.PartName, ToMm(grille.transform.position),
                        grille.AirflowM3PerHour));
                }
                else if (e is FloorElement floor)
                {
                    rooms.Add(RoomOf(floor));
                }
            }

            return DuctSurvey.Of(ports, ducts, grilles, rooms);
        }

        private static RoomFootprint RoomOf(FloorElement floor)
        {
            var centreMm = ToMm(floor.transform.position);
            float halfWidthMm = floor.DimensionsMM.x * 0.5f;
            float halfDepthMm = floor.DimensionsMM.z * 0.5f;
            float heightMm = KitchenSettings.Instance.ConstructionFloorHeightMm;

            return new RoomFootprint(floor.PartName,
                centreMm.XMm - halfWidthMm, centreMm.XMm + halfWidthMm,
                centreMm.ZMm - halfDepthMm, centreMm.ZMm + halfDepthMm, heightMm);
        }

        private static PipeAxis AxisOf(Vector3 direction) =>
            new PipeAxis(direction.x, direction.y, direction.z);

        private static PointMm ToMm(Vector3 units)
        {
            float toMm = 1f / AppConstants.MM_TO_UNITS;
            return new PointMm(units.x * toMm, units.y * toMm, units.z * toMm);
        }
    }
}
