using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Снимок геометрии детали — всё, что нужно прилипанию, ресайзу и
    /// валидации, и ничего больше. Заменяет собой ссылку на KitchenElement в
    /// чистых функциях ядра.
    ///
    /// Зачем: KitchenElement — это MonoBehaviour, и чтобы ПРОЧИТАТЬ его грани в
    /// гипотетической позиции, приходилось ПИСАТЬ в transform.position и потом
    /// возвращать всё назад. Снимок строится сразу для нужной позиции, поэтому
    /// сцена не трогается вовсе, а само ядро исполняется без Unity — под
    /// dotnet test и мутационным тестированием.
    ///
    /// Снимок неизменяем и живёт ровно столько, сколько длится расчёт: деталь
    /// после него может двигаться, снимок об этом не узнает и не должен.</summary>
    public readonly struct ElementGeometry
    {
        /// <summary>Имя детали. Снэп возвращает его в результате — по нему
        /// вызывающий код находит цель у себя.</summary>
        public readonly string Name;

        /// <summary>Шесть габаритных граней. Порядок — контракт: index/2 = ось
        /// (0=X, 1=Y, 2=Z), чётный индекс = положительное направление.</summary>
        public readonly Face[] Faces;

        /// <summary>Дно каждого паза как обычная грань: вкладная панель садится
        /// на него номиналом.</summary>
        public readonly Face[] GrooveSeatFaces;

        /// <summary>Стенки пазов — разметочные плоскости для выравнивания кромки
        /// любой детали, а не поверхности контакта.</summary>
        public readonly Face[] GrooveWallFaces;

        /// <summary>Мировой AABB. У повёрнутой детали он ШИРЕ тела, поэтому
        /// годится только для консервативных отсечек, не для проверки контакта.</summary>
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        /// <summary>Деталь — вкладная панель. Только ей предлагается дно паза:
        /// толстая деталь в паз не садится, и дно давало бы ложное притяжение
        /// внутрь короба.</summary>
        public readonly bool IsPanel;

        public ElementGeometry(string name, Face[] faces, Face[] grooveSeatFaces,
            Face[] grooveWallFaces, Vector3 min, Vector3 max, bool isPanel)
        {
            Name = name;
            Faces = faces;
            GrooveSeatFaces = grooveSeatFaces;
            GrooveWallFaces = grooveWallFaces;
            Min = min;
            Max = max;
            IsPanel = isPanel;
        }

        /// <summary>Снимок пуст (не заполнен) — так выглядит default(ElementGeometry).</summary>
        public bool IsEmpty => Faces == null || Faces.Length == 0;

        /// <summary>AABB по набору вершин — ровно то, что раньше считали
        /// вызывающие вручную после GetVertices.</summary>
        public static void BoundsOf(Vector3[] vertices, out Vector3 min, out Vector3 max)
        {
            min = vertices[0];
            max = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }
        }

        /// <summary>Осевая коробка из позы и габаритов. Нужна и ядру (тесты
        /// строят сцену без Unity), и как эталон порядка граней.</summary>
        public static ElementGeometry Box(string name, Vector3 center, Vector3 sizeUnits,
            bool isPanel = false)
        {
            var half = sizeUnits * 0.5f;
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            var faceDims = new[]
            {
                new Vector2(sizeUnits.y, sizeUnits.z),
                new Vector2(sizeUnits.x, sizeUnits.z),
                new Vector2(sizeUnits.x, sizeUnits.y),
            };
            var offsets = new[]
            {
                axes[0] * half.x, -axes[0] * half.x,
                axes[1] * half.y, -axes[1] * half.y,
                axes[2] * half.z, -axes[2] * half.z,
            };
            var normals = new[] { axes[0], -axes[0], axes[1], -axes[1], axes[2], -axes[2] };
            var rightAxis = new[] { axes[1], axes[1], axes[0], axes[0], axes[0], axes[0] };
            var upAxis = new[] { axes[2], axes[2], axes[2], axes[2], axes[1], axes[1] };

            var faces = new Face[6];
            for (int i = 0; i < 6; i++)
                faces[i] = new Face(center + offsets[i], normals[i], faceDims[i / 2],
                    rightAxis[i], upAxis[i]);

            var empty = System.Array.Empty<Face>();
            return new ElementGeometry(name, faces, empty, empty,
                center - half, center + half, isPanel);
        }
    }
}
