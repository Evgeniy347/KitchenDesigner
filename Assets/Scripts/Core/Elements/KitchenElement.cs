using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [SelectionBase]
    public class KitchenElement : MonoBehaviour
    {
        [SerializeField] private PartData _data = new PartData();

        public PartData Data => _data;

        [Undoable]
        public string PartName
        {
            get => _data.PartName;
            set => _data.PartName = value;
        }

        // Габарит откатывается ПЕРВЫМ: по нему клампятся вырез варочной, кромка
        // и высота опоры — вернуть их по старой плите значило бы подрезать.
        [Undoable(Order = -100)]
        public Vector3Int DimensionsMM
        {
            get => _data.DimensionsMM;
            set
            {
                _data.DimensionsMM = value;
                ApplyDimensions();
            }
        }

        [Undoable]
        public bool Movable
        {
            get => _data.Movable;
            set => _data.Movable = value;
        }

        /// <summary>Габаритная коробка детали строится от её трансформа. У открытой
        /// дверцы и выдвинутого ящика это НЕ так: геометрия считается от закрытой
        /// позы (ClosedPosition), которая заморожена и за трансформом не следует.
        /// Запись в transform.position тогда коробку не двигает — она лишь растёт
        /// симметрично от старого центра, тянущаяся грань уходит на половину дельты,
        /// снэп мажет, а при закрытии деталь прыгает на исходное место.</summary>
        public virtual bool PoseFollowsTransform => !_attachRidden;

        /// <summary>Деталь можно двигать и растягивать прямо сейчас. Кроме флага
        /// «подвижна» требует, чтобы поза шла за трансформом — иначе правка
        /// геометрии молча разъезжается с логической позой.</summary>
        public bool Transformable => Movable && PoseFollowsTransform;

        [NotUndoable("группировка идёт своей командой SetGroupCommand")]
        public int GroupId
        {
            get => _data.GroupId;
            set => _data.GroupId = value;
        }

        [NotUndoable("декор ставится через SetMaterialCommand — одной записи в поле мало, нужен MaterialManager")]
        public string MaterialId
        {
            get => _data.MaterialId;
            set => _data.MaterialId = value;
        }

        [Undoable]
        public bool Transparent
        {
            get => _data.Transparent;
            set => _data.Transparent = value;
        }

        // ── Прикрепление к другой детали ────────────────────────────────
        // Односторонняя связь по имени: родитель тащит детей за собой, ребёнок
        // родителя — никогда. Механика целиком живёт в AttachLinks; здесь только
        // поле и поза покоя, которую у едущей детали спрашивает валидация.

        /// <summary>Имя детали или фасада, к которой эта деталь прикреплена;
        /// пусто — ни к чему. См. <see cref="AttachLinks"/>.</summary>
        [Undoable]
        public string AttachedToName
        {
            get => _data.AttachedToName;
            set => _data.AttachedToName = value;
        }

        private Vector3 _attachRestPos;
        private Quaternion _attachRestRot = Quaternion.identity;
        private bool _attachRidden;

        /// <summary>Деталь ПРЯМО СЕЙЧАС едет за анимацией родителя: её трансформ
        /// принадлежит <see cref="AttachRider"/>, а не пользователю.</summary>
        public bool IsAttachRidden => _attachRidden;

        /// <summary>Поза ПОКОЯ — та, в которой деталь стоит при закрытом
        /// родителе. Это она пишется в проект и по ней считается геометрия;
        /// пока деталь не едет, это просто её трансформ.</summary>
        public Vector3 AttachRestPosition => _attachRidden ? _attachRestPos : transform.position;
        public Quaternion AttachRestRotation => _attachRidden ? _attachRestRot : transform.rotation;

        /// <summary>Начать езду за родителем, запомнив позу покоя (зовёт
        /// <see cref="AttachRider"/>, больше никто).</summary>
        internal void BeginAttachRide(Vector3 restPos, Quaternion restRot)
        {
            _attachRestPos = restPos;
            _attachRestRot = restRot;
            _attachRidden = true;
        }

        /// <summary>Родитель вернулся в покой: трансформ снова принадлежит
        /// детали.</summary>
        internal void EndAttachRide() => _attachRidden = false;

        // ── Зазоры ─────────────────────────────────────────────────────
        // Зазор — расстояние от физической детали до границы её ГАБАРИТА:
        // у фасада это отступ от проёма, у ДВП/ХДФ — технологический зазор в
        // пазу, у обычной детали он обычно нулевой. Механика одна на всех
        // (см. GappedBox), поэтому и свойства живут здесь, а не в двух
        // подклассах, как раньше.

        [Undoable]
        public int GapLeft
        {
            get => _data.GapLeft;
            set { _data.GapLeft = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapRight
        {
            get => _data.GapRight;
            set { _data.GapRight = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapTop
        {
            get => _data.GapTop;
            set { _data.GapTop = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapBottom
        {
            get => _data.GapBottom;
            set { _data.GapBottom = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapFront
        {
            get => _data.GapFront;
            set { _data.GapFront = value; ApplyDimensions(); }
        }

        [Undoable]
        public int GapBack
        {
            get => _data.GapBack;
            set { _data.GapBack = value; ApplyDimensions(); }
        }

        /// <summary>Сумма всех шести зазоров.</summary>
        public int GapMM => _data.GapMM;

        public BoxGaps Gaps => _data.Gaps;

        public int GapOf(GapSide side) => _data.GapOf(side);

        public void SetGap(GapSide side, int valueMM)
        {
            _data.SetGap(side, valueMM);
            ApplyDimensions();
        }

        /// <summary>Есть ли у детали зазоры. Стена, подложка и техника их не
        /// знают: у первых двух габарит — это сама конструкция, у техники он
        /// задан корпусом прибора.</summary>
        public virtual bool SupportsGaps => SupportsGrooves;

        // ── Пазы ───────────────────────────────────────────────────────
        // Пазы поддерживает только базовая «Деталь»: у фасадов/ящиков/столов и
        // прочих подтипов геометрия своя процедурная, и врезка в неё пласти не
        // определена. Стена и подложка — тоже KitchenElement, но деталями не
        // являются, поэтому исключены явно.
        private Mesh? _ownedMesh;

        // Размеры, под которые собран _ownedMesh. UV в нём привязаны к пропорциям
        // детали (см. GrooveMesh.ScaleUvToDecor), поэтому ресайз требует пересборки,
        // а повторный вызов с теми же размерами — нет.
        private Vector3Int _meshDims;

        // Грани, на которых кромки нет: их рисует подложка (EdgeSubstrate).
        // Маска приходит СНАРУЖИ — перекрытие торца зависит от соседей, и знать
        // о нём деталь не может; источник один, EdgeSubstrate.Sync/SyncScene.
        private int _bareFaceMask;

        // Маска, под которую собран _ownedMesh: расхождение с текущей означает,
        // что сабмеш торцов пора пересобрать.
        private int _meshBareFaceMask;

        // Номера служебных сабмешей в _ownedMesh (см. GrooveMesh.SubmeshLayout).
        private GrooveMesh.SubmeshLayout _meshLayout = new GrooveMesh.SubmeshLayout(-1, -1);

        public bool SupportsGrooves =>
            GetType() == typeof(KitchenElement)
            && GetComponent<Wall>() == null
            && GetComponent<BasePlate>() == null;

        public IReadOnlyList<GrooveSpec> Grooves => _data.Grooves;

        // ── Накладки текстур ───────────────────────────────────────────
        // Локальный декор на куске грани: плитка на фартуке, обои на одной
        // стороне стены, ламинат на полу. Поддерживают стена и пол — только у
        // них есть большие плоскости, которые оформляют участками. У детали для
        // этого уже есть свой декор на весь щит (MaterialId).
        public bool SupportsTextureOverlays =>
            GetComponent<Wall>() != null || this is FloorElement;

        public IReadOnlyList<TextureOverlaySpec> TextureOverlays => _data.TextureOverlays;

        /// <summary>Заменить весь набор накладок (правки из UI, undo, загрузка,
        /// дублирование). Единственная точка мутации: ей же принадлежит
        /// уведомление рендера, поэтому накладки в сцене не могут разъехаться
        /// со списком.</summary>
        public void SetTextureOverlays(IEnumerable<TextureOverlaySpec>? overlays)
        {
            if (!SupportsTextureOverlays) return;
            _data.TextureOverlays.Clear();
            if (overlays != null)
                foreach (var o in overlays)
                {
                    if (_data.TextureOverlays.Count >= TextureOverlayGeometry.MAX_PER_ELEMENT) break;
                    _data.TextureOverlays.Add(o);
                }
            TextureOverlayRenderer.Refresh(this);
        }

        // ── Кромки ─────────────────────────────────────────────────────
        // Кромкование поддерживает та же базовая «Деталь», что и пазы, и
        // только когда деталь — лист: ровно одна сторона тоньше порога
        // (см. EdgeBanding.IsSheet). У бруска/куба торцов в смысле кромки нет.
        public bool SupportsEdges => SupportsGrooves && EdgeBanding.IsSheet(_data.DimensionsMM);

        /// <summary>Клеить ли кромку на открытые торцы. У детали, которая
        /// кромкование не поддерживает, всегда false.</summary>
        [NotUndoable("кромка целиком идёт через SetEdgeBandingCommand: три поля одним шагом")]
        public bool EdgeBandingEnabled
        {
            get => SupportsEdges && _data.EdgeBanding;
            set
            {
                if (_data.EdgeBanding == value) return;
                _data.EdgeBanding = value;
                // Кромка закрывает торец, а без неё видна голая плита — набор
                // граней под подложкой меняется целиком.
                if (!SuppressVisualRebuild) EdgeSubstrate.Sync(this);
            }
        }

        [NotUndoable("см. EdgeBandingEnabled — SetEdgeBandingCommand")]
        public float EdgeThicknessMM
        {
            get => _data.EdgeThicknessMM;
            set => _data.EdgeThicknessMM = value;
        }

        /// <summary>Стороны с ручной кромкой (маска по <see cref="EdgeSide"/>):
        /// пользователь взял их на себя, автоматическая проверка на них молчит.</summary>
        [NotUndoable("см. EdgeBandingEnabled — SetEdgeBandingCommand")]
        public int EdgeManualMask
        {
            get => _data.EdgeManualMask;
            set => _data.EdgeManualMask = value;
        }

        public bool IsEdgeManual(EdgeSide side) => _data.IsEdgeManual(side);

        public void SetEdgeManual(EdgeSide side, bool manual) => _data.SetEdgeManual(side, manual);

        // ── Врезная техника (мойка, варочная) ──────────────────────────
        // Мойка и варочная живут отдельными элементами, но их проёмы — часть
        // геометрии ДЕТАЛИ (как окно и стена). Список ведётся деталью, чтобы меш
        // пересобирался из одного места: и при врезке, и при ресайзе детали
        // (доля проёма считается от её размеров).
        private readonly List<IPartCutout> _cutouts = new List<IPartCutout>();

        public IReadOnlyList<IPartCutout> AttachedCutouts => _cutouts;

        public bool HasCutout(IPartCutout cutout) => !IsGone(cutout) && _cutouts.Contains(cutout);

        public void RegisterCutout(IPartCutout cutout)
        {
            if (IsGone(cutout) || !SupportsGrooves || _cutouts.Contains(cutout)) return;
            _cutouts.Add(cutout);
            RebuildGrooveMesh();
        }

        public void UnregisterCutout(IPartCutout cutout)
        {
            if (cutout == null) return;
            if (_cutouts.Remove(cutout)) RebuildGrooveMesh();
        }

        /// <summary>Снять всю врезную технику (возврат детали в пул).</summary>
        public void ClearCutouts()
        {
            if (_cutouts.Count == 0) return;
            _cutouts.Clear();
            RebuildGrooveMesh();
        }

        /// <summary>Уничтоженный MonoBehaviour за интерфейсной ссылкой: обычное
        /// <c>== null</c> здесь не срабатывает (подменённое сравнение Unity
        /// работает только для её собственных типов).</summary>
        private static bool IsGone(IPartCutout? cutout) =>
            cutout == null || (cutout is Object obj && obj == null);

        /// <summary>Ось, ПОПЕРЁК которой режутся проёмы врезной техники: та
        /// локальная ось детали, что смотрит вверх. У повёрнутой доски это Z
        /// (пласть), у столешницы-короба — Y (толщина). Без врезки —
        /// канонический Z.</summary>
        public int CutoutHoleAxis
        {
            get
            {
                foreach (var cutout in _cutouts)
                    if (!IsGone(cutout)) return cutout.HoleAxisIn(this);
                return 2;
            }
        }

        /// <summary>Проёмы врезной техники в нормализованных координатах той
        /// плоскости, в которой их режет GrooveMesh (см. CutoutHoleAxis).</summary>
        public List<GrooveMesh.Rect2> CutoutHoleRects()
        {
            var result = new List<GrooveMesh.Rect2>();
            foreach (var cutout in _cutouts)
            {
                if (IsGone(cutout)) continue;
                var rect = cutout.CutoutRectIn(this);
                if (rect.IsValid) result.Add(rect);
            }
            return result;
        }

        /// <summary>Добавить паз. Дубль (та же сторона + тип) игнорируется:
        /// смещение фиксировано, второй такой паз лёг бы ровно на первый.</summary>
        public bool AddGroove(GrooveSpec spec)
        {
            if (!SupportsGrooves) return false;
            if (_data.Grooves.Count >= AppConstants.GROOVE_MAX_PER_PART) return false;
            if (_data.Grooves.Contains(spec)) return false;
            _data.Grooves.Add(spec);
            RebuildGrooveMesh();
            return true;
        }

        public bool RemoveGrooveAt(int index)
        {
            if (index < 0 || index >= _data.Grooves.Count) return false;
            _data.Grooves.RemoveAt(index);
            RebuildGrooveMesh();
            return true;
        }

        public void ClearGrooves()
        {
            if (_data.Grooves.Count == 0) return;
            _data.Grooves.Clear();
            RebuildGrooveMesh();
        }

        /// <summary>Заменить весь набор пазов (загрузка проекта, дублирование).</summary>
        public void SetGrooves(IEnumerable<GrooveSpec>? grooves)
        {
            if (!SupportsGrooves) return;
            _data.Grooves.Clear();
            if (grooves != null)
                foreach (var g in grooves)
                    if (_data.Grooves.Count < AppConstants.GROOVE_MAX_PER_PART
                        && !_data.Grooves.Contains(g))
                        _data.Grooves.Add(g);
            RebuildGrooveMesh();
        }

        /// <summary>Пересобрать меш под текущие пазы и размеры. Без пазов это
        /// обычная коробка — но СОБСТВЕННАЯ, а не встроенный куб: UV в ней
        /// приведены к физическому масштабу декора, а он зависит от пропорций
        /// детали (см. GrooveMesh.ScaleUvToDecor).</summary>
        public void RebuildGrooveMesh()
        {
            if (!SupportsGrooves) return;
            var filter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (filter == null || meshRenderer == null) return;

            var mats = meshRenderer.sharedMaterials;
            var decor = mats != null && mats.Length > 0 && mats[0] != null
                ? mats[0] : meshRenderer.sharedMaterial;

            var holes = CutoutHoleRects();

            var mesh = GrooveMesh.Build(_data.DimensionsMM, _data.Grooves, holes,
                CutoutHoleAxis, _bareFaceMask, out var layout);
            DestroyOwnedMesh();
            _ownedMesh = mesh;
            _meshDims = _data.DimensionsMM;
            _meshBareFaceMask = _bareFaceMask;
            _meshLayout = layout;
            filter.sharedMesh = mesh;

            // Сабмеш 0 — декор (им управляет MaterialManager), дальше пазы и
            // некромкованные торцы — по факту наличия (см. GrooveMesh.Build).
            var slots = new Material[mesh.subMeshCount];
            slots[0] = decor!;
            meshRenderer.sharedMaterials = slots;
            RefreshSubmeshMaterials();
        }

        /// <summary>Поставить на место материалы СЛУЖЕБНЫХ сабмешей — пазов и
        /// некромкованных торцов. Сабмеш 0 (декор) не трогается.
        ///
        /// Нужно не только после пересборки меша: валидационная тонировка и режим
        /// редактирования модуля заливают деталь одним материалом на все сабмеши
        /// (ElementHighlighter.PaintFlat), и возврат декора чинит только нулевой
        /// слот — паз оставался цвета тонировки, а торец терял подложку.</summary>
        public void RefreshSubmeshMaterials()
        {
            if (!SupportsGrooves || _ownedMesh == null) return;
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            var slots = meshRenderer.sharedMaterials;
            if (slots == null || slots.Length != _ownedMesh.subMeshCount) return;

            if (_meshLayout.Grooves >= 0)
                slots[_meshLayout.Grooves] = GrooveMesh.GrooveMaterial();
            // Без шейдера подложки торец остаётся на декоре: дыра в материалах
            // рендерера дала бы несуществующую грань.
            if (_meshLayout.BareEnds >= 0)
                slots[_meshLayout.BareEnds] = EdgeSubstrate.Material() ?? slots[0];
            meshRenderer.sharedMaterials = slots;
        }

        /// <summary>Грани без кромки — их рисует подложка (см. <see cref="EdgeSubstrate"/>).
        /// Маску считает EdgeSubstrate по всей сцене, деталь её только хранит.</summary>
        public int BareFaceMask => _bareFaceMask;

        /// <summary>Принять новую маску некромкованных граней. Меш пересобирается
        /// только когда маска действительно поменялась: проход по сцене идёт на
        /// каждое изменение, а пересборка меша дороже самого расчёта.</summary>
        public void SetBareFaceMask(int mask)
        {
            if (mask == _bareFaceMask) return;
            _bareFaceMask = mask;
            if (!SuppressVisualRebuild) RebuildGrooveMesh();
        }

        private void DestroyOwnedMesh()
        {
            if (_ownedMesh == null) return;
            DestroyImmediate(_ownedMesh);
            _ownedMesh = null;
        }

        /// <summary>Посадочные грани пазов — ДНО каждого паза как обычная Face.
        /// Благодаря этому прилипание не требует отдельной ветки: вкладная панель
        /// ловится тем же попарным сопоставлением встречных граней, что и всё
        /// остальное, и встаёт номиналом на дно паза (см. GappedBox).
        ///
        /// Габаритные грани (GetFaces) паз НЕ меняет: короб остаётся коробом,
        /// иначе поехали бы ручки, выделение и прилипание соседних деталей.</summary>
        public Face[] GetGrooveSeatFaces() => GetGrooveSeatFacesAt(transform.position);

        /// <summary>Дно пазов для ЗАДАННОЙ позиции детали (см. GetFacesAt).</summary>
        public Face[] GetGrooveSeatFacesAt(Vector3 position)
        {
            int count = _data.Grooves.Count;
            if (count == 0) return System.Array.Empty<Face>();

            var scale = transform.localScale;
            var rot = transform.rotation;
            var pos = position;
            // Пласть, в которой режется паз, — локальная грань +Z.
            var normal = rot * Vector3.forward;
            var right = rot * Vector3.right;
            var up = rot * Vector3.up;
            float floorZ = 0.5f - GrooveMesh.DepthFraction(_data.DimensionsMM);

            var seats = new List<Face>(count);
            foreach (var groove in _data.Grooves)
            {
                var rect = GrooveMesh.ComputeRect(_data.DimensionsMM, groove);
                if (!rect.IsValid) continue;

                var localCenter = new Vector3(
                    (rect.xMin + rect.xMax) * 0.5f * scale.x,
                    (rect.yMin + rect.yMax) * 0.5f * scale.y,
                    floorZ * scale.z);
                var size = new Vector2(
                    (rect.xMax - rect.xMin) * scale.x,
                    (rect.yMax - rect.yMin) * scale.y);

                seats.Add(new Face(pos + rot * localCenter, normal, size, right, up));
            }
            return seats.ToArray();
        }

        /// <summary>Стенки пазов — разметочные плоскости для ВЫРАВНИВАНИЯ кромки
        /// любой детали, а не поверхности контакта: деталь обычно прилегает к
        /// пласти снаружи и в сам паз не заходит. Снэп берёт отсюда координаты
        /// и добавляет детенты «начало паза» / «конец паза» рядом с кромкой
        /// детали (16 и 20 мм от неё).</summary>
        public Face[] GetGrooveWallFaces() => GetGrooveWallFacesAt(transform.position);

        /// <summary>Стенки пазов для ЗАДАННОЙ позиции детали (см. GetFacesAt).</summary>
        public Face[] GetGrooveWallFacesAt(Vector3 position)
        {
            int count = _data.Grooves.Count;
            if (count == 0) return System.Array.Empty<Face>();

            var scale = transform.localScale;
            var rot = transform.rotation;
            var pos = position;
            float depthFrac = GrooveMesh.DepthFraction(_data.DimensionsMM);
            // Стенка тянется от дна паза до пласти.
            float wallCenterZ = 0.5f - depthFrac * 0.5f;

            var walls = new List<Face>(count * 4);
            foreach (var groove in _data.Grooves)
            {
                var rect = GrooveMesh.ComputeRect(_data.DimensionsMM, groove);
                if (!rect.IsValid) continue;

                bool horizontal = groove.side == GrooveSide.Top || groove.side == GrooveSide.Bottom;
                if (horizontal)
                {
                    // Паз идёт вдоль X — стенки перпендикулярны Y.
                    var axis = rot * Vector3.up;
                    var size = new Vector2((rect.xMax - rect.xMin) * scale.x, depthFrac * scale.z);
                    float cx = (rect.xMin + rect.xMax) * 0.5f * scale.x;
                    foreach (float y in new[] { rect.yMin, rect.yMax })
                    {
                        var c = pos + rot * new Vector3(cx, y * scale.y, wallCenterZ * scale.z);
                        walls.Add(new Face(c, axis, size, rot * Vector3.right, rot * Vector3.forward));
                    }
                }
                else
                {
                    // Паз идёт вдоль Y — стенки перпендикулярны X.
                    var axis = rot * Vector3.right;
                    var size = new Vector2((rect.yMax - rect.yMin) * scale.y, depthFrac * scale.z);
                    float cy = (rect.yMin + rect.yMax) * 0.5f * scale.y;
                    foreach (float x in new[] { rect.xMin, rect.xMax })
                    {
                        var c = pos + rot * new Vector3(x * scale.x, cy, wallCenterZ * scale.z);
                        walls.Add(new Face(c, axis, size, rot * Vector3.up, rot * Vector3.forward));
                    }
                }
            }
            return walls.ToArray();
        }

        // Тип Face переехал в KitchenDesigner.Geometry (Geometry/Face.cs): грань —
        // это геометрия, а не деталь сцены, и вложенность тянула MonoBehaviour
        // в каждую чистую функцию прилипания.

        /// <summary>Версия позы: растёт, когда <see cref="SceneChangeTracker"/> замечает
        /// запись в трансформ. Системам, которым важно «деталь сдвинули» (привязка окна к
        /// стене, мойки к столешнице, верхнего ящика к нижнему), достаточно сравнить её со
        /// своей — вместо покадрового пересчёта.</summary>
        public int PoseVersion { get; private set; }

        internal void BumpPoseVersion() => PoseVersion++;

        private void Awake()
        {
            ApplyDimensions();
            PartRegistry.Register(this);
        }

        private void OnDestroy()
        {
            PartRegistry.Unregister(this);
            DestroyOwnedMesh();
        }

        /// <summary>ФИЗИЧЕСКИЙ габарит детали в юнитах, БЕЗ зазоров. Обычно это
        /// localScale; переопределяют те, у кого габаритный бокс не совпадает с
        /// масштабом: радиусная полка (меш в мировых единицах, localScale
        /// единичный), техника (корпус меньше собственного коллайдера).
        /// Зазоры поверх него накладывает <see cref="GappedBox"/> — здесь их
        /// быть не должно, иначе они учтутся дважды.</summary>
        protected virtual Vector3 EffectiveScale => transform.localScale;

        /// <summary>Поза, в которой деталь проверяется на коллизии/связность.
        /// По умолчанию — текущий трансформ. Ящик/фасад переопределяют на
        /// ЗАКРЫТУЮ позу: открывание — транзитная анимация, её коллизии гасит
        /// OpeningCollision, и она не должна порождать нарушения в статической
        /// проверке (иначе открытый ящик «пересекает» свой же фасад). Деталь,
        /// едущая за прикреплённым родителем, — тот же случай: её поза покоя
        /// стоит на месте, пока трансформ уехал вместе с фасадом.</summary>
        protected virtual Vector3 ValidationPosition => AttachRestPosition;
        protected virtual Quaternion ValidationRotation => AttachRestRotation;

        /// <summary>Та же <see cref="ValidationPosition"/>, но для ГИПОТЕТИЧЕСКОЙ
        /// позиции трансформа. Кто сдвигает позу валидации относительно трансформа
        /// (мойка и варочная панель приподняты на половину бортика) или замораживает
        /// её (выдвинутый ящик), обязан переопределить и это — иначе примерка
        /// детали в другую позицию разойдётся с тем, что даёт настоящий сдвиг.</summary>
        protected virtual Vector3 ValidationPositionAt(Vector3 transformPosition)
            => _attachRidden ? _attachRestPos : transformPosition;

        /// <summary>Не пересобирать меш и материалы в <see cref="ApplyDimensions"/>.
        /// Размер, поза и логические ограничения применяются как обычно — гасится
        /// только то, что нужно ГЛАЗУ. Для расчётных тестов, где сцену никто не
        /// рисует: прилипание считается по localScale и позе, меша не касается,
        /// а покадровый свип упирался именно в пересборку меша на каждый
        /// миллиметр. Тест обязан вернуть флаг в false в TearDown.</summary>
        public static bool SuppressVisualRebuild;

        public virtual void ApplyDimensions()
        {
            transform.localScale = new Vector3(
                _data.DimensionsMM.x * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.y * AppConstants.MM_TO_UNITS,
                _data.DimensionsMM.z * AppConstants.MM_TO_UNITS
            );

            if (SuppressVisualRebuild) return;

            // Доли паза и проёма мойки считаются от размеров детали, а UV — от её
            // пропорций: при ресайзе меш надо пересобрать, иначе и то и другое
            // растянется вместе с localScale.
            // Ресайз переставляет торцы (у листа 800×18 и 18×800 это разные
            // грани), поэтому маска пересчитывается вместе с мешем.
            if (_meshDims != _data.DimensionsMM) EdgeSubstrate.Sync(this);

            if (_meshDims != _data.DimensionsMM || _ownedMesh == null
                || _meshBareFaceMask != _bareFaceMask) RebuildGrooveMesh();

            // localScale тянет UV вместе с деталью, поэтому «вырез» декора надо
            // пересчитать под новый размер — иначе рисунок растягивается вместо
            // того, чтобы повторяться в своём физическом масштабе.
            MaterialManager.RefreshTiling(this);
        }

        public virtual Vector3[] GetVertices()
        {
            return GetVerticesAt(transform.position);
        }

        /// <summary>Вершины для ЗАДАННОЙ позы детали. Нужны прилипанию, чтобы
        /// примерить деталь в гипотетическую позицию, НЕ двигая её: раньше для
        /// этого писали в transform.position и возвращали назад, а каждая такая
        /// запись грязнит поддерево трансформов.
        ///
        /// Позиция — это то, что подставляется вместо <see cref="ValidationPosition"/>;
        /// все прочие правила (опущенная стена, замороженная поза открытой
        /// дверцы) остаются за наследником.</summary>
        public virtual Vector3[] GetVerticesAt(Vector3 position)
        {
            var size = EffectiveScale;
            var pos = ValidationPositionAt(position);
            var rot = ValidationRotation;

            // Стена может быть опущена (режим обзора WallCutaway) — используем
            // ПОЛНУЮ геометрию, чтобы валидация связности не зависела от камеры.
            var wall = GetComponent<Wall>();
            if (wall != null && wall.IsLowered)
            {
                size.y = wall.FullScaleY;
                pos.y = wall.FullPosition.y;
            }

            return GappedBox.Vertices(size, _data.Gaps, pos, rot);
        }

        public virtual Face[] GetFaces() => GetFacesAt(transform.position);

        /// <summary>Грани для ЗАДАННОЙ позы. См. <see cref="GetVerticesAt"/>.</summary>
        public virtual Face[] GetFacesAt(Vector3 position)
        {
            var size = EffectiveScale;
            var pos = ValidationPositionAt(position);
            var rot = ValidationRotation;

            var wall = GetComponent<Wall>();
            if (wall != null && wall.IsLowered)
            {
                size.y = wall.FullScaleY;
                pos.y = wall.FullPosition.y;
            }

            return GappedBox.Faces(size, _data.Gaps, pos, rot);
        }

        public void SetDimensionsFromUI(int w, int h, int d)
        {
            DimensionsMM = new Vector3Int(w, h, d);
        }

        public string Describe()
        {
            var p = transform.position;
            return $"{_data.PartName} ({_data.DimensionsMM.x}x{_data.DimensionsMM.y}x{_data.DimensionsMM.z}мм @ " +
                   $"{p.x:F3},{p.y:F3},{p.z:F3})";
        }

        public void Rotate(Quaternion rotation)
        {
            transform.rotation = rotation * transform.rotation;
        }

        public void RotateAroundAxis(Vector3 axis, float angle)
        {
            Rotate(Quaternion.AngleAxis(angle, axis));
        }
    }
}
