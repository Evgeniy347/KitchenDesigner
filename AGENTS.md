## Commit rules
- **Language**: English, format: `<type>: <short description>`
- **Types**: `feat`, `fix`, `chore`, `test`, `refactor`
- No junk files committed, one logical change per commit

## Code quality
- Read file first, copy style. Reuse existing helpers.
- Named constants, no magic numbers. Unity positions in METERS, board sizes in MILLIMETERS.
- One method one job (~40 lines max). No copy-paste.
- Validate inputs, clear error messages. Full word names. No dead code.
- Every change: add/update EditMode test, run suite, keep behaviour/layout in separate commits.

## Persistence (save/load) — safe model
Сохранение проходит один путь: `ElementData.FromElement` (запись) ⇄ `SaveLoadManagerInstance.RestoreScene` (чтение). Любое новое свойство обязано пройти обе стороны, иначе оно «теряется» или «уезжает» после перезагрузки.

**Чек-лист при добавлении сохраняемого свойства (делать все пункты в одном коммите):**
1. Поле в `ElementData` с безопасным значением по умолчанию (совместимость со старым JSON — недостающее поле не должно ломать загрузку).
2. Запись в `ElementData.FromElement`.
3. Восстановление в `RestoreScene`.
4. EditMode-тест round-trip: `FromElement` → `Deserialize(Serialize(...))` → значение сохранилось.

**Золотое правило: сериализуй ИСТОЧНИК ИСТИНЫ, а не производную/анимированную позу.**
`transform.position`/`rotation` могут быть временно смещены (открытая дверца отведена от петли, опущенная стена сдвинута вниз). Пиши логическое состояние, из которого поза вычисляется:
- дверца → `FacadeElement.ClosedPosition`/`ClosedRotation` (закрытая поза) + флаг `doorOpen`;
- стена → `Wall.FullPosition`.
Если сохранить текущий (смещённый) трансформ, при загрузке смещение применится повторно и объект уедет.

## MCP
### Board conventions
- Naming: `{Module}_{Side}`. `dimZ` = board thickness. Frame depth = module − 18mm.
- Side panels: 720mm height. Facade height = 716mm. No back panels.

### Workflow
1. Snapshot → 2. Simulate → 3. Apply → 4. Verify (zero violations except FacadeElement with gapMM)

### Connectivity
- BFS from anchors through face-to-face contacts. Intersections = violations.
- FacadeElement with gapMM > 0 exempt from connectivity check.
 