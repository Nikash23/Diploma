using UnityEngine;

[ExecuteAlways]
public class SectionPlane : MonoBehaviour
{
    static readonly int SectionY = Shader.PropertyToID("_SectionY");
    const float Off = 10000f;

    public bool sectionEnabled = true;
    public Transform building;     // сюда перетащить BuildingLayers
    public float minY = 0f;
    public float maxY = 7f;

    Vector2 lockedXZ;

    void OnEnable()
    {
        lockedXZ = new Vector2(transform.position.x, transform.position.z);
    }

    void LateUpdate()
    {
        var p = transform.position;
        p.x = lockedXZ.x;
        p.z = lockedXZ.y;
        p.y = Mathf.Clamp(p.y, minY, maxY);
        transform.SetPositionAndRotation(p, Quaternion.identity);
        Shader.SetGlobalFloat(SectionY, sectionEnabled ? p.y : Off);
    }

    [ContextMenu("Fit to building")]
    public void FitToBuilding()
    {
        if (building == null) { Debug.LogWarning("Перетащите BuildingLayers в поле Building"); return; }

        var renderers = building.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        minY = b.min.y;
        maxY = b.max.y;
        Debug.Log($"Здание: низ = {minY:F2} м, верх = {maxY:F2} м, высота = {b.size.y:F2} м");

        var p = transform.position;
        p.y = maxY;
        transform.position = p;
    }

    public void ResetSection()
    {
        var p = transform.position;
        p.y = maxY;
        transform.position = p;
    }

    void OnDisable() => Shader.SetGlobalFloat(SectionY, Off);
}