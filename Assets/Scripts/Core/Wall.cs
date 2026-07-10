using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Маркер объекта-стены. Стена движется/выделяется как доска, но в графе
    /// связности — структурный якорь (как пол): исключена из спецификации и подсветки.
    /// Умеет «опускаться» до 100 мм (режим обзора) без потери исходной высоты.</summary>
    public class Wall : MonoBehaviour
    {
        private bool _lowered;
        private float _fullScaleY;
        private float _fullPosY;

        /// <summary>Опустить/поднять стену. loweredHeightUnits — высота в юнитах (м).</summary>
        public void SetLowered(bool lower, float loweredHeightUnits)
        {
            if (lower)
            {
                if (!_lowered)
                {
                    _fullScaleY = transform.localScale.y;
                    _fullPosY = transform.position.y;
                    _lowered = true;
                }
                ApplyLowered(loweredHeightUnits);
            }
            else if (_lowered)
            {
                var sc = transform.localScale; sc.y = _fullScaleY; transform.localScale = sc;
                var p = transform.position; p.y = _fullPosY; transform.position = p;
                _lowered = false;
            }
        }

        public void RestoreFull() => SetLowered(false, 0f);

        /// <summary>Опущена ли стена сейчас (режим обзора).</summary>
        public bool IsLowered => _lowered;

        /// <summary>Высота стены в юнитах при полной высоте (без учёта опускания).</summary>
        public float FullScaleY => _lowered ? _fullScaleY : transform.localScale.y;

        /// <summary>Позиция центра при полной высоте. Пока стена опущена, её
        /// transform смещён вниз — для сохранения нужна именно полная позиция,
        /// иначе после загрузки стена «утонет» (станет ниже).</summary>
        public Vector3 FullPosition
        {
            get
            {
                var p = transform.position;
                if (_lowered) p.y = _fullPosY;
                return p;
            }
        }

        private void ApplyLowered(float loweredHeightUnits)
        {
            float baseY = _fullPosY - _fullScaleY * 0.5f; // низ стены остаётся на месте
            var sc = transform.localScale; sc.y = loweredHeightUnits; transform.localScale = sc;
            var p = transform.position; p.y = baseY + loweredHeightUnits * 0.5f; transform.position = p;
        }
    }
}
