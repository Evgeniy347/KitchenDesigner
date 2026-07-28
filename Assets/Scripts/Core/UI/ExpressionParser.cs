using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Парсер простых арифметических выражений (+ - * /) в числовых полях UI.
    /// Поддерживает: 1234+56 → 1290, 800-100 → 700, 10*5 → 50, 20/4 → 5,
    /// -50+100 → 50, 10+20-5+3 → 28. Пробелы удаляются. При ошибке возвращает null.
    /// Деление на ноль → 0 (особый случай).
    /// </summary>
    public static class ExpressionParser
    {
        public static int? EvaluateInt(string text)
        {
            var tokens = Tokenize(text);
            if (tokens == null) return null;
            if (tokens.Count == 0) return null;

            int result = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                if (tok.isOp)
                {
                    if (i + 1 >= tokens.Count) return null;
                    var next = tokens[i + 1];
                    if (next.isOp) return null;
                    if (tok.ch == '+') result += next.value;
                    else if (tok.ch == '-') result -= next.value;
                    else if (tok.ch == '*') result *= next.value;
                    else if (next.value == 0) return 0; // деление на ноль
                    else result /= next.value;
                    i++;
                }
                else
                {
                    result = tok.value;
                }
            }
            return result;
        }

        public static float? EvaluateFloat(string text)
        {
            var tokens = TokenizeFloat(text);
            if (tokens == null) return null;
            if (tokens.Count == 0) return null;

            float result = 0f;
            for (int i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                if (tok.isOp)
                {
                    if (i + 1 >= tokens.Count) return null;
                    var next = tokens[i + 1];
                    if (next.isOp) return null;
                    if (tok.ch == '+') result += next.value;
                    else if (tok.ch == '-') result -= next.value;
                    else if (tok.ch == '*') result *= next.value;
                    else if (next.value == 0f) return 0f; // деление на ноль
                    else result /= next.value;
                    i++;
                }
                else
                {
                    result = tok.value;
                }
            }
            return result;
        }

        private struct IntToken
        {
            public bool isOp;
            public char ch;
            public int value;
        }

        private struct FloatToken
        {
            public bool isOp;
            public char ch;
            public float value;
        }

        /// <summary>Разбить строку на токены: числа и операторы +,-,*,/.</summary>
        private static List<IntToken>? Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var cleaned = text.Replace(" ", "");
            var tokens = new List<IntToken>();
            int i = 0;

            while (i < cleaned.Length)
            {
                char ch = cleaned[i];

                if (ch == '+' || ch == '-')
                {
                    // Leading operator или после другого оператора — унарный
                    if (tokens.Count == 0 || tokens[^1].isOp)
                    {
                        // Считаем частью следующего числа
                        int start = i;
                        i++;
                        while (i < cleaned.Length && char.IsDigit(cleaned[i])) i++;
                        if (i == start + 1) return null; // оператор без числа
                        var numStr = cleaned.Substring(start, i - start);
                        if (!int.TryParse(numStr, out int v)) return null;
                        tokens.Add(new IntToken { value = v });
                    }
                    else
                    {
                        tokens.Add(new IntToken { isOp = true, ch = ch });
                        i++;
                    }
                }
                else if (ch == '*' || ch == '/')
                {
                    if (tokens.Count == 0 || tokens[^1].isOp) return null;
                    tokens.Add(new IntToken { isOp = true, ch = ch });
                    i++;
                }
                else if (char.IsDigit(ch))
                {
                    int start = i;
                    while (i < cleaned.Length && char.IsDigit(cleaned[i])) i++;
                    if (!int.TryParse(cleaned.Substring(start, i - start), out int v)) return null;
                    tokens.Add(new IntToken { value = v });
                }
                else
                {
                    return null; // недопустимый символ
                }
            }

            return tokens;
        }

        private static List<FloatToken>? TokenizeFloat(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var cleaned = text.Replace(" ", "");
            var tokens = new List<FloatToken>();
            int i = 0;

            while (i < cleaned.Length)
            {
                char ch = cleaned[i];

                if (ch == '+' || ch == '-')
                {
                    if (tokens.Count == 0 || tokens[^1].isOp)
                    {
                        int start = i;
                        i++;
                        while (i < cleaned.Length && (char.IsDigit(cleaned[i]) || cleaned[i] == '.')) i++;
                        if (i == start + 1) return null;
                        var numStr = cleaned.Substring(start, i - start);
                        if (!float.TryParse(numStr,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float v)) return null;
                        tokens.Add(new FloatToken { value = v });
                    }
                    else
                    {
                        tokens.Add(new FloatToken { isOp = true, ch = ch });
                        i++;
                    }
                }
                else if (ch == '*' || ch == '/')
                {
                    if (tokens.Count == 0 || tokens[^1].isOp) return null;
                    tokens.Add(new FloatToken { isOp = true, ch = ch });
                    i++;
                }
                else if (char.IsDigit(ch) || ch == '.')
                {
                    int start = i;
                    while (i < cleaned.Length && (char.IsDigit(cleaned[i]) || cleaned[i] == '.')) i++;
                    if (!float.TryParse(cleaned.Substring(start, i - start),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float v)) return null;
                    tokens.Add(new FloatToken { value = v });
                }
                else
                {
                    return null;
                }
            }

            return tokens;
        }
    }
}
