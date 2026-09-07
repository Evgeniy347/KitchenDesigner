using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KitchenDesigner.Core.UI
{
    public enum ConsoleLineKind
    {
        Log,
        Warning,
        Error,
        Status,
        StatusSuccess,
        StatusWarning,
        StatusError,
    }

    public sealed class ConsoleLine
    {
        public ConsoleLine(ConsoleLineKind kind, string text, DateTime at)
        {
            Kind = kind;
            Text = text;
            FirstAt = at;
            LastAt = at;
            Repeats = 1;
        }

        public ConsoleLineKind Kind { get; }
        public string Text { get; }
        public DateTime FirstAt { get; }
        public DateTime LastAt { get; private set; }
        public int Repeats { get; private set; }

        internal void RepeatedAt(DateTime at)
        {
            LastAt = at;
            Repeats++;
        }
    }

    public sealed class ConsoleLog
    {
        public const int Capacity = 400;

        public static ConsoleLog Shared { get; } = new ConsoleLog();

        private readonly List<ConsoleLine> _lines = new List<ConsoleLine>();

        public int Count => _lines.Count;

        public int Revision { get; private set; }

        public IReadOnlyList<ConsoleLine> Lines => _lines;

        public static bool IsStatus(ConsoleLineKind kind) =>
            kind == ConsoleLineKind.Status
            || kind == ConsoleLineKind.StatusSuccess
            || kind == ConsoleLineKind.StatusWarning
            || kind == ConsoleLineKind.StatusError;

        public void Clear()
        {
            _lines.Clear();
            Revision++;
        }

        public void Append(ConsoleLineKind kind, string text, DateTime at)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_lines.Count > 0)
            {
                var last = _lines[_lines.Count - 1];
                if (last.Kind == kind && string.Equals(last.Text, text, StringComparison.Ordinal))
                {
                    last.RepeatedAt(at);
                    Revision++;
                    return;
                }
            }

            _lines.Add(new ConsoleLine(kind, text, at));
            if (_lines.Count > Capacity) _lines.RemoveRange(0, _lines.Count - Capacity);
            Revision++;
        }

        public static string Prefix(ConsoleLineKind kind) => kind switch
        {
            ConsoleLineKind.Warning => "<w> ",
            ConsoleLineKind.Error => "<!> ",
            ConsoleLineKind.Status => "статус: ",
            ConsoleLineKind.StatusSuccess => "статус ok: ",
            ConsoleLineKind.StatusWarning => "статус, внимание: ",
            ConsoleLineKind.StatusError => "статус, ошибка: ",
            _ => "",
        };

        public static string Format(ConsoleLine line)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            sb.Append(line.LastAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            sb.Append("] ");
            sb.Append(Prefix(line.Kind));
            sb.Append(line.Text);
            if (line.Repeats > 1)
            {
                sb.Append(" ×");
                sb.Append(line.Repeats.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public string Tail(int lineCount)
        {
            if (lineCount <= 0) return string.Empty;
            int start = Math.Max(0, _lines.Count - lineCount);
            var sb = new StringBuilder();
            for (int i = start; i < _lines.Count; i++) sb.AppendLine(Format(_lines[i]));
            return sb.ToString();
        }
    }
}
