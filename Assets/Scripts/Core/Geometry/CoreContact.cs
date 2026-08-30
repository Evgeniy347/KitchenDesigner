namespace KitchenDesigner.Core
{
    public readonly struct CoreContact
    {
        public readonly int A;
        public readonly int B;
        public readonly int FaceA;
        public readonly int FaceB;
        public readonly float Area;
        public readonly bool IsFaceToFace;

        public CoreContact(int a, int b, int faceA, int faceB, float area, bool isFaceToFace)
        {
            A = a; B = b; FaceA = faceA; FaceB = faceB;
            Area = area; IsFaceToFace = isFaceToFace;
        }
    }
}
