// Базовые типы данных проекта: векторы и перечисления.
// Здесь НЕТ UnityEngine: так этот код одинаково собирается для Quest и для Android (задача B-05).
// Все координаты в метрах, ось Y направлена вверх (раздел 5.3 ТЗ).

namespace XrInterior.ProjectData
{
    /// <summary>Точка в плане (вид сверху): X вправо, Z вперёд. Метры.</summary>
    public struct PlanPoint
    {
        public float X;
        public float Z;

        public PlanPoint(float x, float z) { X = x; Z = z; }
    }

    /// <summary>Вектор или позиция в 3D. Метры (для вращения - градусы Эйлера).</summary>
    public struct Vec3
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static Vec3 Zero => new Vec3(0f, 0f, 0f);
        public static Vec3 One => new Vec3(1f, 1f, 1f);
        public static Vec3 Forward => new Vec3(0f, 0f, 1f);
    }

    /// <summary>Размер плоской поверхности: ширина и высота, метры.</summary>
    public struct Size2
    {
        public float Width;
        public float Height;

        public Size2(float width, float height) { Width = width; Height = height; }
    }

    /// <summary>Существующее (в реальном здании) или проектируемое (будет изменено/добавлено).</summary>
    public enum ElementState { Existing, Proposed }

    /// <summary>Откуда создан проект (поле source в ТЗ, раздел 6).</summary>
    public enum ProjectSource { Template, Scan, Bim }

    public enum RoomTemplate { Custom, Rectangular, LShaped, Corridor, Auditorium }

    public enum OpeningType { Door, Window }

    public enum LightKind { Point, Spot }

    public enum XrPlatform { Quest, Android }

    public enum SnapshotPlaneKind { Wall, Floor, Ceiling, Door, Window }

    /// <summary>Результат сравнения элемента с реальностью (FR-210, FR-215, FR-217).</summary>
    public enum ComparisonStatus { Matched, NotBuilt, Extra, Accepted, Rejected }

    /// <summary>Идентификаторы базовых слоёв (FR-108). Список надо согласовать с командой.</summary>
    public static class LayerIds
    {
        public const string Architecture = "architecture";
        public const string Furniture = "furniture";
        public const string Lighting = "lighting";
        public const string Annotations = "annotations";

        public static readonly string[] All = { Architecture, Furniture, Lighting, Annotations };
    }
}
