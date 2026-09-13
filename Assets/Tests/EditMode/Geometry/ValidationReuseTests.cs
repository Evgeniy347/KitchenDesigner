using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Кэш ответа валидации: кадр покоя не считает сцену заново, а любой
/// способ изменить ответ пересчёт возвращает.
///
/// Ключ здесь НЕ ревизия сцены. Сегодня выяснилось, чем ревизия плоха: жест
/// открытия фасада за 177 кадров не двигает её ни разу (<c>NoteSelfAnimated</c>),
/// а переименование детали не двигает её ничем вовсе — <c>DrawerLinks.Rename</c>
/// не трогает ни <c>transform</c>, ни состав реестра. Калитка на таком ключе
/// молчит ровно тогда, когда ответ УЖЕ устарел, и на экране остаются вчерашние
/// нарушения. Поэтому ключ — сам ВХОД валидации: список
/// <see cref="ValidationElement"/>, сравнённый по значениям
/// (<c>ValidatesTheSameAs</c>), включая имя, роль, группу, пару, индексы, осевую
/// стены, каждую грань с её осями, сиденья пазов и каждую вершину.
///
/// Числа сравнения: у живого проекта 411 деталей, 77 % из них несут снимок
/// прошлого кадра (те же массивы — сравнение обрывается на <c>ReferenceEquals</c>),
/// остальные 94 сравниваются по значениям, то есть 94 × (6 граней + 8 вершин).
/// Полная валидация той же сцены — 17,3 мс.
///
/// Каждый обратный вход сперва закрепляет ОПОРНОЕ значение — «та же сцена
/// второй раз стоит нуль валидаций» — и только потом меряет разницу. Без
/// опорного замера ноль пересчётов после изменения читался бы как «кэш
/// сработал», хотя означал бы «кэша нет вовсе и всё считается всегда».</summary>
public class ValidationReuseTests
{
    private const float MM = ValidationTestScene.MM;

    private static readonly Quaternion Yaw90 =
        new Quaternion(0f, 0.70710678f, 0f, 0.70710678f);

    /// <summary>Деталь, которую тест умеет менять каждым из способов
    /// приложения: подвинуть, повернуть, растянуть, снять зазор, прорезать паз,
    /// переименовать, переложить в другую группу.</summary>
    private sealed class Board
    {
        public string Name;
        public Vector3 CentreMm;
        public Vector3 SizeMm;
        public Vector3 NudgeUnits;
        public Quaternion Turn = Quaternion.identity;
        public int GroupId;
        public ElementKind Kind;
        public Face[] Seats = System.Array.Empty<Face>();

        public Board(string name, Vector3 centreMm, Vector3 sizeMm)
        {
            Name = name;
            CentreMm = centreMm;
            SizeMm = sizeMm;
        }

        public ValidationElement Snapshot()
        {
            Vector3 centre = CentreMm * MM + NudgeUnits;
            Vector3 size = SizeMm * MM;
            var box = ElementGeometry.Box(Name, centre, size, Turn);
            var geometry = Seats.Length == 0
                ? box
                : new ElementGeometry(box.Id, box.Name, box.Faces, Seats,
                    System.Array.Empty<Face>(), box.Min, box.Max, box.IsPanel);
            return new ValidationElement(geometry, Corners(centre, size, Turn), Kind,
                GroupId, null, Span.FromCenter(centre.y, size.y), ValidationElement.NoIndex);
        }

        private static Vector3[] Corners(Vector3 centre, Vector3 size, Quaternion turn)
        {
            var half = size * 0.5f;
            var verts = new Vector3[8];
            int i = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        verts[i++] = centre + turn * new Vector3(
                            half.x * sx, half.y * sy, half.z * sz);
            return verts;
        }
    }

    /// <summary>Корпус на полу плюс одинокая доска в воздухе: в ответе есть и
    /// контакты, и нарушение «не подпёрта», и изолированная группа — то есть
    /// устаревший ответ здесь заметен во всех трёх разделах отпечатка.</summary>
    private static List<Board> Cabinet() => new List<Board>
    {
        new Board("Floor", new Vector3(300, -9, 0), new Vector3(6000, 18, 4000))
            { Kind = ElementKind.Anchor | ElementKind.FloorAnchor },
        new Board("SideL", new Vector3(9, 360, 0), new Vector3(18, 720, 560)),
        new Board("SideR", new Vector3(591, 360, 0), new Vector3(18, 720, 560)),
        new Board("Bottom", new Vector3(300, 9, 0), new Vector3(564, 18, 560)),
        new Board("Shelf", new Vector3(300, 400, 0), new Vector3(564, 18, 560)),
        new Board("Top", new Vector3(300, 739, 0), new Vector3(600, 38, 600)),
        new Board("Loose", new Vector3(3000, 1200, 0), new Vector3(400, 18, 400)),
    };

    private static Board Named(List<Board> boards, string name)
    {
        foreach (var board in boards)
            if (board.Name == name) return board;
        throw new AssertionException($"в сцене нет детали {name}");
    }

    private static List<ValidationElement> SnapshotOf(List<Board> boards)
    {
        var scene = new List<ValidationElement>(boards.Count);
        foreach (var board in boards) scene.Add(board.Snapshot());
        return scene;
    }

    /// <summary>Кадр приложения в миниатюре: снимок собирается заново (новые
    /// массивы с теми же числами — так кэш не может выехать на совпадении
    /// ссылок), калитка решает, считать ли, и потребитель читает ответ.</summary>
    private sealed class Frames
    {
        private readonly ValidationReuse _reuse = new ValidationReuse();
        private readonly CoreValidationResult _answer = new CoreValidationResult();

        public string Frame(List<Board> boards)
        {
            var scene = SnapshotOf(boards);
            if (_reuse.NeedsAFreshAnswer(scene)) ValidationCore.Validate(scene, _answer);
            return ValidationTestScene.Fingerprint(_answer, scene);
        }

        public int TakeFullValidations() => _reuse.TakeFullValidations();
    }

    private static string ColdAnswer(List<Board> boards)
    {
        var scene = SnapshotOf(boards);
        return ValidationTestScene.Fingerprint(ValidationCore.Validate(scene), scene);
    }

    /// <summary>Общий обратный вход: закрепить опорное значение, изменить сцену
    /// названным способом, потребовать ровно один пересчёт И совпадение с
    /// полным проходом.
    ///
    /// Вторая проверка важнее первой. Счётчик пересчётов отвечает «работа была»
    /// и ничего не говорит о том, ПРАВИЛЬНЫЙ ли ответ лежит в кэше; отпечаток
    /// спрашивает сам ответ — нарушения, диагностики, контакты, группы.</summary>
    private static void AssertTheChangeIsSeen(string what, System.Action<List<Board>> change)
    {
        var boards = Cabinet();
        var frames = new Frames();

        frames.Frame(boards);
        frames.TakeFullValidations();

        frames.Frame(boards);
        int atRest = frames.TakeFullValidations();
        Assert.AreEqual(0, atRest,
            $"{what}: ОПОРНОЕ значение не закрепилось — неизменённая сцена второй раз "
            + $"стоила {atRest} валидаций. Кэша нет вовсе, и разница ниже ничего не докажет");

        change(boards);
        string after = frames.Frame(boards);
        int afterChange = frames.TakeFullValidations();

        Assert.AreEqual(1, afterChange,
            $"{what}: вход валидации изменился, а пересчёта случилось {afterChange}. "
            + "Ноль здесь — это устаревшие нарушения на экране: молчаливая ложь "
            + "пользователю, которая дороже любых сэкономленных миллисекунд");
        Assert.AreEqual(ColdAnswer(boards), after,
            $"{what}: ответ после пересчёта разошёлся с полным проходом по той же сцене");
    }

    /// <summary>Кадр покоя: сцена размера живого проекта (413 деталей), десять
    /// кадров подряд — одна валидация. Было — десять.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_TheRestingSceneIsValidatedOnceInTenFrames()
    {
        var reuse = new ValidationReuse();
        var answer = new CoreValidationResult();

        for (int frame = 0; frame < 10; frame++)
        {
            var scene = ValidationTestScene.Kitchen(82);
            if (reuse.NeedsAFreshAnswer(scene)) ValidationCore.Validate(scene, answer);
        }

        int validations = reuse.TakeFullValidations();
        TestContext.WriteLine($"кадров покоя 10, полных валидаций {validations}");
        Assert.AreEqual(1, validations,
            $"десять кадров покоя стоили {validations} полных валидаций сцены из 413 "
            + "деталей. Каждая лишняя — это 17,3 мс дампа пользователя, потраченные "
            + "на пересчёт того же самого ответа");
    }

    /// <summary>Положительный контроль к предыдущему: первый кадр обязан
    /// считать. Без него «одна валидация на десять кадров» получалось бы и
    /// у калитки, которая не считает НИКОГДА.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_TheFirstFrameEverIsValidated()
    {
        var reuse = new ValidationReuse();

        bool fresh = reuse.NeedsAFreshAnswer(ValidationTestScene.Kitchen(4));

        Assert.IsTrue(fresh, "первый кадр считать нечего было заранее — ответа ещё нет");
        Assert.AreEqual(1, reuse.TakeFullValidations(),
            "первый кадр обязан числиться полной валидацией");
    }

    /// <summary>Ответ кадра покоя — не «замороженный на всякий случай», а тот
    /// же, что даёт полный проход. Счётчик работы этого не проверяет.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_TheReusedAnswerMatchesAColdPass()
    {
        var boards = Cabinet();
        var frames = new Frames();

        frames.Frame(boards);
        string reused = frames.Frame(boards);

        Assert.AreEqual(ColdAnswer(boards), reused,
            "ответ, взятый из кэша на кадре покоя, разошёлся с полным проходом");
    }

    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_AMovedPartIsSeen() =>
        AssertTheChangeIsSeen("сдвиг",
            boards => Named(boards, "Shelf").CentreMm += new Vector3(0f, 60f, 0f));

    /// <summary>Сдвиг на 0,001 мм. Unity-шный <c>Vector3 ==</c> приблизителен
    /// (порог около 1e-5), и ключ, собранный на нём, проглотил бы это молча —
    /// <c>conventions/CORRECTNESS.md</c> → «Признак „значение не менялось“
    /// сравнивает числа, а не объекты». Ответ валидации от такого сдвига не
    /// меняется, и в этом весь смысл теста: он ловит не неверный вердикт, а
    /// СЛЕПОЙ ключ, который завтра проглотит сдвиг покрупнее.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_AMoveTooSmallForVectorEqualityIsSeen() =>
        AssertTheChangeIsSeen("сдвиг на 0,001 мм",
            boards => Named(boards, "Shelf").NudgeUnits = new Vector3(0f, 1e-6f, 0f));

    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_ATurnedPartIsSeen() =>
        AssertTheChangeIsSeen("поворот", boards => Named(boards, "Top").Turn = Yaw90);

    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_AResizedPartIsSeen() =>
        AssertTheChangeIsSeen("изменение размера",
            boards => Named(boards, "Shelf").SizeMm += new Vector3(0f, 0f, 40f));

    /// <summary>Зазор — это не тот же вход, что размер: он сдвигает ОДНУ грань,
    /// оставляя противоположную на месте, поэтому центр и габарит меняются
    /// вместе и наполовину.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_AGapTakenOffOneSideIsSeen() =>
        AssertTheChangeIsSeen("зазор слева 4 мм", boards =>
        {
            var board = Named(boards, "Shelf");
            board.SizeMm -= new Vector3(4f, 0f, 0f);
            board.CentreMm += new Vector3(2f, 0f, 0f);
        });

    /// <summary>Паз живёт в <c>GrooveSeatFaces</c> — отдельном массиве, которого
    /// прежнее сравнение снимков не касалось вовсе. Прорезанный паз не двигает
    /// ни позу, ни габарит: ключ, собранный из одной позы, не увидел бы его
    /// никогда.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_AGrooveCutIntoAPartIsSeen() =>
        AssertTheChangeIsSeen("паз", boards => Named(boards, "Bottom").Seats = new[]
        {
            new Face(new Vector3(0.3f, 0.012f, 0f), Vector3.up, new Vector2(0.564f, 0.56f),
                Vector3.right, Vector3.forward),
        });

    /// <summary>Переименование — тот самый тихий вход, из-за которого калитка на
    /// ревизии сцены здесь не годится: <c>DrawerLinks.Rename</c> не трогает ни
    /// <c>transform</c>, ни состав реестра, и ревизия не двигается ничем. Имя
    /// при этом несущее: по нему валидация связывает пару ящиков, винтовую опору
    /// с хозяином и проём со стеной.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_ARenamedPartIsSeen() =>
        AssertTheChangeIsSeen("переименование",
            boards => Named(boards, "Shelf").Name = "Shelf_renamed");

    /// <summary>Группа не видна ни в позе, ни в геометрии, а вердикт меняет:
    /// ящик и корпус одного модуля вправе делить объём, разных — нет.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_ARegroupedPartIsSeen() =>
        AssertTheChangeIsSeen("смена группы", boards => Named(boards, "Shelf").GroupId = 7);

    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_AnAddedPartIsSeen() =>
        AssertTheChangeIsSeen("добавление детали", boards => boards.Add(
            new Board("Added", new Vector3(300, 800, 0), new Vector3(400, 18, 400))));

    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_ARemovedPartIsSeen() =>
        AssertTheChangeIsSeen("удаление детали",
            boards => boards.Remove(Named(boards, "Shelf")));

    /// <summary>Замена детали на другую при том же числе деталей: счёт списка
    /// совпадает, и ключ, сверяющий только длину, промолчал бы.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_APartSwappedForAnotherIsSeen() =>
        AssertTheChangeIsSeen("замена детали при том же счёте", boards =>
        {
            int at = boards.IndexOf(Named(boards, "Loose"));
            boards[at] = new Board("Other", new Vector3(3000, 1200, 0),
                new Vector3(500, 18, 400));
        });

    /// <summary>Вершины — отдельное поле входа, а не пересказ коробки: по ним
    /// <c>GrooveSeating</c> решает, насколько глубоко панель села в паз. Сцена
    /// из <see cref="Cabinet"/> их не разводит (коробка и вершины там строятся
    /// из одних чисел), поэтому вход проверяется в лоб — два снимка, у которых
    /// различаются ТОЛЬКО вершины.</summary>
    [Test]
    public void ValidationElement_ValidatesTheSameAs_DiffersWhenOnlyTheVerticesMoved()
    {
        var box = ElementGeometry.Box("Panel", new Vector3(0.3f, 0.4f, 0f),
            new Vector3(0.564f, 0.018f, 0.56f));
        var span = Span.FromCenter(0.4f, 0.018f);
        var here = new Vector3[] { new Vector3(0f, 0f, 0f) };
        var there = new Vector3[] { new Vector3(0f, 0.001f, 0f) };

        var a = new ValidationElement(box, here, ElementKind.None, 0, null, span,
            ValidationElement.NoIndex);
        var b = new ValidationElement(box, there, ElementKind.None, 0, null, span,
            ValidationElement.NoIndex);

        Assert.IsTrue(a.ValidatesTheSameAs(a),
            "положительный контроль: снимок обязан совпадать сам с собой");
        Assert.IsFalse(a.ValidatesTheSameAs(b),
            "вершины разъехались на 1 мм, а признак говорит «не менялось» — "
            + "посадка панели в паз будет судиться по вчерашним вершинам");
    }

    /// <summary>Осевая стены решает, законно ли две стены делят объём
    /// (<c>WallCentreline.MeetAtSharedCorner</c>). Её тоже нет в
    /// <see cref="Cabinet"/>: стены там нет вовсе.</summary>
    [Test]
    public void ValidationElement_ValidatesTheSameAs_DiffersWhenOnlyTheWallCentrelineTurned()
    {
        var box = ElementGeometry.Box("Wall", Vector3.zero, new Vector3(0.1f, 2.5f, 3f));
        var span = Span.FromCenter(0f, 2.5f);
        var dims = new Vector3Int(100, 2500, 3000);
        var along = WallCentreline.Of(Vector3.zero, Quaternion.identity, dims);
        var across = WallCentreline.Of(Vector3.zero, Yaw90, dims);
        var verts = new Vector3[] { Vector3.zero };

        var a = new ValidationElement(box, verts, ElementKind.Anchor, 0, null, span,
            ValidationElement.NoIndex, default, false, ValidationElement.NoIndex, along);
        var b = new ValidationElement(box, verts, ElementKind.Anchor, 0, null, span,
            ValidationElement.NoIndex, default, false, ValidationElement.NoIndex, across);

        Assert.IsTrue(a.ValidatesTheSameAs(a),
            "положительный контроль: снимок обязан совпадать сам с собой");
        Assert.IsFalse(a.ValidatesTheSameAs(b),
            "осевая стены развернулась, а признак говорит «не менялось» — угол двух "
            + "стен останется засчитанным (или незасчитанным) по вчерашней осевой");
    }

    /// <summary>Перестановка двух деталей местами: значения те же, порядок
    /// другой, а ответ адресует детали ИНДЕКСАМИ. Кэш, не заметивший
    /// перестановки, назвал бы нарушителем соседа.</summary>
    [Test]
    public void ValidationReuse_NeedsAFreshAnswer_TwoPartsSwappedInOrderAreSeen() =>
        AssertTheChangeIsSeen("перестановка деталей в списке", boards =>
        {
            (boards[1], boards[2]) = (boards[2], boards[1]);
        });
}
