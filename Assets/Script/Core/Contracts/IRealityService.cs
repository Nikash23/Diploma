using System.Collections.Generic;
using UnityEngine;

namespace XRInterior.Contracts
{
    /// <summary>
    /// Реальное окружение (данные скана). Владелец: разработчик A.
    /// Используется B для коллизий (FR-105) и запрета размещения (FR-207).
    /// ЗАГЛУШКА: сигнатуры согласуются с B и C.
    /// </summary>
    public interface IRealityService
    {
        /// <summary>Плоскости помещения: пол, потолок, стены, двери, окна.</summary>
        IReadOnlyList<RealPlane> GetPlanes();

        /// <summary>Крупные объёмы (столы, шкафы).</summary>
        IReadOnlyList<RealVolume> GetVolumes();

        /// <summary>true, если bounds пересекается с реальной стеной или объёмом (ставить нельзя).</summary>
        bool IsBlocked(Bounds bounds);

        TrackingState GetTrackingState();
    }
}
