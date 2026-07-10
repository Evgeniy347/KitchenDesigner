<p align="center">
  <h1 align="center">Kitchen Designer</h1>
  <p align="center">
    3D-конструктор мебельных щитов<br>
    Unity 6000 • C# • URP
  </p>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
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

---

## Лицензия

MIT License. See [LICENSE](LICENSE) for details.
