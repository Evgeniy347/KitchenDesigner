using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsAboutTab
    {
        public void Build(Transform page, float topY)
        {
            UIFactory.CreateLabel("AboutVersion", page, $"Версия: {BuildInfo.Version}", 18,
                new Vector2(0, topY), new Vector2(SettingsRowFactory.ContentW, 32),
                TextAnchor.MiddleCenter);

            UIFactory.CreateLabel("AboutDate", page, $"Сборка: {BuildInfo.BuildDate}",
                UIStyle.FontBody, new Vector2(0, topY - SettingsRowFactory.RowStep),
                new Vector2(SettingsRowFactory.ContentW, 28), TextAnchor.MiddleCenter);
        }
    }
}
