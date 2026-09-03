namespace KitchenDesigner.Core
{
    public struct SceneClickInput
    {
        public bool HasElement;
        public bool Interactable;
        public bool ModuleEditActive;
        public bool ModuleEditable;
        public bool CtrlHeld;
        public bool InGroup;
        public bool RepeatsGroup;
        public bool RepeatsElement;
        public bool InMultiSelection;
        public ActivationKind Kind;
    }
}
