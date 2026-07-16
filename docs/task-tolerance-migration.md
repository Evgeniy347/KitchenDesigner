# Задача: централизовать допуски (epsilon) в геометрических сравнениях float

## Цель

В коде рассыпаны «магические» допуски: `1e-4f`, `0.001f`, `0.02f`, `0.5f`, `0.999f`,
`Mathf.Approximately` (~40 мест в ~15 файлах `Assets/Scripts/Core`). Нужно собрать их
в один класс с именованными константами и семантическими хелперами и перевести
геометрические проверки на него. Поведение приложения при этом меняться НЕ должно —
это рефакторинг читаемости и единообразия, не изменение логики.

## Шаг 1. Создать класс допусков

Файл `Assets/Scripts/Core/Infrastructure/Tolerance.cs`:

```csharp
namespace KitchenDesigner.Core
{
    /// <summary>Единые геометрические допуски. Все «на глаз подобранные» epsilon
    /// живут здесь под осмысленными именами. Мир — метры (юниты), детали — мм.</summary>
    public static class Tolerance
    {
        /// <summary>Контакт деталей: |зазор| меньше — «касаются» (0.5 мм).
        /// Тот же порог, что ContactDistMM / GapEpsilonMm / MinOverlapMm.</summary>
        public const float ContactMm = 0.5f;

        /// <summary>Геометрический шум float в юнитах (= 0.1 мм). Меньше этого —
        /// координаты считаются равными.</summary>
        public const float EpsilonUnits = 1e-4f;

        /// <summary>Порог параллельности нормалей: |dot| >= этого — грани параллельны.</summary>
        public const float ParallelDot = 0.999f;

        /// <summary>Равенство координат/расстояний в юнитах (метрах).</summary>
        public static bool ApproxEqual(float a, float b) =>
            UnityEngine.Mathf.Abs(a - b) < EpsilonUnits;

        /// <summary>Значение в мм пренебрежимо мало (шум, не зазор и не пересечение).</summary>
        public static bool IsNoiseMm(float mm) =>
            UnityEngine.Mathf.Abs(mm) < ContactMm;

        /// <summary>Строгое пересечение интервалов [min1,max1] и [min2,max2] в юнитах
        /// с допуском: плотный контакт торцами НЕ считается пересечением.</summary>
        public static bool IntervalsOverlap(float min1, float max1, float min2, float max2) =>
            min1 < max2 - EpsilonUnits && max1 > min2 + EpsilonUnits;

        /// <summary>Нормали параллельны (сонаправлены или противонаправлены).</summary>
        public static bool IsParallel(float dot) =>
            UnityEngine.Mathf.Abs(dot) >= ParallelDot;
    }
}
```

Если при миграции встретится проверка, не сводимая к этим хелперам, но с магическим
epsilon — добавь в Tolerance новую именованную константу с комментарием, откуда взято
значение. НЕ подгоняй чужую проверку под неподходящий хелпер.

## Шаг 2. Мигрировать файлы (по одному, с прогоном тестов после каждого)

Файлы к миграции (в этом порядке):

1. `Core/Validation/ConstraintValidator.cs` — `IntersectEpsilon`, `ContactDistMM`, dot `0.999f`.
   ВНИМАНИЕ: `FaceToFaceOverlap = 0.5f` — это ДОЛЯ перекрытия (ratio), НЕ мм. Не трогать.
2. `Core/Elements/FacadeValidator.cs` — `MinOverlapMm` → `Tolerance.ContactMm`.
3. `Core/MCP/McpCommandHandler.cs` — `GapEpsilonMm` → `Tolerance.ContactMm`,
   `ProjectionsOverlapExceptAxis` → `Tolerance.IntervalsOverlap`.
4. `Core/Snap/SnapSystem.cs` — локальные epsilon и dot-пороги.
   ВНИМАНИЕ: порог прилипания из настроек (`SnapThreshold`) — НЕ epsilon, не трогать.
5. `Core/Snap/ResizeSnap.cs`, `Core/Snap/ResizeHandleManager.cs`.
6. `Core/Elements/Wall.cs`, `Core/Elements/DrawerValidator.cs`, `Core/Elements/DrawerElement.cs`,
   `Core/Elements/FacadeElement.cs`.

Для каждого файла:
- заменить голое сравнение/локальную константу на константу или хелпер из Tolerance,
  ТОЛЬКО если семантика совпадает точно (см. правила ниже);
- если значение отличается от констант Tolerance (например `0.02f` в ElementMover) —
  НЕ приводить его к 0.5 мм; вынести как новую именованную константу с исходным значением;
- прогнать EditMode-тесты: `build.cmd -RunTests` (или Unity CLI `-runTests -testPlatform EditMode`);
  все тесты, проходившие до твоего изменения, должны проходить и после. Список заранее
  упавших тестов зафиксируй до начала работы и не сравнивай с ним свои изменения.

## Правила: что МЕНЯТЬ и что НЕ ТРОГАТЬ

МЕНЯТЬ (семантика «геометрическое совпадение/контакт/пересечение»):
- сравнение мировых координат/расстояний на равенство (`==`, `Mathf.Approximately`,
  `Mathf.Abs(a-b) < 0.0001f` и т.п.) → `Tolerance.ApproxEqual`;
- проверки пересечения интервалов/AABB с локальным epsilon → `Tolerance.IntervalsOverlap`;
- проверки «перекрытие/зазор меньше N мм — игнорировать» → `Tolerance.IsNoiseMm` / `ContactMm`;
- сравнения dot-произведений нормалей с 0.99…f → `Tolerance.IsParallel`.

НЕ ТРОГАТЬ (менять их — вносить баги):
- компараторы сортировок (`Sort`, `CompareTo`, `OrderBy`) — epsilon ломает транзитивность;
- поиск минимума/максимума (`if (x < best) best = x`) — строгие сравнения корректны;
- любые операции с int (размеры в мм — int, счётчики, индексы, id);
- условия циклов, прогресс анимации (0..1), таймеры, FPS;
- UI-раскладку (координаты панелей/кнопок);
- пороги из пользовательских настроек (SnapThreshold, GridStep);
- `FaceToFaceOverlap` и другие ДОЛИ (ratio 0..1);
- файлы вне списка выше (в т.ч. Rendering/* — там epsilon завязаны на визуал).

## Критерии готовности

- Класс Tolerance создан, все константы задокументированы (XML-doc, откуда значение).
- В файлах из списка не осталось несемантических магических epsilon
  (проверка: `grep -rnE "1e-[0-9]|0\.9{3}f|Mathf\.Approximately" <файлы>`).
- Поведение не изменилось: полный EditMode-прогон — без новых падений относительно
  зафиксированного базового списка.
- Один коммит на 1–2 файла, в сообщении — какой файл и какие константы заменены.
