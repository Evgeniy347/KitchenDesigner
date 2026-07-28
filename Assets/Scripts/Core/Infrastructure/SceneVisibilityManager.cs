using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Кто чем владеет по видимости в сцене:
    /// стены и их окна/двери — <see cref="WallManager"/>, полы — CameraController,
    /// всё остальное («объекты») — этот менеджер. Разделение важно: два
    /// компонента, гасящих один и тот же рендерер, дают мигание.</summary>
    public static class SceneVisibility
    {
        /// <summary>Гасит/включает рендеры элемента вместе с потомками — у окна,
        /// двери, ящика геометрия собрана из дочерних объектов, и одного
        /// MeshRenderer на корне недостаточно.</summary>
        public static void SetRenderersEnabled(KitchenElement element, bool enabled)
        {
            if (element == null) return;
            var renderers = element.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = enabled;
        }

        /// <summary>Виден ли элемент сейчас. У окна/двери/ящика геометрия в
        /// потомках, поэтому одного рендерера на корне мало; отсутствие
        /// рендереров вовсе считаем «виден» — гасить нечего.</summary>
        public static bool AnyRendererEnabled(KitchenElement element)
        {
            if (element == null) return false;
            var renderers = element.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) return true;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].enabled) return true;
            return false;
        }

        /// <summary>«Объект» — всё, что не относится к конструкции помещения
        /// (стена, пол) и не является окном или дверью.</summary>
        public static bool IsObject(KitchenElement element)
        {
            if (element == null) return false;
            if (element is WindowElement || element is DoorElement) return false;
            if (element is FloorElement) return false;
            if (element.GetComponent<Wall>() != null) return false;
            if (element.GetComponent<BasePlate>() != null) return false;
            return true;
        }

        /// <summary>Должен ли объект быть виден при текущих настройках.
        /// В фоторежиме сцена цельная — прячем только по «скрыть источники света».</summary>
        public static bool ShouldBeVisible(KitchenElement element, KitchenSettings? s)
        {
            if (s == null) return true;
            if (element is LightSourceElement && s.HideLightSources) return false;
            return PhotoMode.Active || s.ObjectsVisible;
        }
    }

    /// <summary>Применяет настройки «Объекты» и «Скрыть источники света»
    /// к сцене каждый кадр — по тем же правилам, что WallManager для стен.</summary>
    public class SceneVisibilityManager : MonoBehaviour
    {
        public void LateUpdate() => Apply();

        public static void Apply()
        {
            using var _ = PerfMarkers.SceneVisibilityApply.Auto();
            var s = KitchenSettings.Instance;
            foreach (var e in PartRegistry.GetAll())
            {
                if (e == null || !SceneVisibility.IsObject(e)) continue;
                SceneVisibility.SetRenderersEnabled(e, SceneVisibility.ShouldBeVisible(e, s));
            }
        }
    }
}
