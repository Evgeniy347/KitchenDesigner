# Вынос геометрического ядра из Unity и мутационное тестирование

**Дата:** 13.08.2026
**Unity:** 6000.4.3f1
**Статус:** все этапы выполнены (0 снят как потерявший смысл — см. §6.4)

| Метрика | Было | Стало |
|---|---|---|
| Тесты ядра под `dotnet` | — | **187 за 143 мс** |
| Mutation score ядра | — | **71.5%** (порог 66 в `tools/mutation-test.ps1`) |
| `SnapMutationTests` | 160.9 с | **124.6 с** |
| Инвариант снэпа | — | совпал во всех прогонах, кроме одного пойманного бага |
| Инвариант валидации | — | 10 чисел по замороженной сцене (§6.2) |
| Цикл «правка → проверка», один класс | ~2 мин | **3 с** (§8.1) |
| Полная проверка перед коммитом | ~20 мин | **6,5 мин** (§8.1) |

---

## 1. Итог одним абзацем

Геометрическое ядро (снэп, ресайз, пазы, допуски) выносится в отдельную сборку
`KitchenDesigner.Geometry`, которая собирается ДВАЖДЫ из одних и тех же исходников:
Unity — по asmdef, `dotnet` — по glob в csproj. Ядро работает со снимками
геометрии вместо `MonoBehaviour`, поэтому исполняется вне Unity. Это даёт
мутационное тестирование через штатный `dotnet-stryker` (без форков и обвязки),
прогон тестов ядра за миллисекунды вместо минут и убирает из снэпа запись в
`transform`, в которую упирается производительность `SnapMutationTests`.

Объём: 48–68 ч на этапы 1–6.

---

## 2. Исходное состояние: покрытие

Замер: Unity Code Coverage 1.2.6,
`Unity.exe -runTests -batchMode -enableCodeCoverage -coverageOptions "generateAdditionalMetrics;generateHtmlReport;assemblyFilters:+KitchenDesigner.Runtime"`

| Режим | Line coverage | Method coverage | Классов | Файлов | Тестов |
|-------|---------------|-----------------|---------|--------|--------|
| EditMode | 68% (12418/18258) | 69.4% (1937/2789) | 296 | 195 | 1812 |
| PlayMode | 55.5% (10150/18258) | 60.6% (1691/2789) | 296 | 195 | 74 |
| Union (оценка) | ~78% | ~78% | 296 | 195 | — |

Отчёты: `test-results/coverage/{editmode,playmode}/results/Report/Summary.md`.
Union получен оценкой; Unity Code Coverage умеет сливать оба режима в один
отчёт — при следующем замере брать реальное число, а не оценку.

**Пробелы (union < 15%):**

| Класс | Строк | Union% |
|-------|-------|--------|
| ElementMover | 355 | 13.2% |
| TextureOverlayHandles | 245 | 9% |
| ResizeHandleManager | 271 | 5.1% (EditMode) |
| UIManager | 359 | 0% (EditMode) |
| SidebarUI | 150 | 0% (EditMode) |

46 классов имеют 100% покрытие.

**Дописано (+70 тестов, все зелёные):**

| Файл | Было | Стало |
|------|------|-------|
| `Assets/Tests/EditMode/ElementMoverTests.cs` | 3 | 26 |
| `Assets/Tests/EditMode/ResizeHandleManagerTests.cs` | 0 | 25 |
| `Assets/Tests/EditMode/TextureOverlayTests.cs` | 40 | 62 |

**Порядок работ по покрытию.** Мутационное тестирование отвечает на вопрос
«тесты есть, но проверяют ли они что-нибудь», поэтому окупается там, где покрытие
УЖЕ высокое: `SnapSystem`, `ResizeSnap`, `ConstraintValidator`. Классам с 0%
мутанты ничего нового не скажут — и так видно, что они не покрыты, а тесты на
главный UI-контроллер дороги и дают мало дефектов. Сначала ядро (этапы 1–6),
мутационный порог, и только потом решение по UI-покрытию.

---

## 3. Ограничение платформы: что исполняется вне Unity

### 3.1. Компиляция — проблем нет

| Сборка | Файлов | Результат |
|--------|--------|-----------|
| `KitchenDesigner.Runtime` | 206 | 0 ошибок, 716 предупреждений (CS8632, CS0618) |
| Тестовая сборка | 132 | 0 ошибок, 0 предупреждений |

Резолвятся все зависимости: `UnityEngine.*Module.dll`, `Newtonsoft.Json.dll`,
`Unity.TextMeshPro`, `Unity.RenderPipelines.*`, `UnityEngine.UI`,
`Unity.Mathematics`, `Unity.Collections`, `Unity.Burst`.

### 3.2. Исполнение — ECall падает на ВЫЗОВЕ, а не на загрузке

`UnityEngine.CoreModule.dll` грузится под CoreCLR нормально.
`SecurityException: ECall methods must be packaged into a system module`
возникает только при вызове конкретного extern-метода
(`[MethodImpl(MethodImplOptions.InternalCall)]` — прямой вызов в нативный движок).

Проверено на .NET 8 (спайк `test-results/tmp/spike-noengine/A-unityengine`):

| Исполняется под `dotnet` | Падает (ECall) |
|---|---|
| `Vector3`: арифметика, `Dot`, `Cross`, `Min/Max`, `magnitude`, `normalized`, `Distance` | `Quaternion.Euler` |
| `Vector2`, `Vector3Int` | `Quaternion.AngleAxis` |
| `Rect`, `Bounds.SetMinMax` | `Quaternion.LookRotation` |
| `Mathf`: `Abs`, `Max`, `Min`, `Clamp`, `RoundToInt`, `Sqrt`, `Approximately` | `Quaternion.Inverse` |
| `Quaternion.identity`, `Quaternion * Vector3`, `Quaternion.Angle` | `Matrix4x4.TRS` |
| | `new GameObject()` и всё сценовое |

**Следствие:** ядро пользуется привычными `Vector3`/`Mathf`/`Rect`/`Bounds`, а
`Quaternion` — как ДАННЫМИ (поворот вектора работает). Запрещено только
КОНСТРУИРОВАТЬ повороты. Переезд на `Unity.Mathematics` не требуется.

### 3.3. Ядро почти не связано с Unity

| Файл | Связь с Unity |
|---|---|
| `Snap/ResizeSnap.cs` | нет |
| `Snap/ResizeMath.cs` | нет |
| `Snap/FaceContact.cs` | нет (только поля типа `KitchenElement`) |
| `Elements/GrooveMesh.cs` | нет |
| `Infrastructure/Tolerance.cs` | только `Mathf.Abs` |
| `Elements/GappedBox.cs` | нет (2 упоминания `transform` — в комментариях) |
| `Snap/SnapSystem.cs` | **только** round-trip `moved.transform.position` (строки 88, 164, 209, 589-590, 670), 2 `Debug.Log`, 1 чтение `other.transform.position` в диагностике |
| `Validation/ConstraintValidator.cs` | `GetComponent<>` для проверки типа, чтения `transform.position/localScale` |

Ни одного `Quaternion.Euler`, `Matrix4x4` или `Instantiate` в ядре снэпа нет.

---

## 4. Целевое состояние проверено сквозным спайком

`test-results/tmp/spike-noengine/Geometry` — `Tolerance` перенесён дословно,
`ResizeSnap` и `ResizeMath` переведены с `KitchenElement` на снимок геометрии.
`Geometry.Tests` — 7 тестов на NUnit 3.

| Проверка | Результат |
|---|---|
| `dotnet build` | OK |
| `dotnet test` | 7/7, **21 мс** |
| `dotnet-stryker 4.16` | 133 мутанта, 101 протестирован, **43 с**, score 40% |

### 4.1. Какой сигнал даёт мутационное тестирование

Выжившие мутанты приходятся ровно на пороги, у которых в коде стоит комментарий
«так сделано, потому что был баг»:

| Место | Мутация | Что возвращает |
|---|---|---|
| `ResizeSnap.Overlap` | `<=` → `<` | Баг «касание ровно по ребру не считается контактом»: растягиваемая деталь проезжает мимо кромки соседа, не прилипая ни на одном миллиметре |
| `ResizeSnap.Overlap` | `&&` → `\|\|` | Перекрытие проверяется по одной оси из двух |
| `ResizeSnap.SnapDelta` | `>` → `>=` | Ломает инклюзивность порога, ради которой заведён `ThresholdEpsilon` |
| `ResizeMath.Compute` | `&&` → `\|\|` | Снимает защиту «округлённая грань не заходит за снэп-плоскость» |
| `Tolerance.IsParallel` | `>=` → `>` | Граничный случай параллельности нормалей |

Score 40% посчитан против семи тестов спайка, а не против реального набора —
реальные тесты часть этих мутантов убьют. Значим здесь класс сигнала: пороги,
за которыми стоят уже исправленные баги, регрессионными тестами не защищены.

---

## 5. Целевая архитектура

### 5.1. Одни и те же исходники, две сборки

```
Assets/Scripts/Core/Geometry/          ← asmdef KitchenDesigner.Geometry
   Tolerance.cs  Face.cs  ElementGeometry.cs
   ResizeSnap.cs  ResizeMath.cs  SnapCore.cs
   ValidationCore.cs (ElementKind, ValidationElement)
   GrooveMath.cs  GappedBox.cs  EdgeBanding.cs

Assets/Scripts/Core/**                 ← asmdef KitchenDesigner.Runtime
   KitchenElement, адаптер SnapSystem, UI, MCP, персистентность
   ссылается на KitchenDesigner.Geometry

geometry/KitchenDesigner.Geometry.csproj
   <Compile Include="..\Assets\Scripts\Core\Geometry\**\*.cs" />
   <Reference Include="UnityEngine.CoreModule" />

geometry/KitchenDesigner.Geometry.Tests.csproj
   <Compile Include="..\Assets\Tests\EditMode\Geometry\**\*.cs" />
   NUnit 3.14
```

Дублирования исходников нет. Тесты ядра лежат в `Assets/Tests/EditMode/Geometry/`
и исполняются ОБОИМИ раннерами.

**NUnit пинится на 3.x**: в Unity именно эта ветка, а NUnit 4 убрал
`Assert.IsTrue`/`Assert.IsFalse` — на 4.x тесты перестанут быть переносимыми
между раннерами.

### 5.2. Граница: снимок вместо компонента

```csharp
public readonly struct ElementGeometry
{
    public readonly string Name;
    public readonly Face[] Faces;
    public readonly Face[] GrooveSeatFaces;
    public readonly Face[] GrooveWallFaces;
    public readonly Vector3 Min, Max;   // AABB
    public readonly bool IsPanel;
}
```

`KitchenElement.ToGeometry(Vector3 atPosition)` строит снимок для ЗАДАННОЙ
позиции. Подтипы (`FacadeElement`/`GappedBox`, `DrawerElement`) переопределяют
его так же, как сейчас переопределяют `GetFaces`, — их специфика остаётся
в `Runtime`.

Публичные сигнатуры не ломаются:
`SnapSystem.TrySnap(KitchenElement, List<KitchenElement>, Vector3)` остаётся
тонким адаптером, который собирает снимки и зовёт ядро.

### 5.3. Побочный эффект: производительность

Из ядра исчезает round-trip `moved.transform.position = basePos` — снимок и так
строится для нужной позиции. За один прогон `SnapMutationTests` это ~2 млн
мутаций графа сцены.

Замеры показали, что именно сюда упирается тест: точечные оптимизации не
уменьшали общее время, а перекладывали его между функциями (убираешь вызов
`TrySnap` из `Diagnose` — ровно на столько же дорожает оставшийся `TrySnap`,
хотя его код не менялся). Причина — запись в трансформ грязнит поддерево, а
пересчёт оплачивает тот, кто следующим читает геометрию. Аллокации ни при чём:
за фазы свипа всего 48 и 98 сборок мусора.

### 5.4. Глобальное состояние, которое уезжает в параметры

- `KitchenSettings.Instance.SnapEnabled` / `.SnapThreshold` — читаются внутри
  `TrySnap` (строки 83, 86), становятся аргументами ядра.
- `SnapSystem.VerboseLog` + `Debug.Log` — необязательный `Action<string>`,
  либо логи собирает адаптер.
- `FaceCache` — статический кэш, включаемый из теста. В ядре не нужен: снимки
  соседей строятся один раз вызывающим кодом.

---

## 6. Этапы

Каждый этап заканчивается зелёным полным прогоном (EditMode + PlayMode + ASP.NET).

| # | Этап | Часы | Результат |
|---|---|---|---|
| 0 | Ручная проверка сигнала: 40–60 курируемых мутаций по `SnapSystem`/`ResizeSnap` против РЕАЛЬНОГО набора тестов | 6–8 | Доля выживших на реальных тестах — обоснование объёма работ цифрой |
| 1 | ✅ **Сделано.** Каркас двойной сборки: asmdef `Geometry`, `geometry/Geometry.csproj` + `Geometry.Tests.csproj`, перенос `Tolerance`, 20 тестов, архитектурный сторож | 4–6 | Unity 17/17 и `dotnet test` 20/20 (31 мс); Stryker **100%** на `Tolerance`; инвариант `SnapMutationTests` цел |
| 2 | ✅ **Сделано.** `AppConstants`, `GrooveSpec`, `DrawerConstants` → ядро; `Face` из вложенного типа `KitchenElement` в самостоятельный тип ядра (54 ссылки в 14 файлах); контракт порядка граней закреплён тестом | 8–10 | 59 тестов ядра за 53 мс; Stryker **90.6%**; инвариант `SnapMutationTests` цел |
| 2* | Остаток стадии 2, съехавший на этап 3: `GrooveMath` (расщепление `GrooveMesh`), `GappedBox`, `EdgeBanding`, `FaceContact`. Причина — они тянут `PartData`, а тот через `MaterialCatalog` тянет загрузку текстур; развязывается это тем же снимком, что и этап 3 | — | — |
| 3 | ✅ **Сделано.** `ElementGeometry` + `BoxGaps` + `ToGeometry()`; `ResizeSnap`, `ResizeMath`, `GappedBox` → ядро на снимках. Адаптеры не понадобились: вызовов оказалось 8, переписаны напрямую | 8–12 | Ресайз-математика не видит сцену; инвариант цел. Mutation score временно упал до 21%: тесты перевезённого кода ещё в Unity-части — это работа этапа 5 |
| 4 | ✅ **Сделано.** Запись в `transform` убрана (`GetFacesAt`/`GetVerticesAt`/`ValidationPositionAt` во всех шести классах со своей геометрией); `Collect`, `TryPickCandidate`, `FacesOverlap`, `GetFaceRect`, `BestEdgeDelta` переехали в `SnapCore`. `SnapSystem` остался адаптером сцены (настройки, снимки, `Diagnose`, логи) | 16–24 | Ядро снэпа собирается и тестируется под `dotnet`. Тест: **160.9 → 124.6s**; `TrySnap` 79.6→51.8s, `Diagnose` 82→26s, `ResizeMath` 22.6→3.7s |
| 5 | ✅ **Сделано.** Сначала `ResizeSnapTests`, `ResizeMathTests`, `SnapPostEdgeDetentTests`, `SnapKnownLimitationTests` + база `SnapCoreTestBase`; затем ветки пазов (`GroovedGeometry`) и переносы `SnapCoreEdgeCase/LineContact/ExistingContact` — см. §6.4 | 8–10 | 187 тестов ядра за 143 мс; `SnapCore` 33.7 → 64.1%, `ResizeSnap` 35.1 → 69.1%, `ResizeMath` 65.8 → 94.7% |
| 6 | ✅ **Сделано.** `tools/mutation-test.ps1`: тесты ядра + Stryker с порогом (`--break-at`), проверен в обе стороны — при 45 проходит, при 95 роняет прогон | 4–6 | Порог как gate; baseline **48.9%** |
| 7 | ✅ **Сделано.** Инвариант валидации (10 чисел, §6.2), `ElementKind` + `ValidationElement`, ядро `ValidationCore`; `ConstraintValidator` стал адаптером сцены | 16–24 | Валидация исполняется под `dotnet` и мутируется. Тестов ядра **101 → 140** (155 мс); mutation score **48.9 → 58.4%**, по самому `ValidationCore` — **71%** (465 мутантов, 12 без покрытия) |

### 6.1. Почему этап 7 был отдельной работой

Разведка (901 строка, 24 точки связи с Unity) показала, что `ConstraintValidator`
цепляется за движок иначе, чем снэп. У снэпа связь была ГЕОМЕТРИЧЕСКАЯ — «дай
грани в этой позе», и снимок её закрыл. Здесь связь СЕМАНТИЧЕСКАЯ: четырнадцать
решений вида «это пол или стена» (`IsAnchor`), «это проём» (окно/дверь), «это
мойка/варочная/светильник — пропустить», «это ящик», «это фасад с зазором»,
«панель села в паз этой доски».

Снимком геометрии это не выражается — нужна ВТОРАЯ абстракция границы.

Второе отличие важнее. Этапы 1–6 страховал инвариант `SnapMutationTests`: пять
чисел, которые обязаны совпасть. **Валидацию он не покрывает.** Её страхуют шесть
файлов (`ConstraintValidatorTests`, `FacadeValidatorTests`, `DrawerValidatorTests`,
`DrawerOpenValidationTests`, `AssembledFacadeValidationReproTests`,
`SaveValidationTests`) — сеть заметно реже. Поэтому этап начат не с переноса, а
с инварианта (§6.2).

Оценка поднята с 12–16 до 16–24 ч именно из-за этих двух пунктов.

### 6.3. Как решена семантика: `ElementKind`

Роль детали уехала в ядро отдельным набором флагов, а решение «кто есть кто»
осталось в сцене — ровно один файл, `Validation/ValidationSnapshot.cs`, где и
живут все `GetComponent` и проверки типа.

| Флаг | Что даёт |
|---|---|
| `Anchor` | корень BFS связности: пол, стена, проём в ней |
| `FloorAnchor` | пол штатно проходит ПОД стенами — пересечение якорей законно |
| `Opening` | окно/дверь: сидит в теле стены, но обязано в неё помещаться по высоте |
| `Drawer` | штатно пересекается с панелями своего модуля и с парным ящиком |
| `Decor` | светильник: ни пересечений, ни опоры |
| `Recessed` | мойка/варочная: врезана в столешницу, держится бортиком |
| `FloatingFacade` | фасад с зазором плавает в проёме — face-контакта не требует |

Флаги не взаимоисключающие: пол — это `Anchor|FloorAnchor`, окно —
`Anchor|Opening`. Два производных предиката (`IgnoredInPairs`, `NeedsNoSupport`)
собраны из них в самом ядре: раньше эти списки типов дублировались в трёх местах
(`ProcessPair`, `CheckConnectivity`, `FindNearContacts`) и расходились.

Снимок валидации — `ValidationElement`: геометрия + флаги + четыре скаляра, без
которых правила не работают (`GroupId`, имя парного ящика, габарит по высоте,
индекс своей стены). Стена разрешается ПО ИМЕНИ в адаптере, а в ядро приезжает
уже готовым индексом — имён ядро не знает вовсе.

Что осталось в адаптере `ConstraintValidator`: сборка снимков, перевод индексов
обратно в `KitchenElement` и два запроса «по требованию» (`FindNearContacts`,
`FindUnseatedPanels`), которые ходят по сцене напрямую и в горячий путь не
входят. Правила пересечений, контактов, связности и высоты проёмов — целиком в
ядре.

**Побочный эффект:** высота стены теперь всегда меряется от ЛОГИЧЕСКОЙ позы
(`Wall.FullPosition`), а не от трансформа. Раньше это делалось только внутри
проверки проёмов; теперь это свойство снимка, и подрезанная `WallCutaway` стена
не может дать ложное «окно вылезло за стену».

### 6.2. Инвариант валидации (сделано)

`Assets/Tests/EditMode/ValidationInvariantTests.cs` — аналог `SnapMutationTests`
для валидации: `docs/example.save.json` (274 детали) прогоняется через все три
валидатора, десять счётчиков обязаны совпасть до единицы.

```
elements 274 | contacts 1336 | violations 0 | isolatedGroups 0
kind.Overlap 0 | kind.Unsupported 0 | kind.OutOfWallBounds 0
drawer.errors 30 | facade.faceObstructions 0 | facade.openingViolations 63
```

По `ConstraintValidator` сцена чиста; 30 и 63 — реальные замечания
`DrawerValidator` (зазор 40 мм на сторону при допуске 12,5) и `FacadeValidator`
(траектория открывания). Это baseline реального сейва, а не эталон качества:
задача инварианта — поймать, что число поехало само.

Второй тест — детерминизм: два прогона `Validate` на одной сцене обязаны дать
одинаковые счётчики и одинаковый список нарушений. Валидатор держит статические
scratch-буферы и словарь broad-phase сетки; если их чистка сломается, baseline
начнёт плавать между прогонами, а мутационный прогон — давать ложные убийства.

Полный список нарушений пишется в `test-results/validation-invariant.log`, в
консоль идёт только таблица расхождений. Оба теста — 2,2 с.

**Правило приёмки этапа 7:** десять чисел обязаны совпасть, как пять чисел
`SnapMutationTests` — для этапов 1–6.

**Этапы 1–6: 48–68 ч.**

### 6.4. Этап 5: перенос тестов в ядро

Правила снэпа переехали в ядро ещё на этапе 4, а тесты на них оставались в
Unity — Stryker их не видел, и `SnapCore` показывал 33.7% при формально
покрытом коде. Перенесено на снимки:

| Файл ядра | Что закрыто | Score: было → стало |
|---|---|---|
| `ResizeSnap.cs` | `ResizeSnapGrooveTests` — дно паза (посадка вкладной панели) и стенки паза (разметочные детенты, двусторонние) | 35.1% → **69.1%** |
| `SnapCore.cs` | `SnapCoreGrooveTests`, `SnapCoreEdgeCaseTests`, `SnapCoreLineContactTests`, `SnapCoreExistingContactTests` | 33.7% → **64.1%** |
| `ResizeMath.cs` | `DimAlong`, `CenterForAppliedDims` (зажатый размер) | 65.8% → **94.7%** |

Снимки с пазами строит `GroovedGeometry` — тестовый двойник
`GetGrooveSeatFacesAt`/`GetGrooveWallFacesAt`: дно и стенки задаются числами,
без разбора `GrooveMesh`. Без него ветки пазов были недостижимы — это и был
главный блокер этапа.

Повороты в ядре собираются вручную (`RotX`/`RotY`/`RotZ`/`Euler` в
`SnapCoreTestBase`): `Quaternion.Euler` — вызов в нативный движок и под
CoreCLR падает.

**В Unity осталось намеренно:** чтение `KitchenSettings` (порог, вкл/выкл),
отключённые `SetActive(false)` детали и всё, что проверяет АДАПТЕР, а не
правила. Сценовые двойники перенесённых наборов оставлены как проверка пути
через сцену.

**Что не добито.** У `SnapCore` и `ValidationCore` остаётся ~110 и ~120
выживших мутантов. Заметная их часть неубиваема по построению: упаковка ключа
ячейки broad-phase, размер ячейки сетки, порядок дедупа пар (результат от них
не зависит — только скорость) и строки verbose-логов. Дальнейший рост score
здесь стоит дороже, чем даёт.

Этап 0 (ручная проверка сигнала на 40–60 курируемых мутациях) смысл потерял:
он обосновывал объём работ, а работы уже сделаны и обоснованы результатом.

### 6.5. Ловушка: вход инварианта обязан быть неподвижным

`ValidationInvariantTests` сначала читал `docs/example.save.json` — и покраснел
на −7 контактов при полностью нетронутом коде валидации. Причина: этот файл
ЖИВОЙ, его перезаписывает автосохранение десктопа и PlayMode-прогон (о том же
предупреждает комментарий в `PlayModeTestConfig`). Один прогон поменял в нём
режим окна — и baseline поехал.

Инвариант переведён на замороженную копию
`Assets/Tests/EditMode/Fixtures/validation-scene.save.json`. `SaveValidationTests`
по-прежнему читает `docs/example.save.json` намеренно: его задача — проверять
ТЕКУЩИЙ файл проекта, а не эталон.

---

## 7. Контроль регрессий

`SnapMutationTests` — главная защита от багов прилипания, и она даёт жёсткий
инвариант. За 7 прогонов разных конфигураций счётчики событий совпали до единицы:

```
Existing snap OK: 2121 | Big resize OK: 425 | Big move OK: 1280
Sweep snap events: 320835 | competition warnings: 0
```

**Правило приёмки каждого этапа:** эти пять чисел обязаны совпасть. Расхождение
хоть на единицу означает, что поведение снэпа поехало, — этап не принят.

Правило уже окупилось. На этапе 4 инвариант поймал двойное применение смещения
позы: `GetVertices()` звал `GetVerticesAt(ValidationPosition)`, а тот внутри
снова прогонял позицию через `ValidationPositionAt`. У обычной детали это
тождество, а у мойки и варочной панели бортик прибавлялся дважды. Проявилось
восемью `INTERSECT-AFTER-SNAP` на одной детали из 274 в диапазоне 45–53 мм;
счётчики ушли на 2120/320820. Остальные 1936 тестов молчали — они гоняют
детали, у которых поза валидации совпадает с трансформом.

**Время прогона показателем не считать.** На рабочей машине идентичный код давал
от 158 до 217 с (фон: Chrome, YandexDisk). Для перформансных замеров: три
прогона подряд, брать минимум, синхронизацию папки на время замеров
останавливать.

Дополнительно:

- **Архитектурный тест** в `Geometry.Tests`: запрещённые символы (`MonoBehaviour`,
  `GameObject`, `GetComponent`, `transform.`, `Debug.`, `Quaternion.Euler`,
  `Quaternion.AngleAxis`, `Quaternion.LookRotation`, `Quaternion.Inverse`,
  `Matrix4x4`) не встречаются в исходниках ядра. Без него Unity-сборка соберётся,
  а `dotnet` упадёт в рантайме на `SecurityException`.
- **Порядок граней — контракт** (`index/2` = ось, чётный индекс = положительное
  направление), от него зависит `ResizeHandleManager`. Зафиксировать тестом
  на этапе 1.
- **Снапшоты**: `ui_*.verified.json` принадлежат PlayMode, массовый
  `Move-Item *.candidate.json` их затирает (см. AGENTS.md).

---

## 8. CI

```powershell
# на каждый push — секунды
.\tools\mutation-test.ps1 -TestsOnly
.\build.cmd -RunTests                       # ~4 мин, плеер не собирается

# ночью
.\tools\mutation-test.ps1 -ThresholdBreak 66
.\build.cmd -RunPlayMode
```

### 8.1. Чем платится цикл разработки

Побочный итог работы: время уходило не в тесты, а в запуск Unity. Каждый
`Unity.exe -batchMode` платит фиксированные 60–90 с (лицензия, Asset Pipeline
Refresh ~12 с, три domain reload), а `build.cmd -RunTests` вдобавок собирал
плеер — ещё 7–8 минут к КАЖДОЙ проверке тестов.

Сборка отвязана от тестов, а все вызовы Unity сведены в один шлюз
`tools/unity.ps1`: открыт редактор — команда уходит в него по сокету
(`Assets/Editor/EditorBridge.cs`), закрыт — поднимается фоновый.

| Что | Было | Стало |
|---|---|---|
| Один тестовый класс | ~2 мин | **3 с** |
| Полный EditMode через мост (`-Live`) | 6–7 мин | 2,5 мин |
| Полный EditMode холодным batch | 6–7 мин | 2,5 мин (без сборки плеера) |
| Сборка WinDebug | ~3 мин | 37 с |
| `-RunTests -RunPlayMode` | ~20 мин | **6,5 мин** |

**Полный прогон и PlayMode намеренно идут холодным batch.** Живой редактор не
даёт полной верности: два полных прогона подряд в одной сессии разошлись на
`CameraControllerTests` (шаг SmoothDamp считается от `Time.deltaTime`) и
`UiAsciiSymbolsTests` (атлас шрифта переживает прогон), а PlayMode в нём виснет
на входе в play mode. Утечку `KitchenSettings` между прогонами мост чинит сам —
перечитывает ассет с диска, как это делал новый процесс.

Для ядра всё это неважно: 187 тестов под `dotnet` идут 0,14 с и Unity не
касаются вовсе. Чем больше кода живёт в ядре, тем короче цикл — это и есть
главный практический выигрыш от всей работы.

**Ограничение, которое надо учитывать при переносе в GitHub Actions.** Ядро
ИСПОЛНЯЕТСЯ без Unity, но при СБОРКЕ ему нужен `UnityEngine.CoreModule.dll`:
`Vector3`, `Mathf`, `Rect` лежат в нём. На чистом раннере без установленного
редактора `dotnet build` ядра не пройдёт. Варианты:

- гонять джобу в образе с редактором (`unityci/editor`) — лицензия для
  `dotnet build` не нужна, она нужна только самому Unity;
- либо довести ядро до `noEngineReferences` со своими типами вектора и
  математики — тогда никакой зависимости от Unity не остаётся вовсе.

До того как это решено, скрипт рассчитан на локальный запуск и принимает
`-UnityManagedDir` для нестандартного пути к редактору.

Проекты разведены по подпапкам (`geometry/core`, `geometry/tests`) не для
красоты: в одном каталоге они делили `obj/`, и сборка ядра (без единого
PackageReference) затирала `project.assets.json` тестов — следующий
`dotnet test` падал на «не найдено пространство имён NUnit», и `dotnet restore`
не помогал, только удаление `obj/`. Разделение попутно сняло требование флага
`--test-project`.

Порог: снимать baseline после каждого этапа и ставить `--threshold-break` на
5 пунктов ниже достигнутого.

---

## 9. Отвергнутые варианты

| Вариант | Почему отвергнут |
|---|---|
| Запуск тестов под `dotnet test` без выноса ядра | `SecurityException`: ECall запрещён CoreCLR для пользовательских сборок |
| Запуск под standalone `mono.exe` | `cant resolve internal call to UnityEngine.GameObject::Internal_CreateGameObject_Injected` — таблица internal calls регистрируется только внутри `Unity.exe` |
| Форк `codmw44/stryker-net` (ветка `feature/add_unity_support`) | 22 коммита, заброшен в 2023, не влит в upstream, требует адаптации к текущему Stryker. Его механики (IPC через `UnityListens.txt`, удержание Unity между мутантами, `AsmdefParser`) нужны только для мутирования кода ВНУТРИ Unity. После выноса ядра не нужны вовсе |
| Свой mutation harness поверх `Unity.exe -runTests` | Стоимость прогона: `SnapMutationTests` идёт 158–217 с. Сотни мутантов на `SnapSystem` = десятки часов на один модуль за один прогон. В CI не живёт. Быстрые юнит-тесты гонять дешевле, но именно на них мутанты и выживают |
| Переезд ядра на `Unity.Mathematics` | Не нужен: `Vector3`/`Mathf`/`Rect`/`Bounds` исполняются под CoreCLR (§3.2) |
| asmdef с `"noEngineReferences": true` | Запрещает `Vector3` и `Mathf` целиком, потребовал бы переписать всю математику. Достаточно запрета на подмножество API (§7, архитектурный тест) |

---

## 10. Риски

| Риск | Митигация |
|---|---|
| `FacadeElement`/`DrawerElement` считают геометрию от ЗАКРЫТОЙ позы (`ClosedPosition`), а не от трансформа | Снимок строит сам элемент — специфика остаётся в `Runtime`, ядро её не видит |
| Кто-то добавит `GetComponent` в ядро: Unity соберёт, `dotnet` упадёт в рантайме | Архитектурный тест на запрещённые символы (§7) |
| Расхождение `SnapSystem` и `ResizeSnap` — известная болезнь («растягивается, но не перетаскивается») | Обе живут в одном ядре и мутируются вместе; парный тест обязателен |
| Stryker мутирует константы `Tolerance` | Это ценно: именно на них выжили мутанты в спайке |
| Порядок граней сломается при переносе | Тест-контракт на этапе 1 (§7) |
