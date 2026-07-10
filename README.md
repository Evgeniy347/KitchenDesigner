<p align="center">
  <img src="https://i.postimg.cc/5NXzYL2m/opencode-game-logo.png" width="200" alt="OpenCode Game Studios">
  <h1 align="center">Kitchen Designer</h1>
  <p align="center">
    3D-конструктор мебельных щитов<br>
    Unity 6000 • C# • URP
  </p>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
  <a href=".opencode/agents"><img src="https://img.shields.io/badge/agents-38-blueviolet" alt="38 Agents"></a>
  <a href=".opencode/skills"><img src="https://img.shields.io/badge/skills-37-green" alt="37 Skills"></a>
</p>

---

## Что это

Визуальный 3D-конструктор для раскладки мебельных щитов (досок):

- **Создание** досок произвольных размеров или из пресетов
- **Перемещение** по плоскости с привязкой к сетке 16 мм
- **Прилегание** плоскость-к-плоскости (face-to-face snapping)
- **Контроль** цельности конструкции — все доски должны быть скреплены
- **Спецификация** — таблица всех деталей с размерами и площадью

---

## Быстрый старт

```bash
# Открыть проект в Unity Editor
# Открыть Assets/Scenes/TestScene.unity
# Нажать Play
```

### Скрипты

| Скрипт | Назначение |
|--------|-----------|
| `clean.ps1` | Очистка проекта от временных файлов |
| `build.ps1` | Сборка .exe + прогон тестов |

---

## Стек

| Компонент | Версия |
|-----------|--------|
| Unity | 6000.4.3f1 |
| Render Pipeline | URP 17.4.0 |
| Язык | C# |
| Тесты | Unity Test Framework (NUnit) |

## Агенты

Проект использует 38 специализированных Opencode-агентов:

**Директоры**: `creative-director`, `technical-director`, `producer`

**Unity-специалисты**: `unity-specialist`, `unity-ui-specialist`, `unity-shader-specialist`

**Программисты**: `gameplay-programmer`, `lead-programmer`, `ui-programmer`, `tools-programmer`

**Дизайнеры**: `game-designer`, `systems-designer`, `ux-designer`, `level-designer`

**QA**: `qa-lead`, `qa-tester`

Полный список: `.opencode/agents/`

---

## Лицензия

MIT License. See [LICENSE](LICENSE) for details.
