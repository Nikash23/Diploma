// Выбор предмета в редакторе (B-07, FR-103). Не зависит от Unity: одинаково работает в VR, MR и на телефоне.

using System;

namespace XrInterior.ProjectData
{
    public sealed class EditorSelection
    {
        public string SelectedId { get; private set; }
        public bool HasSelection { get { return SelectedId != null; } }

        /// <summary>Вызывается при смене выбора; параметр: новый id или null, если выбор снят.</summary>
        public event Action<string> SelectionChanged;

        public void Select(string objectId)
        {
            if (SelectedId == objectId) return;
            SelectedId = objectId;
            Action<string> handler = SelectionChanged;
            if (handler != null) handler(SelectedId);
        }

        public void Clear() { Select(null); }

        /// <summary>Выбирает следующий по списку предмет проекта (по кругу). Возвращает его id или null, если предметов нет.</summary>
        public string SelectNext(Project project)
        {
            if (project == null || project.Objects.Count == 0) { Clear(); return null; }
            int index = project.Objects.FindIndex(o => o.Id == SelectedId);
            PlacedObject next = project.Objects[(index + 1) % project.Objects.Count];
            Select(next.Id);
            return next.Id;
        }

        /// <summary>Снимает выбор, если выбранного предмета больше нет в проекте (удалён или отменено добавление).</summary>
        public void Refresh(Project project)
        {
            if (SelectedId == null) return;
            if (project == null || !project.Objects.Exists(o => o.Id == SelectedId)) Clear();
        }
    }
}
