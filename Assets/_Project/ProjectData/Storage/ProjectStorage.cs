// Хранилище проектов на диске (задача B-02, FR-002, FR-003).
// Один проект = три возможных файла в папке хранилища:
//   <id>.json      основной файл
//   <id>.json.bak  резервная копия предыдущего сохранения
//   <id>.json.tmp  временный файл, в который пишем новую версию
//
// Как сохраняем (атомарно):
//   1. пишем новую версию во временный файл и проверяем, что он читается;
//   2. если основной файл читается - копируем его в .bak;
//   3. удаляем основной файл и переименовываем временный на его место.
// Если питание или приложение оборвётся на любом шаге, при чтении найдётся целая копия.
//
// Класс не зависит от Unity.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XrInterior.ProjectData
{
    public sealed class ProjectStorage
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _root;

        public string RootFolder { get { return _root; } }

        public ProjectStorage(string rootFolder)
        {
            if (string.IsNullOrWhiteSpace(rootFolder))
                throw new ArgumentException("Не указана папка хранилища.", nameof(rootFolder));
            _root = rootFolder;
            Directory.CreateDirectory(_root);
        }

        public string MainPath(string id) { CheckId(id); return Path.Combine(_root, id + ".json"); }
        public string BackupPath(string id) { return MainPath(id) + ".bak"; }
        public string TempPath(string id) { return MainPath(id) + ".tmp"; }

        public bool Exists(string id)
        {
            return File.Exists(MainPath(id)) || File.Exists(TempPath(id)) || File.Exists(BackupPath(id));
        }

        /// <summary>Атомарно записывает проект на диск.</summary>
        public void Write(Project project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            string main = MainPath(project.Id);
            string tmp = TempPath(project.Id);
            string bak = BackupPath(project.Id);

            string json = ProjectSerializer.ToJson(project);

            // 1. Временный файл, сброшенный на диск.
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Utf8NoBom.GetBytes(json);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            // Проверка: записанное должно читаться.
            try
            {
                ProjectSerializer.FromJson(File.ReadAllText(tmp, Utf8NoBom));
            }
            catch (ProjectFormatException ex)
            {
                TryDelete(tmp);
                throw new IOException("Не удалось корректно записать проект '" + project.Id + "'.", ex);
            }

            // 2-3. Резервная копия (только если основной файл целый, чтобы не затереть хорошую копию битой) и замена.
            if (File.Exists(main))
            {
                if (IsReadable(main)) File.Copy(main, bak, true);
                File.Delete(main);
            }
            File.Move(tmp, main);
        }

        public Project Read(string id)
        {
            bool recovered;
            return Read(id, out recovered);
        }

        /// <summary>
        /// Читает проект. Порядок поиска целой копии: основной файл, временный, резервный.
        /// recoveredFromBackup = true, если основной файл прочитать не удалось и использована другая копия
        /// (в этом случае основной файл будет восстановлен).
        /// Исключения: ProjectNotFoundException, UnsupportedProjectVersionException (без попытки восстановления
        /// из копии), ProjectFormatException (повреждены все копии).
        /// </summary>
        public Project Read(string id, out bool recoveredFromBackup)
        {
            recoveredFromBackup = false;
            string[] candidates = { MainPath(id), TempPath(id), BackupPath(id) };
            bool anyFile = false;
            ProjectFormatException firstError = null;

            for (int i = 0; i < candidates.Length; i++)
            {
                string path = candidates[i];
                if (!File.Exists(path)) continue;
                anyFile = true;
                try
                {
                    Project p = ProjectSerializer.FromJson(File.ReadAllText(path, Utf8NoBom));
                    if (p.Id != id)
                        throw new ProjectFormatException("Идентификатор внутри файла не совпадает с его именем.");
                    if (i > 0)
                    {
                        recoveredFromBackup = true;
                        TryRepairMain(p);
                    }
                    return p;
                }
                catch (UnsupportedProjectVersionException)
                {
                    throw; // файл новой версии не заменяем старой копией
                }
                catch (ProjectFormatException ex)
                {
                    if (firstError == null) firstError = ex;
                }
            }

            if (!anyFile) throw new ProjectNotFoundException(id);
            throw new ProjectFormatException(
                "Проект '" + id + "' повреждён, восстановить его из копий не удалось: " + firstError.Message, firstError);
        }

        public void Delete(string id)
        {
            TryDelete(MainPath(id));
            TryDelete(TempPath(id));
            TryDelete(BackupPath(id));
        }

        /// <summary>Список проектов в хранилище, самые свежие первыми. Нечитаемые проекты в список не попадают.</summary>
        public IReadOnlyList<ProjectInfo> List()
        {
            var ids = new HashSet<string>();
            foreach (string file in Directory.GetFiles(_root))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".json.tmp") || name.EndsWith(".json.bak"))
                    name = name.Substring(0, name.Length - 4);
                if (!name.EndsWith(".json")) continue;
                ids.Add(name.Substring(0, name.Length - 5));
            }

            var result = new List<ProjectInfo>();
            foreach (string id in ids)
            {
                try
                {
                    Project p = Read(id);
                    result.Add(new ProjectInfo { Id = p.Id, Name = p.Name, Source = p.Source, UpdatedAt = p.UpdatedAt });
                }
                catch (ProjectFormatException) { }
                catch (ProjectNotFoundException) { }
                catch (ArgumentException) { }
                catch (IOException) { }
            }
            result.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));
            return result;
        }

        // ---------- служебное ----------

        private static void CheckId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Пустой идентификатор проекта.");
            if (id != Path.GetFileName(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Недопустимый идентификатор проекта: '" + id + "'.");
        }

        private static bool IsReadable(string path)
        {
            try
            {
                ProjectSerializer.FromJson(File.ReadAllText(path, Utf8NoBom));
                return true;
            }
            catch (UnsupportedProjectVersionException) { return true; } // файл целый, просто новее; копию сохраняем
            catch (ProjectFormatException) { return false; }
            catch (IOException) { return false; }
        }

        private void TryRepairMain(Project p)
        {
            try { Write(p); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
