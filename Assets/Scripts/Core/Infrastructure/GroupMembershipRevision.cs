namespace KitchenDesigner.Core
{
    public static class GroupMembershipRevision
    {
        public static int Version { get; private set; }

        public static void Bump() => Version++;
    }
}
