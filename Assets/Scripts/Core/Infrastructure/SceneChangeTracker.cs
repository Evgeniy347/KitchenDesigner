using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Превращает изменения поз в версии: раз в кадр опрашивает встроенный
    /// флаг <c>Transform.hasChanged</c>, который Unity сама взводит при любой записи в
    /// трансформ, и двигает <see cref="SceneRevision"/> плюс персональную
    /// <see cref="KitchenElement.PoseVersion"/> сдвинувшихся элементов.
    ///
    /// Это единственное место, где допустим покадровый обход реестра: чтение bool у
    /// каждого элемента стоит микросекунды, а взамен все остальные системы перестают
    /// каждый кадр искать стену, хозяина или пару по имени.
    ///
    /// Флаг сбрасывается здесь и только здесь — потребители сравнивают версии, а не
    /// читают <c>hasChanged</c> сами, иначе первый же прочитавший «съел» бы сигнал у
    /// остальных.</summary>
    public class SceneChangeTracker : MonoBehaviour
    {
        // Порядок в кадре не важен: потребители сравнивают версии, поэтому изменение,
        // замеченное в LateUpdate, будет обработано в следующем кадре.
        private void LateUpdate() => Poll();

        /// <summary>Опрашивает позы немедленно. Публичный метод нужен тестам и коду,
        /// которому результат перемещения нужен в этом же кадре.</summary>
        public static void Poll()
        {
            bool any = false;
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null) continue;

                var t = e.transform;
                if (!t.hasChanged) continue;

                t.hasChanged = false;
                e.BumpPoseVersion();
                any = true;
            }

            if (any) SceneRevision.Bump();
        }
    }
}
