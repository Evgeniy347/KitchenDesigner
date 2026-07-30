using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Электрический духовой шкаф Bosch HBA514BB3 (чёрный) — готовая модель из
    /// группы «Техника»: габариты заданы производителем и не редактируются
    /// (<see cref="IFixedSizeElement"/>), поэтому поля Ш/В/Г в окне свойств серые,
    /// ручек ресайза нет, а edit_elements правку размера отклоняет.
    ///
    /// ПЯТЬ параллелепипедов, все — дети при ЕДИНИЧНОМ масштабе корня:
    ///   0 «Body»         — корпус за фасадом, уходит в нишу колонны;
    ///   1 «Facade»       — рамка фасада на всю его площадь;
    ///   2 «Glass»        — чёрное стекло двери, утоплено в рамку;
    ///   3 «ControlPanel» — верхняя полоса фасада;
    ///   4 «Handle»       — ручка по низу панели управления.
    ///
    /// Фасад собран ЗДЕСЬ, а не через <see cref="AssembledFacadeElement"/>:
    /// у сборного фасада рамка считается по формуле каталога (доля A от стороны,
    /// стойки + перекладины + вставка) и живёт одним процедурным мешем с двумя
    /// сабмешами под фрезеровки. Духовке нужна не рамка мебельного фасада, а
    /// четыре коробки с размерами из монтажной схемы (полоса 96 сверху, стекло
    /// 499 снизу, ручка со своим выступом) — переиспользование потребовало бы
    /// параметризовать AssembledFacadeMesh тремя чужими случаями ради геометрии,
    /// которая проще самой формулы.
    ///
    /// В отличие от варочной духовка НЕ врезная: она не <see cref="IPartCutout"/>,
    /// ни к какой детали не привязывается и не режет проёмов — обычный корпусный
    /// элемент со штатными снапами и валидацией.
    ///
    /// Все размеры — из docs/APPLIANCES-BRIEF.md §2 (монтажная схема Bosch).
    /// О двух расхождениях брифа см. FACADE_BOTTOM_OVERHANG_MM и HANDLE_TOP_MM.
    /// </summary>
    public class OvenElement : KitchenElement, IFixedSizeElement
    {
        /// <summary>Единственная модель. Второй духовке хватило бы таблицы
        /// «модель → размеры», как у <see cref="CooktopElement.ModelDimensionsMM"/>;
        /// заводить её ради одной строки — лишний слой.</summary>
        public const string MODEL = "Bosch HBA514BB3";

        // ── Фасад ───────────────────────────────────────────────────────
        public const int FACADE_WIDTH_MM = 594;
        public const int FACADE_HEIGHT_MM = 595;

        /// <summary>Толщина фасадной рамки/двери. Дробная — поэтому геометрия
        /// считается во float мм, а в целочисленный габарит уходит округление.</summary>
        public const float FACADE_THICKNESS_MM = 19.5f;

        // ── Корпус за фасадом ───────────────────────────────────────────
        public const int BODY_WIDTH_MM = 570;
        public const int BODY_DEPTH_MM = 548;
        public const int BODY_HEIGHT_MM = 535;

        /// <summary>Фасад выступает над корпусом сверху — на этой отметке прибор
        /// висит во фланце ниши, поэтому она и взята за опорную.</summary>
        public const int FACADE_TOP_OVERHANG_MM = 25;

        /// <summary>Выступ фасада ПОД корпусом — величина ПРОИЗВОДНАЯ, чтобы
        /// фасад, корпус и верхний выступ сходились без щели.
        ///
        /// РАСХОЖДЕНИЕ В БРИФЕ. Там сказано «595 = 25 + 535 + 7.5 + допуск
        /// скругления», но 25 + 535 + 7.5 = 567.5, то есть до 595 не хватает
        /// 27.5 мм — это не допуск, а полноценные три сантиметра. Из трёх чисел
        /// два взяты со схемы напрямую (фасад 595, корпус 535) и одно — верхний
        /// выступ 25 (по нему прибор встаёт в нишу), поэтому нижний выступ
        /// вычисляется: 595 − 25 − 535 = 35 мм. Если схему уточнят и корпус
        /// окажется 562.5 — поменяется одна константа, а геометрия сойдётся сама.</summary>
        public const int FACADE_BOTTOM_OVERHANG_MM =
            FACADE_HEIGHT_MM - FACADE_TOP_OVERHANG_MM - BODY_HEIGHT_MM;

        // ── Разбивка фасада ─────────────────────────────────────────────
        /// <summary>Панель управления — верхняя полоса фасада.</summary>
        public const int CONTROL_PANEL_HEIGHT_MM = 96;

        /// <summary>Стекло двери — весь остаток фасада ниже панели.</summary>
        public const int GLASS_HEIGHT_MM = FACADE_HEIGHT_MM - CONTROL_PANEL_HEIGHT_MM;

        /// <summary>Ширина рамки вокруг стеклянной вставки двери.</summary>
        public const int DOOR_FRAME_MM = 15;

        /// <summary>Толщина накладных деталей фасада (стекло, полоса панели):
        /// они лежат заподлицо с передней плоскостью рамки, а не поверх неё, —
        /// иначе фасад выходил бы за собственный габарит.</summary>
        public const float OVERLAY_THICKNESS_MM = 2f;

        // ── Ручка ───────────────────────────────────────────────────────
        /// <summary>Верх ручки — по низу панели управления, т.е. на отметке
        /// <see cref="CONTROL_PANEL_HEIGHT_MM"/> от верха фасада.
        ///
        /// РАСХОЖДЕНИЕ В БРИФЕ. Там ручка описана и как «по низу панели
        /// управления», и как «на отметке 405 от верха фасада» — одновременно эти
        /// два условия несовместимы: низ панели лежит на 96 мм. 405 мм от верха
        /// пришлось бы на середину стекла, где у HBA5 ручки нет; значит, это
        /// размер со схемы от другой базы (линии ниши/столешницы). Держимся
        /// геометрически определённого условия — низа панели управления.</summary>
        public const int HANDLE_TOP_MM = CONTROL_PANEL_HEIGHT_MM;
        public const int HANDLE_HEIGHT_MM = 28;
        public const int HANDLE_SIDE_INSET_MM = 12;

        /// <summary>Максимальный выступ ручки вперёд от плоскости фасада. Ручка
        /// СОЗНАТЕЛЬНО выходит за габаритную коробку: коробка описывает то, что
        /// встаёт в нишу колонны (корпус + фасад), и включить в неё накладную
        /// ручку значило бы, что духовка перестала помещаться в свою же нишу.</summary>
        public const int HANDLE_PROTRUSION_MM = 50;

        // ── Габарит ─────────────────────────────────────────────────────
        /// <summary>Корпус плюс фасад — 548 + 19.5 = 567.5 мм.</summary>
        public const float TOTAL_DEPTH_MM = BODY_DEPTH_MM + FACADE_THICKNESS_MM;

        /// <summary>Глубина в целочисленном габарите: 567.5 округлены ВВЕРХ,
        /// чтобы коробка накрывала прибор целиком. Полмиллиметра расхождения с
        /// геометрией — цена того, что DimensionsMM целые.</summary>
        public const int DEPTH_MM = 568;

        /// <summary>Габарит прибора: 594 × 595 × 568.</summary>
        public static Vector3Int ModelDimensionsMM =>
            new Vector3Int(FACADE_WIDTH_MM, FACADE_HEIGHT_MM, DEPTH_MM);

        private readonly List<GameObject> _children = new List<GameObject>();

        /// <summary>Габариты заданы производителем — всегда и у любого экземпляра
        /// (в отличие от варочной, где один класс обслуживает и свободный
        /// элемент, и пресет).</summary>
        public bool HasFixedSize => true;

        protected override Vector3 EffectiveScale => new Vector3(
            FACADE_WIDTH_MM * AppConstants.MM_TO_UNITS,
            FACADE_HEIGHT_MM * AppConstants.MM_TO_UNITS,
            DEPTH_MM * AppConstants.MM_TO_UNITS);

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
        private const int IdxFacade = 1;
        private const int IdxGlass = 2;
        private const int IdxPanel = 3;
        private const int IdxHandle = 4;
        private const int ChildCount = 5;

        private static string ChildName(int idx) => idx switch
        {
            IdxBody => "Body",
            IdxFacade => "Facade",
            IdxGlass => "Glass",
            IdxPanel => "ControlPanel",
            _ => "Handle",
        };

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
        /// +Z, как у варочной; ось Y вверх.</summary>
        private void RebuildGeometry()
        {
            if (_children.Count < ChildCount) return;

            float halfH = FACADE_HEIGHT_MM * 0.5f;
            // Полглубины считаем по НАСТОЯЩИМ 567.5, а не по округлённым 568:
            // геометрия обязана сойтись сама с собой, округление живёт только в
            // габаритной коробке.
            float halfD = TOTAL_DEPTH_MM * 0.5f;

            // Задняя плоскость фасада — общая база для корпуса и накладок.
            float facadeBackZ = halfD - FACADE_THICKNESS_MM;

            // Корпус: подвешен под верхним выступом фасада, уходит назад до
            // задней плоскости габарита.
            Box(IdxBody,
                new Vector3(0f, halfH - FACADE_TOP_OVERHANG_MM - BODY_HEIGHT_MM * 0.5f,
                    facadeBackZ - BODY_DEPTH_MM * 0.5f),
                new Vector3(BODY_WIDTH_MM, BODY_HEIGHT_MM, BODY_DEPTH_MM));

            // Рамка фасада — на всю его площадь.
            Box(IdxFacade,
                new Vector3(0f, 0f, halfD - FACADE_THICKNESS_MM * 0.5f),
                new Vector3(FACADE_WIDTH_MM, FACADE_HEIGHT_MM, FACADE_THICKNESS_MM));

            // Накладки утоплены в рамку: их передняя грань совпадает с передней
            // гранью фасада.
            float overlayZ = halfD - OVERLAY_THICKNESS_MM * 0.5f;

            // Панель управления — верхняя полоса.
            Box(IdxPanel,
                new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f, overlayZ),
                new Vector3(FACADE_WIDTH_MM, CONTROL_PANEL_HEIGHT_MM, OVERLAY_THICKNESS_MM));

            // Стекло двери — в проёме рамки под панелью управления.
            float doorTopY = halfH - CONTROL_PANEL_HEIGHT_MM;
            Box(IdxGlass,
                new Vector3(0f, (doorTopY - halfH) * 0.5f, overlayZ),
                new Vector3(FACADE_WIDTH_MM - 2 * DOOR_FRAME_MM,
                    GLASS_HEIGHT_MM - 2 * DOOR_FRAME_MM, OVERLAY_THICKNESS_MM));

            // Ручка: верхняя грань — по низу панели управления, вперёд выступает
            // за габарит (см. HANDLE_PROTRUSION_MM).
            Box(IdxHandle,
                new Vector3(0f, halfH - HANDLE_TOP_MM - HANDLE_HEIGHT_MM * 0.5f,
                    halfD + HANDLE_PROTRUSION_MM * 0.5f),
                new Vector3(FACADE_WIDTH_MM - 2 * HANDLE_SIDE_INSET_MM,
                    HANDLE_HEIGHT_MM, HANDLE_PROTRUSION_MM));

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
        // Цвета прибора фиксированы вместе с размерами: духовка — купленная
        // техника, а не отделываемая деталь, поэтому декор из каталога к ней не
        // применяется (как у мойки).

        private static Material? _bodyMat;
        private static Material? _glassMat;
        private static Material? _panelMat;
        private static Material? _handleMat;

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

        /// <summary>Корпус и рамка фасада — чёрный матовый.</summary>
        private static Material BodyMaterial()
        {
            if (_bodyMat == null) _bodyMat = Lit(new Color(0.05f, 0.05f, 0.05f, 1f), 0.05f, 0.25f);
            return _bodyMat!;
        }

        /// <summary>Стекло двери — чёрное глянцевое.</summary>
        private static Material GlassMaterial()
        {
            if (_glassMat == null) _glassMat = Lit(new Color(0.03f, 0.03f, 0.035f, 1f), 0.05f, 0.95f);
            return _glassMat!;
        }

        /// <summary>Панель управления — чуть светлее стекла, иначе на чёрном её
        /// не видно вовсе.</summary>
        private static Material PanelMaterial()
        {
            if (_panelMat == null) _panelMat = Lit(new Color(0.14f, 0.14f, 0.15f, 1f), 0.05f, 0.6f);
            return _panelMat!;
        }

        /// <summary>Ручка — тёмный металл.</summary>
        private static Material HandleMaterial()
        {
            if (_handleMat == null) _handleMat = Lit(new Color(0.22f, 0.22f, 0.23f, 1f), 0.6f, 0.7f);
            return _handleMat!;
        }

        private void ApplyMaterials()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (child == null) continue;
                var mr = child.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                mr.sharedMaterial = i switch
                {
                    IdxGlass => GlassMaterial(),
                    IdxPanel => PanelMaterial(),
                    IdxHandle => HandleMaterial(),
                    _ => BodyMaterial(),
                };
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
