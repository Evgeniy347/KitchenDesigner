namespace KitchenDesigner.Core
{
    public static class SceneClickPlan
    {
        public static SceneClickAction Decide(in SceneClickInput input)
        {
            if (!input.HasElement || !input.Interactable)
                return input.CtrlHeld ? SceneClickAction.Ignore : SceneClickAction.DeselectAll;

            if (input.ModuleEditActive)
            {
                if (!input.ModuleEditable) return SceneClickAction.Ignore;
                return input.CtrlHeld
                    ? SceneClickAction.ModuleToggleInSelection
                    : SceneClickAction.ModuleSelect;
            }

            if (input.CtrlHeld) return SceneClickAction.ToggleInSelection;

            if (input.InGroup)
                return input.RepeatsGroup
                    ? SceneClickAction.EnterModuleEdit
                    : SceneClickAction.SelectGroup;

            if (input.RepeatsElement && ActivationRules.RespondsToDoubleClick(input.Kind))
                return SceneClickAction.ActivateSwitch;

            if (input.InMultiSelection) return SceneClickAction.CollapseToClicked;

            return SceneClickAction.Select;
        }

        public static bool RecordsClick(SceneClickAction action)
            => action != SceneClickAction.Ignore
                && action != SceneClickAction.DeselectAll
                && action != SceneClickAction.ModuleSelect
                && action != SceneClickAction.ModuleToggleInSelection;

        public static bool KeepsSelectionSnapshot(SceneClickAction action)
            => action == SceneClickAction.ActivateSwitch;
    }
}
