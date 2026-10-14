using UnityEngine;

[CreateAssetMenu(menuName = "Furniture/Item")]
public class FurnitureItem : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public GameObject prefab;   // pivot по центру низа модели, на корне BoxCollider
    public string category;
}
