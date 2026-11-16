using System;
using UnityEngine;

namespace XRInterior.Contracts
{
    /// <summary>
    /// Привязка проекта к физическому пространству. Владелец: разработчик A.
    /// Калибровка хранится отдельно от геометрии проекта и может быть сброшена.
    /// ЗАГЛУШКА: сигнатуры согласуются с B и C.
    /// </summary>
    public interface IAlignmentService
    {
        /// <summary>
        /// Калибровка по опорному углу и направлению опорной стены.
        /// cornerPosition — где пользователь стоит в углу, wallDirection — куда смотрит вдоль опорной стены.
        /// </summary>
        AlignmentResult Calibrate(Vector3 cornerPosition, Vector3 wallDirection);

        /// <summary>Ручная коррекция: сдвиг (м) и поворот вокруг Y (градусы).</summary>
        void ApplyManualOffset(Vector3 offset, float rotationYDegrees);

        /// <summary>Сохранить текущую привязку как spatial anchor.</summary>
        void SaveAnchor();

        /// <summary>Сбросить привязку (проект не теряется).</summary>
        void Reset();

        /// <summary>Привязка изменилась.</summary>
        event Action<AlignmentResult> AlignmentChanged;
    }
}
