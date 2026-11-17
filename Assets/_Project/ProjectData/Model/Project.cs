// Корневой объект: всё содержимое файла проекта.

using System;
using System.Collections.Generic;

namespace XrInterior.ProjectData
{
    public class Project : ExtensibleEntity
    {
        /// <summary>Текущая версия схемы. Увеличивается при любом несовместимом изменении формата.</summary>
        public const int CurrentSchemaVersion = 1;

        public int Version { get; set; } = CurrentSchemaVersion;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Units { get; set; } = "meters";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ProjectSource Source { get; set; } = ProjectSource.Template;
        /// <summary>Выбранное здание (FR-001).</summary>
        public string BuildingId { get; set; }

        public Reference Reference { get; set; } = new Reference();

        public List<Room> Rooms { get; set; } = new List<Room>();
        public List<Wall> Walls { get; set; } = new List<Wall>();
        public List<Opening> Openings { get; set; } = new List<Opening>();
        public List<PlacedObject> Objects { get; set; } = new List<PlacedObject>();
        public List<ProjectLight> Lights { get; set; } = new List<ProjectLight>();
        public List<MaterialOverride> MaterialOverrides { get; set; } = new List<MaterialOverride>();

        public List<Variant> Variants { get; set; } = new List<Variant>();
        public string ActiveVariantId { get; set; }

        public List<Alignment> Alignments { get; set; } = new List<Alignment>();
        public List<SpaceSnapshot> SpaceSnapshots { get; set; } = new List<SpaceSnapshot>();
        public List<ComparisonResult> ComparisonResults { get; set; } = new List<ComparisonResult>();
    }
}
