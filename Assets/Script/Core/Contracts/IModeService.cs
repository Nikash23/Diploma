using System;

namespace XRInterior.Contracts
{
    /// <summary>
    /// Режим отображения (VR / MR). Владелец: разработчик A.
    /// Переключение НЕ пересоздаёт объекты проекта.
    /// ЗАГЛУШКА: сигнатуры согласуются с B и C, менять только по общему решению.
    /// </summary>
    public interface IModeService
    {
        AppMode CurrentMode { get; }

        /// <summary>Включить VR или MR (passthrough вкл/выкл, окружение, свет).</summary>
        void SetMode(AppMode mode);

        /// <summary>Открыть проект в другом режиме (VR→MR или MR→VR).</summary>
        void TransferProject(string projectId, AppMode targetMode);

        /// <summary>Режим сменился. Аргумент: новый режим.</summary>
        event Action<AppMode> ModeChanged;
    }
}