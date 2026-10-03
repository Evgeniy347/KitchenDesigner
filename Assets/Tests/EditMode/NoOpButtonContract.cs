using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.UI;

/// <summary>Контракт «кнопка либо выключается, когда её нажатие пустое, либо названа всегда
/// исполнимой» (UI-GUIDELINES §9) в виде таблицы правил по имени узла. Один и тот же разбор
/// гоняют стражи тулбара и окон, чтобы правило читалось одинаково везде. Имя сопоставляется
/// регулярным выражением целиком: у строчных кнопок имя несёт номер строки.</summary>
internal sealed class NoOpButtonContract
{
    private readonly List<(Regex name, Func<Button, bool>? noOp, string? reason)> _rules =
        new List<(Regex, Func<Button, bool>?, string?)>();

    public NoOpButtonContract Always(string namePattern, string reason)
    {
        _rules.Add((Anchored(namePattern), null, reason));
        return this;
    }

    public NoOpButtonContract NoOpWhen(string namePattern, Func<Button, bool> isNoOp)
    {
        _rules.Add((Anchored(namePattern), isNoOp, null));
        return this;
    }

    private static Regex Anchored(string pattern) => new Regex("^(?:" + pattern + ")$");

    public List<string> Violations(IEnumerable<Button> buttons, string state)
    {
        var found = new List<string>();
        foreach (var button in buttons)
        {
            if (button == null || !button.gameObject.activeInHierarchy) continue;
            var rule = Match(button.name);
            if (rule == null)
            {
                found.Add($"{state}: «{button.name}» не названа ни «бывает пустой», ни «всегда исполнима»");
                continue;
            }

            if (rule.Value.noOp == null)
            {
                if (!button.interactable) found.Add($"{state}: «{button.name}» названа всегда исполнимой, а выключена");
                continue;
            }

            bool expected = !rule.Value.noOp(button);
            if (button.interactable != expected)
                found.Add($"{state}: «{button.name}» interactable={button.interactable}, ожидалось {expected}");
        }
        return found;
    }

    private (Regex name, Func<Button, bool>? noOp, string? reason)? Match(string name)
    {
        foreach (var rule in _rules)
            if (rule.name.IsMatch(name)) return rule;
        return null;
    }
}
