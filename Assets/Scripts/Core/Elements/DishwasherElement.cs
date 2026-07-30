using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Встраиваемая посудомоечная машина Bosch SMV25EX02E — третья готовая модель
    /// группы «Техника» (docs/APPLIANCES-BRIEF.md §3). Габариты заданы
    /// производителем и не редактируются (<see cref="IFixedSizeElement"/>): поля
    /// Ш/В/Г в окне свойств серые, ручек ресайза нет, edit_elements правку
    /// размера отклоняет.
    ///
    /// ДВА параллелепипеда, оба — дети при ЕДИНИЧНОМ масштабе корня:
    ///   0 «Body»         — корпус 598×815×550, тёмно-серый;
    ///   1 «ControlPanel» — узкая чёрная полоса панели управления.
    ///
    /// ГЛАВНОЕ ОТЛИЧИЕ от варочной и духовки: машина ПОЛНОВСТРАИВАЕМАЯ, своей
    /// фасадной панели у неё НЕТ. Лицо ей делает обычный мебельный фасад, который
    /// пользователь пристёгивает по имени — ровно как у ящика (<see cref="IFacadeHost"/>,
    /// <see cref="DrawerLinks.Rename"/>, <see cref="DrawerLinks.IsFacadeInContact"/>).
    /// Поэтому фасада в этой модели и нет: включить его сюда значило бы нарисовать
    /// прибору вторую переднюю плоскость поверх пристёгнутой.
    ///
    /// Как и духовка, машина НЕ врезная: не <see cref="IPartCutout"/>, ни к какой
    /// детали не привязана, проёмов не режет — обычный корпусный элемент со
    /// штатными снапами и валидацией.
    /// </summary>
    public class DishwasherElement : KitchenElement, IFixedSizeElement, IFacadeHost
    {
        /// <summary>Единственная модель — см. <see cref="ApplianceModels.All"/>.</summary>
        public const string MODEL = "Bosch SMV25EX02E";

        // ── Корпус ──────────────────────────────────────────────────────
        public const int BODY_WIDTH_MM = 598;
        public const int BODY_DEPTH_MM = 550;

        /// <summary>Диапазон высоты корпуса на регулируемых ножках.</summary>
        public const int HEIGHT_MIN_MM = 815;
        public const int HEIGHT_MAX_MM = 875;

        /// <summary>НОМИНАЛЬНАЯ высота корпуса — ножки завинчены до упора.
        ///
        /// Выбраны 815 (нижняя граница и номинал DNS), и это не «взяли первое
        /// число»: вся арифметика ниши сходится именно на границах диапазона.
        /// Фасад пристёгивается от верха цоколя до верха корпуса, значит
        /// «цоколь = высота корпуса − высота фасада». Подставляя границы:
        ///   815 − 725 = 90  = минимальный цоколь;
        ///   875 − 655 = 220 = максимальный цоколь.
        /// То есть диапазон фасада 655–725 и диапазон цоколя 90–220 — это ОДИН
        /// и тот же диапазон высоты, пересчитанный через корпус, и 815 стоит
        /// ровно на его нижнем конце. На номинальном фасаде 720 цоколь выходит
        /// 95 (а не минимальные 90) — фасад упирается в столешницу 820 с зазором
        /// 5 мм под ней, что для встраиваемой техники и нужно.
        ///
        /// Размер фиксирован, поэтому «настроить ножки» пользователь не может:
        /// у прибора берётся паспортный номинал, а подъём до 875 — дело монтажа,
        /// а не модели.</summary>
        public const int BODY_HEIGHT_MM = HEIGHT_MIN_MM;

        // ── Ниша ────────────────────────────────────────────────────────
        public const int NICHE_WIDTH_MM = 600;
        public const int NICHE_MIN_DEPTH_MM = 550;

        // ── Мебельный фасад (пристёгивается, в модель НЕ входит) ─────────
        /// <summary>Ширина фасада — по ширине ниши.</summary>
        public const int FACADE_WIDTH_MM = NICHE_WIDTH_MM;

        public const int FACADE_MIN_HEIGHT_MM = 655;
        public const int FACADE_MAX_HEIGHT_MM = 725;

        /// <summary>Номинал под столешницу 820 (720 фасада + 95 цоколя + 5 мм
        /// зазора под столешницей = 820).</summary>
        public const int FACADE_NOMINAL_HEIGHT_MM = 720;

        // ── Цоколь под фасадом ──────────────────────────────────────────
        public const int PLINTH_MIN_MM = 90;
        public const int PLINTH_MAX_MM = 220;

        /// <summary>Со схемы: ниша под цоколь 89 мм.
        ///
        /// РАСХОЖДЕНИЕ В БРИФЕ на 1 мм: та же схема подписывает цоколь «min 90».
        /// Спорить тут не о чем — 89 это проём ниши, 90 это цоколь, который в
        /// него встаёт, и лишний миллиметр съедает допуск. Ограничением берём
        /// 90 (<see cref="PLINTH_MIN_MM"/>): проверка на «цоколь мельче
        /// допустимого» должна опираться на сам цоколь, а не на его нишу.</summary>
        public const int PLINTH_NICHE_MM = 89;

        /// <summary>Со схемы: отступ низа корпуса от плоскости фасада, мм —
        /// цоколь стоит не заподлицо с фасадом, а утоплен под него. Модель
        /// (две коробки) этого не рисует; число нужно тому, кто ставит цоколь,
        /// и отдаётся через MCP.</summary>
        public const int PLINTH_SETBACK_MM = 53;

        /// <summary>Со схемы: выступ ножек вперёд, мм.</summary>
        public const int FEET_PROTRUSION_MM = 100;

        // ── Панель управления ───────────────────────────────────────────
        /// <summary>Высота чёрной полосы панели управления. На настоящей машине
        /// панель лежит на ВЕРХНЕМ ТОРЦЕ двери — при закрытой двери её видно
        /// только сверху, а сверху у встроенной машины столешница. Рисуем её
        /// самой верхней полосой переда: так прибор узнаётся, и «полновстраиваемая,
        /// органов управления на лице нет» не превращается в «лицо пустое».</summary>
        public const int CONTROL_PANEL_HEIGHT_MM = 14;

        /// <summary>Толщина накладной полосы: она лежит заподлицо с передней
        /// плоскостью корпуса, а не поверх неё, — иначе прибор вышел бы за
        /// собственный габарит.</summary>
        public const float OVERLAY_THICKNESS_MM = 2f;

        /// <summary>Габарит прибора: 598 × 815 × 550. Ниша (600) и фасад в него
        /// НЕ входят — коробка описывает сам прибор, как у духовки.</summary>
        public static Vector3Int ModelDimensionsMM =>
            new Vector3Int(BODY_WIDTH_MM, BODY_HEIGHT_MM, BODY_DEPTH_MM);

        /// <summary>Высота цоколя, которая получается под фасадом такой высоты:
        /// фасад стоит от верха цоколя до верха корпуса.</summary>
        public static int PlinthForFacade(int facadeHeightMM) => BODY_HEIGHT_MM - facadeHeightMM;

        /// <summary>Высота фасада в допустимом для модели диапазоне 655–725.</summary>
        public static bool IsFacadeHeightValid(int facadeHeightMM) =>
            facadeHeightMM >= FACADE_MIN_HEIGHT_MM && facadeHeightMM <= FACADE_MAX_HEIGHT_MM;

        [SerializeField] private string _attachedFacadeName = "";

        private readonly List<GameObject> _children = new List<GameObject>();

        /// <summary>Габариты заданы производителем — всегда и у любого экземпляра.</summary>
        public bool HasFixedSize => true;

        /// <summary>Имя пристёгнутого мебельного фасада. Пометка та же, что у
        /// ящика: связь ведёт <see cref="DrawerLinks"/>, а не правка свойств —
        /// откат делают Create/DeleteCommand самого фасада.</summary>
        [NotUndoable("обратная ссылка на фасад, ведёт DrawerLinks")]
        public string AttachedFacadeName
        {
            get => _attachedFacadeName;
            set => _attachedFacadeName = value ?? "";
        }

        /// <summary>Пристёгнутый фасад по имени, либо null. Удалённый фасад
        /// выпадает из PartRegistry — ссылка перестаёт находиться сама, и
        /// висячего указателя на объект не остаётся (а сам факт «имя есть,
        /// фасада нет» ловит анализатор сцены кодом DWH-02).</summary>
        public FacadeElement? FindAttachedFacade()
        {
            if (string.IsNullOrEmpty(_attachedFacadeName)) return null;
            foreach (var e in PartRegistry.All)
                if (e is FacadeElement f && f.PartName == _attachedFacadeName) return f;
            return null;
        }

        protected override Vector3 EffectiveScale => new Vector3(
            BODY_WIDTH_MM * AppConstants.MM_TO_UNITS,
            BODY_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            BODY_DEPTH_MM * AppConstants.MM_TO_UNITS);

        public override void ApplyDimensions()
        {
            // Единичный масштаб корня: геометрия детей задана в мировых единицах,
            // иначе они масштабировались бы дважды.
            transform.localScale = Vector3.one;

            // Габарит возвращается на место ЗДЕСЬ, а не в сеттерах: через
            // ApplyDimensions проходит любой путь правки (окно свойств,
            // ResizeCommand, MCP, загрузка сейва), и одна строка запирает все.
            Data.DimensionsMM = ModelDimensionsMM;

            UpdateCollider();
            if (SuppressVisualRebuild) return;
            EnsureChildren();
            RebuildGeometry();
        }

        // ── Геометрия ───────────────────────────────────────────────────

        private const int IdxBody = 0;
        private const int IdxPanel = 1;
        private const int ChildCount = 2;

        private static string ChildName(int idx) => idx == IdxBody ? "Body" : "ControlPanel";

        private void UpdateCollider()
        {
            var existing = GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.size = EffectiveScale;
            box.center = Vector3.zero;
        }

        private void EnsureChildren()
        {
            while (_children.Count < ChildCount)
            {
                int idx = _children.Count;
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = ChildName(idx);
                // Коллайдер у детей не нужен — клик ловит BoxCollider корня, а в
                // WebGL-сборке примитив всё равно приходит без него.
                var col = child.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                child.transform.SetParent(transform, false);
                _children.Add(child);
            }
        }

        /// <summary>Раскладка в ЛОКАЛЬНЫХ мм от центра прибора. Перёд — сторона
        /// +Z, как у варочной и духовки; ось Y вверх.</summary>
        private void RebuildGeometry()
        {
            if (_children.Count < ChildCount) return;

            float halfH = BODY_HEIGHT_MM * 0.5f;
            float halfD = BODY_DEPTH_MM * 0.5f;

            Box(IdxBody, Vector3.zero,
                new Vector3(BODY_WIDTH_MM, BODY_HEIGHT_MM, BODY_DEPTH_MM));

            // Полоса панели утоплена в корпус: её передняя грань совпадает с
            // передней гранью корпуса.
            Box(IdxPanel,
                new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f,
                    halfD - OVERLAY_THICKNESS_MM * 0.5f),
                new Vector3(BODY_WIDTH_MM, CONTROL_PANEL_HEIGHT_MM, OVERLAY_THICKNESS_MM));

            ApplyMaterials();
        }

        /// <summary>Поставить дочернюю коробку: позиция и размер — в ЛОКАЛЬНЫХ мм.</summary>
        private void Box(int idx, Vector3 centerMM, Vector3 sizeMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            var go = _children[idx];
            if (go == null) return;
            go.transform.localPosition = centerMM * toU;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = sizeMM * toU;
        }

        // ── Материалы ───────────────────────────────────────────────────
        // Цвета прибора фиксированы вместе с размерами: посудомойка — купленная
        // техника, а не отделываемая деталь, поэтому декор из каталога к ней не
        // применяется (как у мойки и духовки).

        private static Material? _bodyMat;
        private static Material? _panelMat;

        private static Material Lit(Color color, float metallic, float smoothness)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        // Сравнение с null — именно `== null`, а не `??=`: у UnityEngine.Object
        // своё «уничтоженное, но не null» состояние, и `??=` его не видит.

        /// <summary>Корпус — тёмно-серый матовый.</summary>
        private static Material BodyMaterial()
        {
            if (_bodyMat == null) _bodyMat = Lit(new Color(0.18f, 0.18f, 0.19f, 1f), 0.1f, 0.35f);
            return _bodyMat!;
        }

        /// <summary>Панель управления — чёрная.</summary>
        private static Material PanelMaterial()
        {
            if (_panelMat == null) _panelMat = Lit(new Color(0.03f, 0.03f, 0.035f, 1f), 0.05f, 0.7f);
            return _panelMat!;
        }

        private void ApplyMaterials()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (child == null) continue;
                var mr = child.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                mr.sharedMaterial = i == IdxPanel ? PanelMaterial() : BodyMaterial();
            }
        }

        public void DestroyChildren()
        {
            foreach (var child in _children)
            {
                if (child == null) continue;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
            _children.Clear();
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            DestroyChildren();
        }
    }
}
