#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Покадровый буфер метрик с выгрузкой в CSV. Буфер преаллоцирован, а запись
    /// на диск происходит один раз при остановке — иначе файловый I/O попал бы в те самые
    /// кадры, которые мы измеряем.</summary>
    public sealed class PerfCsvLog
    {
        private readonly string[] _header;
        private readonly int _columns;
        private readonly float[] _data;
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
            _data = new float[_capacity * _columns];
        }

        public void Start()
        {
            _rows = 0;
            Recording = true;
        }

        /// <summary>Добавляет строку. Значений должно быть ровно столько же, сколько колонок
        /// в заголовке. Переполнение буфера останавливает запись, а не затирает начало:
        /// интересен обычно первый прогон целиком.</summary>
        public void Append(float[] values)
        {
            if (!Recording || values.Length != _columns) return;
            if (_rows >= _capacity)
            {
                Recording = false;
                return;
            }

            Array.Copy(values, 0, _data, _rows * _columns, _columns);
            _rows++;
        }

        /// <summary>Останавливает запись и сохраняет файл. Возвращает путь либо null,
        /// если писать было нечего.</summary>
        public string? Stop()
        {
            Recording = false;
            if (_rows == 0) return null;

            var dir = Path.Combine(RepoRoot(), "test-results", "perf");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"perf_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder(_rows * _columns * 8);
            for (int c = 0; c < _columns; c++)
            {
                if (c > 0) sb.Append(';');
                sb.Append(_header[c]);
            }
            sb.Append('\n');

            for (int r = 0; r < _rows; r++)
            {
                int off = r * _columns;
                for (int c = 0; c < _columns; c++)
                {
                    if (c > 0) sb.Append(';');
                    // Инвариантная культура: под русской локалью иначе получится запятая
                    // и в разделителе, и в дробной части.
                    sb.Append(_data[off + c].ToString("0.####", CultureInfo.InvariantCulture));
                }
                sb.Append('\n');
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        /// <summary>Корень репозитория (kd-repose) — ищем вверх по дереву папку с build.cmd.
        /// В редакторе dataPath это Assets/, в сборке — KitchenDesigner_Data/, поэтому
        /// фиксированное число «..» не годится.</summary>
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 6 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "build.cmd"))) return dir.FullName;
                dir = dir.Parent;
            }
            return Application.persistentDataPath;
        }
    }
}

#endif
