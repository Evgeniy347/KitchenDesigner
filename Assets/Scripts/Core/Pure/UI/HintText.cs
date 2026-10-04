using System;
using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core.UI
{
    public static class HintText
    {
        public const string TablePrefix = "hint.";

        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "settings.view.preset",
            "settings.view.walls",
            "settings.view.wallOutline",
            "settings.view.lowerNearWalls",
            "settings.view.lowerAllWalls",
            "settings.view.hideOpeningsOnLoweredWalls",
            "settings.view.objects",
            "settings.view.objectOutline",
            "settings.view.hideLightSources",
            "settings.construction.region",
            "settings.construction.frostDepth",
            "settings.construction.soil",
            "settings.construction.floorHeight",
            "settings.construction.neighbourLevels",
            "settings.construction.masonry",
            "settings.construction.joint",
            "settings.construction.waste",
            "settings.construction.concrete",
            "settings.construction.sand",
            "settings.construction.gravel",
            "settings.construction.compacted",
            "settings.construction.slabThickness",
            "element.floorSlab.technology",
            "element.floorSlab.concrete",
            "element.floorSlab.rebarDiameter",
            "element.floorSlab.rebarStep",
            "element.fence.postSection",
            "element.fence.postStep",
            "element.fence.pitDepth",
            "element.fence.sheetMark",
            "element.duct.profile",
            "element.duct.diameter",
            "element.duct.width",
            "element.duct.height",
            "element.duct.airflow",
            "element.grille.airflow",
            "element.roof.type",
            "element.roof.ridgeAxis",
            "element.roof.pitchDeg",
            "element.roof.overhangMm",
            "element.roof.rafterStepMm",
            "element.wallLayer.thickness",
            "element.wallLayer.battenStep",
            "element.bed.headboard",
            "element.bed.size",
            "element.laundry.kind",
            "element.bathtub.rimWidth",
            "element.bathtub.bowlDepth",
            "element.bathtub.bowlRadius",
            "element.bathtub.bowlFillet",
            "element.showerColumn.wallOffset",
            "element.showerColumn.armReach",
            "element.showerColumn.columnHeight",
            "element.showerColumn.riserDiameter",
            "element.showerColumn.headDiameter",
            "element.showerColumn.headThickness",
            "element.showerColumn.handDiameter",
            "element.showerColumn.hoseLength",
            "element.bathMixer.centres",
            "element.bathMixer.bodyLength",
            "element.bathMixer.bodyDiameter",
            "element.bathMixer.escutcheonReach",
            "element.bathMixer.outletDiameter",
            "element.bathMixer.spout",
            "element.toilet.seatHeight",
            "element.toilet.flushPlate",
            "element.pipe.derived",
            "element.pipe.nominalBore",
            "element.pipeFitting.bore",
            "element.table.legInset",
            "element.seat.seatHeight",
            "element.pouffe.seatThickness",
            "element.light.temperature",
            "element.light.power",
            "element.light.diffusion",
            "element.light.beamAngle",
            "element.light.softness",
            "element.light.upLight",
            "element.light.shape",
            "element.light.shadow",
            "element.light.shadowStrength",
            "element.light.glow",
            "element.light.drop",
            "element.light.upCone",
            "element.light.range",
            "element.light.rangeMax",
            "element.light.upRange",
            "element.light.efficacy",
            "element.light.lumensPerUnit",
            "element.window.tint",
            "element.window.sill",
            "element.window.openingMode",
            "element.door.sash",
            "element.wallDevice.plateWidth",
            "element.wallDevice.plateHeight",
            "element.wallDevice.protrusion",
            "element.wallDevice.posts",
            "element.wallDevice.powered",
            "element.facade.doorMode",
            "element.assembled.fill",
            "element.drawer.type",
            "element.drawer.length",
            "element.drawer.color",
            "element.drawer.upperLength",
            "element.drawer.boxWidth",
            "element.screwLeg.baseDiameter",
            "element.screwLeg.thread",
            "element.screwLeg.threadLength",
            "element.screwLeg.baseHeight",
            "element.pillar.diameter",
            "element.pillar.midHeight",
            "element.cooktop.cutout",
            "element.radial.cornerRadius",
            "element.shape.cornerRadius",
            "element.wall.loadBearing",
            "element.wall.masonry",
            "element.wall.joint",
            "element.wall.waste",
            "element.foundation.soil",
            "element.foundation.frostDepth",
            "element.foundation.sand",
            "element.foundation.gravel",
            "element.foundation.compacted",
            "element.foundation.concrete",
            "element.foundation.rebarDiameter",
            "element.foundation.rebarStep",
            "element.foundation.cover",
            "settings.photo.ceiling",
            "settings.photo.ssgi",
            "settings.photo.hdr",
            "settings.photo.lightsPerObject",
            "settings.photo.aoFullRes",
            "settings.photo.aoDirect",
            "settings.photo.aoFalloff",
            "settings.photo.shadows",
            "settings.photo.softShadows",
            "settings.photo.antiAliasing",
            "settings.photo.supersampling",
            "settings.photo.ambientOcclusion",
            "settings.photo.bloom",
            "settings.photo.vignette",
            "settings.photo.renderScale",
            "settings.photo.shadowMap",
            "settings.photo.aoIntensity",
            "settings.photo.aoRadius",
            "settings.photo.ssgiStrength",
            "settings.photo.ssgiRadius",
            "settings.photo.ssgiSamples",
            "settings.photo.ssgiResolution",
            "settings.photo.ssgiBlur",
            "settings.light.ambient",
            "settings.light.exposure",
            "settings.light.contrast",
            "settings.light.saturation",
            "settings.light.bloomStrength",
            "settings.light.bloomThreshold",
            "settings.light.bloomClamp",
            "settings.light.vignetteStrength",
            "settings.light.sunShadowStrength",
            "settings.light.shadowDistance",
            "settings.light.lampShadows",
            "settings.light.floorBounce",
            "settings.light.sky",
            "settings.light.equator",
            "settings.light.bounceMax",
            "settings.light.tonemap",
            "settings.control.mouseSensitivity",
            "settings.control.wasdSpeed",
            "settings.control.arrowSpeed",
            "settings.control.mouseInvertX",
            "settings.control.mouseInvertY",
            "settings.control.perfRecordingFile",
            "settings.project.grid",
            "settings.project.gridStep",
            "settings.project.snap",
            "settings.project.snapThreshold",
            "settings.project.distanceGuides",
            "settings.project.blockOnViolation",
            "settings.project.autoSave",
            "settings.project.autoSaveInterval",
            "settings.project.cameraPanFree",
            "settings.project.spatialGrid",
            "settings.project.edgePartialThreshold",
        };

        private static Snapshot? _snapshot;

        public static IReadOnlyDictionary<string, string> All
        {
            get
            {
                int revision = Loc.Revision;
                var cached = _snapshot;
                if (cached != null && cached.Revision == revision) return cached.Texts;
                var built = new Snapshot(revision, Keys.ToDictionary(k => k, Text, StringComparer.Ordinal));
                _snapshot = built;
                return built.Texts;
            }
        }

        public static string TableKey(string key) => TablePrefix + key;

        public static bool Has(string key) => Keys.Contains(key, StringComparer.Ordinal);

        public static string Of(string key)
        {
            if (Has(key)) return Text(key);
            throw new KeyNotFoundException(
                "Нет текста подсказки для ключа «" + key + "» — внесите ключ в HintText.Keys, а текст "
                + TableKey(key) + " в Localization/ru.json и en.json");
        }

        private static string Text(string key) => Loc.T(TableKey(key));

        private sealed class Snapshot
        {
            public readonly int Revision;
            public readonly IReadOnlyDictionary<string, string> Texts;

            public Snapshot(int revision, IReadOnlyDictionary<string, string> texts)
            {
                Revision = revision;
                Texts = texts;
            }
        }
    }
}
