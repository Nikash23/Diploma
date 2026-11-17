// Реализация IProjectService поверх ProjectStorage (задача B-02).
// Не зависит от Unity. Подключение к жизненному циклу приложения (пауза, выход) - в UnityIntegration.

using System;
using System.Collections.Generic;
using System.IO;

namespace XrInterior.ProjectData
{
    public sealed class ProjectService : IProjectService
    {
        private readonly ProjectStorage _storage;
        private bool _dirty;

        public ProjectService(ProjectStorage storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            _storage = storage;
        }

        public Project Current { get; private set; }
        public bool HasProject { get { return Current != null; } }

        /// <summary>Сохранять сразу после каждого изменения (FR-003). По умолчанию включено.</summary>
        public bool AutoSaveOnChange { get; set; } = true;

        public event Action<Project> ProjectLoaded;
        public event Action ProjectChanged;
        public event Action ProjectSaved;

        public IReadOnlyList<ProjectInfo> ListProjects()
        {
            return _storage.List();
        }

        public Project Create(string name, ProjectSource source, string buildingId = null)
        {
            RequireName(name);
            SaveIfDirty();
            Project project = ProjectFactory.Create(name.Trim(), source, buildingId);
            _storage.Write(project);
            SetCurrent(project);
            return project;
        }

        public Project Load(string projectId)
        {
            SaveIfDirty();
            Project project = _storage.Read(projectId);
            SetCurrent(project);
            return project;
        }

        public void Save()
        {
            if (Current == null) return;
            Current.UpdatedAt = DateTime.UtcNow;
            _storage.Write(Current);
            _dirty = false;
            Action saved = ProjectSaved;
            if (saved != null) saved();
        }

        /// <summary>Сохраняет, только если есть несохранённые изменения (вызывается при паузе и выходе).</summary>
        public void SaveIfDirty()
        {
            if (Current != null && _dirty) Save();
        }

        public void Rename(string projectId, string newName)
        {
            RequireName(newName);
            if (Current != null && Current.Id == projectId)
            {
                Current.Name = newName.Trim();
                Save();
                Action changed = ProjectChanged;
                if (changed != null) changed();
                return;
            }

            Project project = _storage.Read(projectId);
            project.Name = newName.Trim();
            project.UpdatedAt = DateTime.UtcNow;
            _storage.Write(project);
        }

        public void Delete(string projectId)
        {
            _storage.Delete(projectId);
            if (Current != null && Current.Id == projectId)
            {
                Current = null;
                _dirty = false;
            }
        }

        public string Export(string projectId, string destinationFolder)
        {
            if (string.IsNullOrWhiteSpace(destinationFolder))
                throw new ArgumentException("Не указана папка для экспорта.", nameof(destinationFolder));

            Project project;
            if (Current != null && Current.Id == projectId)
            {
                SaveIfDirty();
                project = Current;
            }
            else
            {
                project = _storage.Read(projectId);
            }

            Directory.CreateDirectory(destinationFolder);
            string path = Path.Combine(destinationFolder, project.Id + ".json");
            File.WriteAllText(path, ProjectSerializer.ToJson(project), new System.Text.UTF8Encoding(false));
            return path;
        }

        public Project Import(string filePath)
        {
            SaveIfDirty();
            Project project = ProjectSerializer.FromJson(File.ReadAllText(filePath));

            var errors = ProjectValidator.Validate(project);
            if (errors.Count > 0)
                throw new ProjectFormatException("Файл проекта содержит ошибки: " + string.Join("; ", errors));

            // Чтобы импорт не затёр уже существующий проект с тем же id, делаем отдельную копию.
            if (_storage.Exists(project.Id))
            {
                project.Id = IdGenerator.New("project");
                project.Name = project.Name + " (копия)";
            }

            project.UpdatedAt = DateTime.UtcNow;
            _storage.Write(project);
            SetCurrent(project);
            return project;
        }

        public void MarkChanged()
        {
            if (Current == null) return;
            _dirty = true;
            Current.UpdatedAt = DateTime.UtcNow;

            Action changed = ProjectChanged;
            if (changed != null) changed();

            if (AutoSaveOnChange) Save();
        }

        // ---------- служебное ----------

        private void SetCurrent(Project project)
        {
            Current = project;
            _dirty = false;
            Action<Project> loaded = ProjectLoaded;
            if (loaded != null) loaded(project);
        }

        private static void RequireName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название проекта не может быть пустым.");
        }
    }
}
