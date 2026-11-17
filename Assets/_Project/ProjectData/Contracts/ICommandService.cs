// Контракт: изменения проекта и история undo/redo (ТЗ, раздел 11; FR-106).
// Владелец: роль B. Менять только по согласованному запросу (T-02).
//
// Любое изменение проекта - это команда. Модули XR, AR и UI создают команды
// и передают их в Execute, а сами Project не трогают.

using System;

namespace XrInterior.ProjectData
{
    /// <summary>Одно обратимое изменение проекта.</summary>
    public interface ICommand
    {
        /// <summary>Понятное название для интерфейса: «Переместить диван», «Добавить стену».</summary>
        string Name { get; }

        /// <summary>Применяет изменение к проекту. Вызывается при первом выполнении и при повторе (redo).</summary>
        void Execute(Project project);

        /// <summary>Полностью отменяет изменение, возвращая проект в прежнее состояние.</summary>
        void Undo(Project project);
    }

    public interface ICommandService
    {
        /// <summary>Выполняет команду, добавляет её в историю и очищает список повторов (redo).</summary>
        void Execute(ICommand command);

        bool CanUndo { get; }
        bool CanRedo { get; }

        /// <summary>Отменяет последнюю команду. Если отменять нечего - ничего не делает.</summary>
        void Undo();

        /// <summary>Повторяет последнюю отменённую команду. Если повторять нечего - ничего не делает.</summary>
        void Redo();

        /// <summary>Название команды, которую отменит Undo (для подсказки на кнопке), или null.</summary>
        string NextUndoName { get; }

        /// <summary>Название команды, которую повторит Redo, или null.</summary>
        string NextRedoName { get; }

        /// <summary>Сколько команд хранится в истории. Не меньше 50 (FR-106).</summary>
        int Capacity { get; }

        /// <summary>История изменилась (выполнена, отменена или повторена команда, либо история очищена).</summary>
        event Action HistoryChanged;
    }
}
