// Инструменты редактора (B-07, FR-103, FR-113): переместить, повернуть, удалить, дублировать.
// Одни и те же вызовы использует и XR-слой (контроллеры Quest в VR и MR), и предпросмотр.
// Все изменения проходят через ICommandService (undo/redo). Не зависит от Unity.
//
// Правила:
//  - перемещение привязывается к сетке и правилам каталога (PlacementService); пересечение с реальной
//    геометрией блокирует действие (FR-207), пересечения со стенами и предметами дают предупреждение;
//  - поворот идёт шагом из SnapSettings (по умолчанию 15°); предметы на стене не вращаются (их поворот задаёт стена);
//  - дубликат ставится рядом с оригиналом вдоль его локальной оси X.

using System;

namespace XrInterior.ProjectData
{
    public sealed class ToolResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        /// <summary>Предупреждение (например, пересечение с другим предметом). Действие при этом выполнено.</summary>
        public string Warning { get; private set; }
        /// <summary>Id созданного объекта (для дублирования).</summary>
        public string ObjectId { get; private set; }

        public static ToolResult Ok(string message, string warning = null, string objectId = null)
        {
            return new ToolResult { Success = true, Message = message, Warning = warning, ObjectId = objectId };
        }

        public static ToolResult Fail(string message)
        {
            return new ToolResult { Success = false, Message = message };
        }
    }

    public sealed class EditorTools
    {
        /// <summary>Зазор между оригиналом и дубликатом, метры.</summary>
        public const float DuplicateGap = 0.1f;

        private const float DegToRad = (float)Math.PI / 180f;

        private readonly IProjectService _projects;
        private readonly ICommandService _commands;
        private readonly ICatalogService _catalog;
        private readonly SnapSettings _snap;

        /// <summary>Проверка реальной геометрии (сервис A). null - реальных объектов нет.</summary>
        public IRealityCollisionChecker Reality { get; set; }

        public EditorTools(IProjectService projects, ICommandService commands, ICatalogService catalog,
            SnapSettings snap = null)
        {
            if (projects == null) throw new ArgumentNullException(nameof(projects));
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            _projects = projects;
            _commands = commands;
            _catalog = catalog;
            _snap = snap ?? SnapSettings.Default;
        }

        /// <summary>Переносит предмет в точку плана (с привязкой к сетке и правилами размещения).</summary>
        public ToolResult MoveTo(string objectId, PlanPoint target)
        {
            Project p; PlacedObject obj; CatalogItem item;
            ToolResult fail = Resolve(objectId, out p, out obj, out item);
            if (fail != null) return fail;

            int floor = FloorOf(p);
            PlacementResult placement = PlacementService.Place(p, item, target, obj.Rotation.Y, floor, _snap);
            if (!placement.IsValid) return ToolResult.Fail("Нельзя переместить: " + placement.Reason);

            PlacedObject candidate = Candidate(obj, placement.Position,
                new Vec3(obj.Rotation.X, placement.RotationY, obj.Rotation.Z));
            CollisionReport report = CollisionService.CheckObject(p, candidate, _catalog, Reality, floor);
            if (report.HasBlocking) return ToolResult.Fail("Нельзя переместить: " + report.BlockingSummary);

            _commands.Execute(new SetObjectTransformCommand(obj.Id, candidate.Position, candidate.Rotation,
                obj.Scale, "Переместить: " + item.Name));
            return ToolResult.Ok("Перемещено: " + item.Name + ".", report.HasCollisions ? report.Summary : null);
        }

        /// <summary>Поворачивает предмет вокруг вертикальной оси на заданный угол с привязкой к шагу.</summary>
        public ToolResult RotateBy(string objectId, float degrees)
        {
            Project p; PlacedObject obj; CatalogItem item;
            ToolResult fail = Resolve(objectId, out p, out obj, out item);
            if (fail != null) return fail;

            if (item.Placement == PlacementRule.Wall)
                return ToolResult.Fail("Предмет на стене не вращается: его поворот задаёт стена.");

            float yaw = GridSnap.SnapYaw(obj.Rotation.Y + degrees, _snap.RotationStepDegrees);
            PlacedObject candidate = Candidate(obj, obj.Position, new Vec3(obj.Rotation.X, yaw, obj.Rotation.Z));
            int floor = FloorOf(p);
            CollisionReport report = CollisionService.CheckObject(p, candidate, _catalog, Reality, floor);
            if (report.HasBlocking) return ToolResult.Fail("Нельзя повернуть: " + report.BlockingSummary);

            _commands.Execute(new SetObjectTransformCommand(obj.Id, obj.Position, candidate.Rotation,
                obj.Scale, "Повернуть: " + item.Name));
            return ToolResult.Ok("Повёрнуто: " + item.Name + " (" + yaw.ToString("0.#") + "°).",
                report.HasCollisions ? report.Summary : null);
        }

        /// <summary>Удаляет предмет вместе с его материалами и состояниями в вариантах.</summary>
        public ToolResult Delete(string objectId)
        {
            Project p = _projects.Current;
            if (p == null) return ToolResult.Fail("Нет открытого проекта.");
            PlacedObject obj = p.Objects.Find(o => o.Id == objectId);
            if (obj == null) return ToolResult.Fail("Объект не найден.");

            _commands.Execute(new RemoveObjectCommand(obj.Id, "Удалить объект"));
            return ToolResult.Ok("Удалено.");
        }

        /// <summary>Создаёт копию предмета рядом с оригиналом. В ObjectId результата лежит id копии.</summary>
        public ToolResult Duplicate(string objectId)
        {
            Project p; PlacedObject obj; CatalogItem item;
            ToolResult fail = Resolve(objectId, out p, out obj, out item);
            if (fail != null) return fail;

            float distance = item.Size.X * Math.Abs(obj.Scale.X) + DuplicateGap;
            float yaw = obj.Rotation.Y * DegToRad;
            float x = GridSnap.Snap(obj.Position.X + (float)Math.Cos(yaw) * distance, _snap.PositionStep);
            float z = GridSnap.Snap(obj.Position.Z - (float)Math.Sin(yaw) * distance, _snap.PositionStep);
            var position = new Vec3(x, obj.Position.Y, z);

            var command = new DuplicateObjectCommand(obj.Id, position, null, "Дублировать: " + item.Name);
            PlacedObject candidate = Candidate(obj, position, obj.Rotation);
            candidate.Id = command.NewObjectId;

            int floor = FloorOf(p);
            CollisionReport report = CollisionService.CheckObject(p, candidate, _catalog, Reality, floor);
            if (report.HasBlocking) return ToolResult.Fail("Нельзя дублировать: " + report.BlockingSummary);

            _commands.Execute(command);
            return ToolResult.Ok("Создана копия: " + item.Name + ".",
                report.HasCollisions ? report.Summary : null, command.NewObjectId);
        }

        // ---------- вспомогательное ----------

        private ToolResult Resolve(string objectId, out Project project, out PlacedObject obj, out CatalogItem item)
        {
            obj = null;
            item = null;
            project = _projects.Current;
            if (project == null) return ToolResult.Fail("Нет открытого проекта.");

            obj = project.Objects.Find(o => o.Id == objectId);
            if (obj == null) return ToolResult.Fail("Объект не найден.");

            item = _catalog.GetItem(obj.CatalogId);
            if (item == null)
                return ToolResult.Fail("Предмета '" + obj.CatalogId + "' нет в каталоге, действие недоступно.");
            return null;
        }

        private static PlacedObject Candidate(PlacedObject source, Vec3 position, Vec3 rotation)
        {
            return new PlacedObject
            {
                Id = source.Id,
                CatalogId = source.CatalogId,
                Position = position,
                Rotation = rotation,
                Scale = source.Scale
            };
        }

        private static int FloorOf(Project p)
        {
            return p.Rooms.Count > 0 ? p.Rooms[0].Floor : 1;
        }
    }
}
