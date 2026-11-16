using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRInterior.Contracts
{
    /// <summary>
    /// Сканирование и сравнение проекта с реальностью. Владелец: разработчик A.
    /// Accept/Ignore не меняют проект напрямую: принятие проходит через ICommandService (B).
    /// ЗАГЛУШКА: сигнатуры согласуются с B и C.
    /// </summary>
    public interface IComparisonService
    {
        /// <summary>Запустить сканирование (MRUK / Space Setup при необходимости).</summary>
        Task<bool> ScanAsync();

        /// <summary>Сохранить снимок пространства в проект.</summary>
        void SaveSnapshot();

        /// <summary>Сопоставить проект с реальностью (допуски из настроек: 0,10 м и 5°).</summary>
        void Compare();

        IReadOnlyList<ComparisonItem> GetResults();

        /// <summary>Принять расхождение (изменение вносится командой редактора).</summary>
        void Accept(string elementId);

        /// <summary>Отклонить расхождение (статус Ignored).</summary>
        void Ignore(string elementId);

        event Action SnapshotSaved;
        event Action<IReadOnlyList<ComparisonItem>> ComparisonCompleted;
    }
}
