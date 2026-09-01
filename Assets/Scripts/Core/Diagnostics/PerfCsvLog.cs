#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class PerfCsvLog
    {
        public const char ColumnSeparator = ';';
        public const string ValueFormat = "0.####";

        private const int MaxDirectoriesUpToRepoRoot = 6;

        private readonly string[] _header;
        private readonly int _columns;
        private readonly float[] _preallocatedData;
        private readonly int _capacity;
        private int _rows;

        public bool Recording { get; private set; }
        public int Rows => _rows;
        public bool Full => _rows >= _capacity;

        public PerfCsvLog(string[] header, int capacityFrames)
        {
            _header = header;
            _columns = header.Length;
            _capacity = Mathf.Max(1, capacityFrames);
            _preallocatedData = new float[_capacity * _columns];
        }

        public void Start()
        {
            _rows = 0;
            Recording = true;
        }

        public void Append(float[] values)
        {
            if (!Recording || values.Length != _columns) return;
            if (_rows >= _capacity)
            {
                Recording = false;
                return;
            }

            Array.Copy(values, 0, _preallocatedData, _rows * _columns, _columns);
            _rows++;
        }

        public string? Stop()
        {
            Recording = false;
            if (_rows == 0) return null;

            var dir = Path.Combine(RepoRootFoundByWalkingUpToBuildCmd(), "test-results", "perf");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"perf_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            File.WriteAllText(path, BuildCsv(), Encoding.UTF8);
            return path;
        }

        internal string BuildCsv()
        {
            var sb = new StringBuilder(_rows * _columns * 8);
            for (int c = 0; c < _columns; c++)
            {
                if (c > 0) sb.Append(ColumnSeparator);
                sb.Append(_header[c]);
            }
            sb.Append('\n');

            for (int r = 0; r < _rows; r++)
            {
                int off = r * _columns;
                for (int c = 0; c < _columns; c++)
                {
                    if (c > 0) sb.Append(ColumnSeparator);
                    sb.Append(FormatCellInvariantly(_preallocatedData[off + c]));
                }
                sb.Append('\n');
            }

            return sb.ToString();
        }

        internal static string FormatCellInvariantly(float value) =>
            value.ToString(ValueFormat, CultureInfo.InvariantCulture);

        private static string RepoRootFoundByWalkingUpToBuildCmd()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < MaxDirectoriesUpToRepoRoot && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "build.cmd"))) return dir.FullName;
                dir = dir.Parent;
            }
            return Application.persistentDataPath;
        }
    }
}

#endif
