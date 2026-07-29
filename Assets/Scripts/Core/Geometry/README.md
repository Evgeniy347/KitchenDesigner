# KitchenDesigner.Geometry — ядро без сцены

Чистая геометрия: допуски, грани, прилипание, ресайз, пазы. Собирается ДВАЖДЫ
из этих же исходников — Unity по `KitchenDesigner.Geometry.asmdef`, а `dotnet`
по `geometry/Geometry.csproj` (glob на эту папку). Дублирования нет.

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
cd geometry
dotnet test Geometry.Tests.csproj
dotnet-stryker --project Geometry.csproj --test-project Geometry.Tests.csproj --reporter html
```

`--test-project` обязателен: в каталоге два .csproj, и без флага Stryker не
выбирает, какой из них мутировать.

## Что разрешено

`Vector3`, `Vector2`, `Vector3Int`, `Rect`, `Bounds`, весь `Mathf`, а также
`Quaternion` как ДАННЫЕ: `Quaternion.identity`, умножение на вектор и
`Quaternion.Angle` исполняются под CoreCLR. Нельзя только КОНСТРУИРОВАТЬ
повороты — их строит вызывающий код на стороне `KitchenDesigner.Runtime`.
