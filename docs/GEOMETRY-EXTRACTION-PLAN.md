# Вынос геометрического ядра из Unity и мутационное тестирование

**Дата:** 13.08.2026
**Unity:** 6000.4.3f1
**Статус:** проверено спайком, план к исполнению

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
| 3 | Граница снимка: `ElementGeometry` + `KitchenElement.ToGeometry()`; `ResizeSnap`/`ResizeMath` на снимки, старые сигнатуры — адаптеры | 8–12 | Ресайз-математика тестируется без сцены |
| 4 | **Ядро снэпа**: `Collect`, `TryPickCandidate`, `FacesOverlap`, `GetFaceRect`, `BestEdgeDelta` → на снимки; убирается запись в `transform`; `TrySnap`/`Diagnose` — адаптеры | 16–24 | Снэп чист; здесь же выигрыш по времени |
| 5 | Перенос быстрых юнит-тестов снэпа в `Assets/Tests/EditMode/Geometry/` | 8–10 | Реальный набор гоняется под `dotnet test` |
| 6 | Stryker в CI: baseline score, `--threshold-break`, ночной прогон, отчёт в артефакты | 4–6 | Мутационный порог как gate |
| 7 | *(опционально)* `ConstraintValidator`: развязать `GetComponent<>`-проверки типа через флаги в снимке | 12–16 | Валидация тоже мутируется |

**Этапы 1–6: 48–68 ч.**

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

```
push:
  cd geometry/tests && dotnet test                               # секунды
  build.cmd -RunTests                                            # ~6 мин
nightly:
  cd geometry/tests && dotnet-stryker --project Geometry.csproj \
                 --threshold-break <baseline-5> --reporter html --reporter json
  build.cmd -RunPlayMode
```

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
