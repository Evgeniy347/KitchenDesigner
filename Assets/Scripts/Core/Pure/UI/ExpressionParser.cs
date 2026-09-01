using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Core.UI
{
    public static class ExpressionParser
    {
        public static bool IsValidDimensionChar(char ch, bool allowDecimal = false)
        {
            return char.IsDigit(ch) || ch == '+' || ch == '-' || ch == '*' || ch == '/' || ch == ' '
                || (allowDecimal && ch == '.');
        }

        public static int? EvaluateInt(string text)
        {
            var tokens = Tokenize(text);
            if (tokens == null) return null;
            if (tokens.Count == 0) return null;

            int result = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                if (!tok.isOp)
                {
                    result = tok.value;
                    continue;
                }

                if (i + 1 >= tokens.Count) return null;
                var next = tokens[i + 1];
                if (next.isOp) return null;
                if (tok.ch == '+') result += next.value;
                else if (tok.ch == '-') result -= next.value;
                else if (tok.ch == '*') result *= next.value;
                else if (next.value == 0) return DivisionByZeroResultInt;
                else result /= next.value;
                i++;
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
                if (!tok.isOp)
                {
                    result = tok.value;
                    continue;
                }

                if (i + 1 >= tokens.Count) return null;
                var next = tokens[i + 1];
                if (next.isOp) return null;
                if (tok.ch == '+') result += next.value;
                else if (tok.ch == '-') result -= next.value;
                else if (tok.ch == '*') result *= next.value;
                else if (next.value == 0f) return DivisionByZeroResultFloat;
                else result /= next.value;
                i++;
            }
            return result;
        }

        public const int DivisionByZeroResultInt = 0;
        public const float DivisionByZeroResultFloat = 0f;

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

        private static bool SignBelongsToTheNextNumber(int tokensSoFar, bool previousWasOperator) =>
            tokensSoFar == 0 || previousWasOperator;

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
                    if (SignBelongsToTheNextNumber(tokens.Count, tokens.Count > 0 && tokens[^1].isOp))
                    {
                        int start = i;
                        i++;
                        while (i < cleaned.Length && char.IsDigit(cleaned[i])) i++;
                        if (i == start + 1) return null;
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
                    return null;
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
                    if (SignBelongsToTheNextNumber(tokens.Count, tokens.Count > 0 && tokens[^1].isOp))
                    {
                        int start = i;
                        i++;
                        while (i < cleaned.Length && IsDigitOrDot(cleaned[i])) i++;
                        if (i == start + 1) return null;
                        var numStr = cleaned.Substring(start, i - start);
                        if (!TryParseInvariant(numStr, out float v)) return null;
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
                else if (IsDigitOrDot(ch))
                {
                    int start = i;
                    while (i < cleaned.Length && IsDigitOrDot(cleaned[i])) i++;
                    if (!TryParseInvariant(cleaned.Substring(start, i - start), out float v)) return null;
                    tokens.Add(new FloatToken { value = v });
                }
                else
                {
                    return null;
                }
            }

            return tokens;
        }

        private static bool IsDigitOrDot(char ch) => char.IsDigit(ch) || ch == '.';

        private static bool TryParseInvariant(string s, out float value) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
