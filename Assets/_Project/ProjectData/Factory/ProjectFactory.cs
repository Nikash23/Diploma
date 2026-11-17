// Создание новых проектов и идентификаторов.

using System;

namespace XrInterior.ProjectData
{
    public static class IdGenerator
    {
        /// <summary>Новый уникальный id вида "obj_3f9a1c2b7d4e".</summary>
        public static string New(string prefix)
        {
            return prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }
    }

    public static class ProjectFactory
    {
        /// <summary>Пустой проект с одним вариантом планировки.</summary>
        public static Project Create(string name, ProjectSource source, string buildingId = null)
        {
            DateTime now = DateTime.UtcNow;
            string variantId = IdGenerator.New("variant");

            var project = new Project
            {
                Id = IdGenerator.New("project"),
                Name = name,
                Source = source,
                BuildingId = buildingId,
                CreatedAt = now,
                UpdatedAt = now,
                ActiveVariantId = variantId
            };
            project.Variants.Add(new Variant { Id = variantId, Name = "Вариант 1" });
            return project;
        }
    }
}
