using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class StrokePath
    {
        private const int ArcSegmentsPerQuarter = 8;
        private const int CircleSegments = 32;
        private const float DegenerateRadius = 1e-6f;
        private const float SamePointSquared = 1e-12f;
        private const double RadiusAlreadyFits = 1.0;
        private const double NoSweep = 0.0;

        public static List<List<Vector2>> Parse(string data)
        {
            var strokes = new List<List<Vector2>>();
            var reader = new Reader(data);
            var current = new Vector2();
            var start = new Vector2();
            List<Vector2>? stroke = null;
            char command = ' ';

            while (reader.SkipSeparators())
            {
                if (reader.AtCommand) command = reader.ReadCommand();
                else if (command == ' ') throw new FormatException("path data must start with a command: " + data);
                else if (command == 'M') command = 'L';
                else if (command == 'm') command = 'l';

                bool relative = char.IsLower(command);
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        current = Target(reader.ReadPoint(), current, relative);
                        start = current;
                        stroke = new List<Vector2> { current };
                        strokes.Add(stroke);
                        break;
                    case 'L':
                        current = Target(reader.ReadPoint(), current, relative);
                        Extend(ref stroke, strokes, current);
                        break;
                    case 'H':
                        current = new Vector2(relative ? current.x + reader.ReadNumber() : reader.ReadNumber(), current.y);
                        Extend(ref stroke, strokes, current);
                        break;
                    case 'V':
                        current = new Vector2(current.x, relative ? current.y + reader.ReadNumber() : reader.ReadNumber());
                        Extend(ref stroke, strokes, current);
                        break;
                    case 'A':
                        current = ReadArc(reader, current, relative, ref stroke, strokes);
                        break;
                    case 'O':
                        AddCircle(strokes, Target(reader.ReadPoint(), current, relative), reader.ReadNumber());
                        break;
                    case 'Z':
                        current = start;
                        Extend(ref stroke, strokes, current);
                        break;
                    default:
                        throw new FormatException("unsupported path command '" + command + "': " + data);
                }
            }

            return strokes;
        }

        private static Vector2 Target(Vector2 point, Vector2 current, bool relative) =>
            relative ? current + point : point;

        private static void Extend(ref List<Vector2>? stroke, List<List<Vector2>> strokes, Vector2 point)
        {
            if (stroke == null)
            {
                stroke = new List<Vector2>();
                strokes.Add(stroke);
            }
            stroke.Add(point);
        }

        private static Vector2 ReadArc(Reader reader, Vector2 from, bool relative,
            ref List<Vector2>? stroke, List<List<Vector2>> strokes)
        {
            float rx = Math.Abs(reader.ReadNumber());
            float ry = Math.Abs(reader.ReadNumber());
            float rotation = reader.ReadNumber();
            bool large = reader.ReadFlag();
            bool sweep = reader.ReadFlag();
            var to = Target(reader.ReadPoint(), from, relative);

            foreach (var point in FlattenArc(from, to, rx, ry, rotation, large, sweep))
                Extend(ref stroke, strokes, point);
            return to;
        }

        private static IEnumerable<Vector2> FlattenArc(Vector2 from, Vector2 to, float rx, float ry,
            float rotationDegrees, bool large, bool sweep)
        {
            if (rx < DegenerateRadius || ry < DegenerateRadius || (from - to).sqrMagnitude < SamePointSquared)
            {
                yield return to;
                yield break;
            }

            double phi = rotationDegrees * Math.PI / 180.0;
            double cosPhi = Math.Cos(phi), sinPhi = Math.Sin(phi);
            double dx = (from.x - to.x) / 2.0, dy = (from.y - to.y) / 2.0;
            double x1 = cosPhi * dx + sinPhi * dy;
            double y1 = -sinPhi * dx + cosPhi * dy;

            double radiusX = rx, radiusY = ry;
            double lambda = x1 * x1 / (radiusX * radiusX) + y1 * y1 / (radiusY * radiusY);
            if (lambda > RadiusAlreadyFits)
            {
                double scale = Math.Sqrt(lambda);
                radiusX *= scale;
                radiusY *= scale;
            }

            double numerator = radiusX * radiusX * radiusY * radiusY
                - radiusX * radiusX * y1 * y1 - radiusY * radiusY * x1 * x1;
            double denominator = radiusX * radiusX * y1 * y1 + radiusY * radiusY * x1 * x1;
            double coefficient = Math.Sqrt(Math.Max(0.0, numerator / denominator)) * (large == sweep ? -1.0 : 1.0);
            double cxPrime = coefficient * radiusX * y1 / radiusY;
            double cyPrime = -coefficient * radiusY * x1 / radiusX;
            double cx = cosPhi * cxPrime - sinPhi * cyPrime + (from.x + to.x) / 2.0;
            double cy = sinPhi * cxPrime + cosPhi * cyPrime + (from.y + to.y) / 2.0;

            double startAngle = Math.Atan2((y1 - cyPrime) / radiusY, (x1 - cxPrime) / radiusX);
            double endAngle = Math.Atan2((-y1 - cyPrime) / radiusY, (-x1 - cxPrime) / radiusX);
            double sweepAngle = endAngle - startAngle;
            if (sweep && sweepAngle < NoSweep) sweepAngle += 2 * Math.PI;
            else if (!sweep && sweepAngle > NoSweep) sweepAngle -= 2 * Math.PI;

            int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweepAngle) / (Math.PI / 2) * ArcSegmentsPerQuarter));
            for (int i = 1; i < steps; i++)
            {
                double angle = startAngle + sweepAngle * i / steps;
                double px = radiusX * Math.Cos(angle), py = radiusY * Math.Sin(angle);
                yield return new Vector2((float)(cosPhi * px - sinPhi * py + cx), (float)(sinPhi * px + cosPhi * py + cy));
            }
            yield return to;
        }

        private static void AddCircle(List<List<Vector2>> strokes, Vector2 center, float radius)
        {
            var ring = new List<Vector2>(CircleSegments + 1);
            for (int i = 0; i <= CircleSegments; i++)
            {
                double angle = 2 * Math.PI * i / CircleSegments;
                ring.Add(new Vector2(center.x + radius * (float)Math.Cos(angle), center.y + radius * (float)Math.Sin(angle)));
            }
            strokes.Add(ring);
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _index;

            public Reader(string text) => _text = text;

            public bool AtCommand => _index < _text.Length && char.IsLetter(_text[_index]);

            public bool SkipSeparators()
            {
                while (_index < _text.Length && (char.IsWhiteSpace(_text[_index]) || _text[_index] == ',')) _index++;
                return _index < _text.Length;
            }

            public char ReadCommand() => _text[_index++];

            public Vector2 ReadPoint() => new Vector2(ReadNumber(), ReadNumber());

            public bool ReadFlag()
            {
                SkipSeparators();
                char c = _index < _text.Length ? _text[_index] : '\0';
                if (c != '0' && c != '1') throw new FormatException("arc flag must be 0 or 1 at " + _index + ": " + _text);
                _index++;
                return c == '1';
            }

            public float ReadNumber()
            {
                SkipSeparators();
                int begin = _index;
                if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+')) _index++;
                while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                if (_index < _text.Length && _text[_index] == '.')
                {
                    _index++;
                    while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                }
                if (_index == begin) throw new FormatException("number expected at " + _index + ": " + _text);
                return float.Parse(_text.Substring(begin, _index - begin), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
