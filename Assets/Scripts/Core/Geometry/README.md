# KitchenDesigner.Geometry — ядро без сцены

Чистая геометрия: допуски, грани, константы, прилипание, ресайз, пазы.
Собирается ДВАЖДЫ из этих же исходников — Unity по
`KitchenDesigner.Geometry.asmdef`, а `dotnet` по `geometry/core/Geometry.csproj`
(glob на эту папку). Дублирования нет.

Смысл второй сборки — мутационное тестирование штатным `dotnet-stryker` и
прогон тестов ядра за миллисекунды вместо минут. Полный план и обоснование:
`docs/GEOMETRY-EXTRACTION-PLAN.md`.

## Что здесь запрещено

Код обязан исполняться под CoreCLR, где недоступны вызовы в нативный движок
(`[MethodImpl(MethodImplOptions.InternalCall)]`). Запрет проверяется тестом
`GeometryArchitectureTests` — он читает исходники этой папки и падает на
любом из символов:

| Запрещено | Причина |
|---|---|
| `MonoBehaviour`, `GameObject`, `Component` | сцена |
| `GetComponent`, `transform.`, `Instantiate`, `Destroy` | сцена |
| `Debug.` | логгер движка; ядро возвращает данные, а не пишет в консоль |
| `Quaternion.Euler`, `.AngleAxis`, `.LookRotation`, `.Inverse` | ECall |
| `Matrix4x4` | ECall |

## Как запускать

```
cd geometry/tests
dotnet test
dotnet-stryker --project Geometry.csproj --reporter html
```

Проекты РАЗВЕДЕНЫ по подпапкам (`geometry/core`, `geometry/tests`) намеренно.
Пока оба .csproj лежали в одном каталоге, они делили `obj/`, и сборка ядра
(в котором нет ни одного PackageReference) затирала `project.assets.json`
тестов — следующий `dotnet test` падал с «не найдено пространство имён NUnit».
`dotnet restore` это не чинило, помогало только удаление `obj/`. Заодно ушло
требование флага `--test-project`: Stryker отказывался работать, когда в
каталоге больше одного проекта.

Тесты ядра (`Assets/Tests/EditMode/Geometry/`) живут по тем же правилам: они
компилируются в обе сборки. Поворот в тесте задавайте литералом
(`new Quaternion(0, 0.70710678f, 0, 0.70710678f)` — это 90° вокруг Y), а не
через `Quaternion.Euler`, иначе `dotnet test` упадёт с `SecurityException`.

## Что разрешено

`Vector3`, `Vector2`, `Vector3Int`, `Rect`, `Bounds`, весь `Mathf`, а также
`Quaternion` как ДАННЫЕ: `Quaternion.identity`, умножение на вектор и
`Quaternion.Angle` исполняются под CoreCLR. Нельзя только КОНСТРУИРОВАТЬ
повороты — их строит вызывающий код на стороне `KitchenDesigner.Runtime`.
