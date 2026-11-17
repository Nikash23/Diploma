// Контракт: каталог предметов и материалов (ТЗ, раздел 11; FR-101, FR-107).
// Владелец: роль B (данные). Модели и текстуры делает C, интерфейс каталога тоже у C.
// Менять только по согласованному запросу (T-02).

using System.Collections.Generic;

namespace XrInterior.ProjectData
{
    public interface ICatalogService
    {
        /// <summary>Категории предметов (диваны, столы, шкафы и т. п.).</summary>
        IReadOnlyList<CatalogCategory> GetCategories();

        /// <summary>
        /// Поиск предметов. text - часть названия (пусто = все), categoryId - ограничение категорией (null = все).
        /// Никогда не возвращает null.
        /// </summary>
        IReadOnlyList<CatalogItem> Search(string text = null, string categoryId = null);

        /// <summary>Предмет по id или null, если такого нет.</summary>
        CatalogItem GetItem(string catalogId);

        /// <summary>
        /// Материалы, подходящие для поверхности (например MaterialSurface.Floor).
        /// MaterialSurface.All - вообще все материалы. Никогда не возвращает null.
        /// </summary>
        IReadOnlyList<MaterialInfo> GetMaterials(MaterialSurface surface = MaterialSurface.All);

        /// <summary>Материал по id или null, если такого нет.</summary>
        MaterialInfo GetMaterial(string materialId);
    }
}
