using UnityEngine;
using UnityEngine.EventSystems;

// Вешается на иконку в меню (создаётся скриптом CatalogMenu автоматически).
public class CatalogDragItem : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    public FurnitureItem item;
    public PlacementController placement;

    Vector3 baseScale;

    void Awake() { baseScale = transform.localScale; }

    public void OnPointerDown(PointerEventData e)
    {
        if (placement == null || item == null || placement.IsPlacing) return;
        placement.BeginPlacement(item, true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (placement == null || !placement.IsPlacing) return;
        placement.TryPlace();
    }

    public void OnPointerEnter(PointerEventData e) { transform.localScale = baseScale * 1.08f; }
    public void OnPointerExit(PointerEventData e) { transform.localScale = baseScale; }
}