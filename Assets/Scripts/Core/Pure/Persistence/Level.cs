namespace KitchenDesigner.Core
{
    [System.Serializable]
    public class Level
    {
        public string id = "";
        public string name = "";
        public int floorElevationMm;
        public int heightMm;

        public Level() { }

        public Level(string id, string name, int floorElevationMm, int heightMm)
        {
            this.id = id ?? "";
            this.name = name ?? "";
            this.floorElevationMm = floorElevationMm;
            this.heightMm = heightMm;
        }
    }
}
