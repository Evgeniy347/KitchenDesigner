using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class SofaUnfoldMotion
    {
        public float Progress { get; private set; }

        public SofaStage Target { get; private set; }

        public bool IsMoving => Progress != SofaUnfold.ProgressOf(Target);

        public float RemainingSeconds
            => Mathf.Abs(SofaUnfold.ProgressOf(Target) - Progress) * SofaUnfold.SecondsPerStage;

        public void GoTo(SofaStage target) => Target = target;

        public void Snap(SofaStage stage)
        {
            Target = stage;
            Progress = SofaUnfold.ProgressOf(stage);
        }

        public bool Advance(float seconds)
        {
            Progress = Mathf.MoveTowards(Progress, SofaUnfold.ProgressOf(Target),
                Mathf.Max(0f, seconds) / SofaUnfold.SecondsPerStage);
            return IsMoving;
        }
    }
}
