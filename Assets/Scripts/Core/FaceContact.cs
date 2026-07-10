namespace KitchenDesigner.Core
{
    public struct FaceContact
    {
        public KitchenElement elementA;
        public KitchenElement elementB;
        public int faceA;
        public int faceB;
        public float contactArea;
        public bool isFaceToFace;

        public FaceContact(KitchenElement a, KitchenElement b, int fa, int fb, float area, bool faceToFace)
        {
            elementA = a;
            elementB = b;
            faceA = fa;
            faceB = fb;
            contactArea = area;
            isFaceToFace = faceToFace;
        }
    }
}
