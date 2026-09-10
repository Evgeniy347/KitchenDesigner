namespace KitchenDesigner.Core.Construction
{
    public readonly struct WallQuantitiesResult
    {
        public readonly MasonryTechnology Technology;
        public readonly MasonryCounting Counting;
        public readonly double GrossVolumeM3;
        public readonly double OpeningsVolumeM3;
        public readonly double NetVolumeM3;
        public readonly int PiecesLaid;
        public readonly int Pieces;
        public readonly double MortarM3;
        public readonly double TimberM3;
        public readonly int Studs;
        public readonly double StudMetres;
        public readonly double PlateMetres;

        public WallQuantitiesResult(MasonryTechnology technology, MasonryCounting counting,
            double grossVolumeM3, double openingsVolumeM3, double netVolumeM3,
            int piecesLaid, int pieces, double mortarM3, double timberM3,
            int studs, double studMetres, double plateMetres)
        {
            Technology = technology;
            Counting = counting;
            GrossVolumeM3 = grossVolumeM3;
            OpeningsVolumeM3 = openingsVolumeM3;
            NetVolumeM3 = netVolumeM3;
            PiecesLaid = piecesLaid;
            Pieces = pieces;
            MortarM3 = mortarM3;
            TimberM3 = timberM3;
            Studs = studs;
            StudMetres = studMetres;
            PlateMetres = plateMetres;
        }

        public double RunningMetres => StudMetres + PlateMetres;
    }
}
