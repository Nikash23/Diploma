// Контракт: жизненный цикл проекта (ТЗ, раздел 11).
// Владелец: роль B. Менять только по согласованному запросу (T-02).
//
// Правило архитектуры: другие модули (XR, AR, UI) НЕ меняют содержимое проекта напрямую.
// Любое изменение идёт через ICommandService.

using System;
using System.Collections.Generic;

namespace XrInterior.ProjectData
{
    /// <summary>Краткие сведения о проекте для списка проектов (экран C-07).</summary>
    public class ProjectInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ProjectSource Source { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Проект с таким id не найден в хранилище.</summary>
    public class ProjectNotFoundException : Exception
    {
        public ProjectNotFoundException(string projectId)
            : base("Проект '" + projectId + "' не найден.") { }
    }

    public interface IProjectService
    {
        /// <summary>Открытый сейчас проект или null, если ничего не открыто.</summary>
        Project Current { get; }

        /// <summary>Открыт ли какой-либо проект.</summary>
        bool HasProject { get; }

        /// <summary>Проект загружен или создан и стал текущим.</summary>
        event Action<Project> ProjectLoaded;

        /// <summary>Содержимое текущего проекта изменилось. Вызывает ICommandService, больше никто.</summary>
        event Action ProjectChanged;

        /// <summary>Текущий проект успешно записан на диск (для уведомления «Сохранено», FR-404).</summary>
        event Action ProjectSaved;

        /// <summary>Список сохранённых проектов, самые свежие первыми.</summary>
        IReadOnlyList<ProjectInfo> ListProjects();

        /// <summary>Создаёт новый проект, делает его текущим и сохраняет. (FR-001, FR-007)</summary>
        Project Create(string name, ProjectSource source, string buildingId = null);

        /// <summary>
        /// Загружает проект и делает его текущим. (FR-002, FR-008)
        /// Исключения: ProjectNotFoundException, ProjectFormatException, UnsupportedProjectVersionException.
        /// </summary>
        Project Load(string projectId);

        /// <summary>Сохраняет текущий проект. Запись атомарная, с резервной копией. (FR-002, FR-003)</summary>
        void Save();

        /// <summary>Переименовывает проект (не обязательно открытый). (FR-002)</summary>
        void Rename(string projectId, string newName);

        /// <summary>Удаляет проект. Подтверждение у пользователя запрашивает UI, не сервис. (FR-002)</summary>
        void Delete(string projectId);

        /// <summary>Копирует проект в JSON-файл в указанной папке и возвращает путь к файлу. (FR-005)</summary>
        string Export(string projectId, string destinationFolder);

        /// <summary>Читает JSON-файл проекта, добавляет его в хранилище и открывает. (FR-005)</summary>
        Project Import(string filePath);

        /// <summary>
        /// Сообщает, что содержимое проекта изменилось (запускает автосохранение и событие ProjectChanged).
        /// Вызывает только ICommandService после выполнения, отмены или повтора команды.
        /// </summary>
        void MarkChanged();
    }
}
