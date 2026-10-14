using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PlacementController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] XRRayInteractor ray;               // объект Placement Ray
    [SerializeField] InputActionProperty confirm;       // XRI Right Interaction/Activate
    [SerializeField] InputActionProperty rotate;        // (необязательно) стик, Vector2

    [Header("Слои")]
    [SerializeField] LayerMask floorMask;               // Floor
    [SerializeField] LayerMask blockMask;               // Furniture + Wall

    [Header("Материалы призрака")]
    [SerializeField] Material okMat;
    [SerializeField] Material badMat;

    [Header("Настройки")]
    [SerializeField] float gridStep = 0.05f;            // 0 = без сетки
    [SerializeField] float rotateSpeed = 90f;

    GameObject ghost;
    FurnitureItem current;
    Renderer[] rends;
    Material[][] origMats;
    BoxCollider box;
    float yaw;
    bool valid;

    public bool IsPlacing => ghost != null;

    void OnEnable()
    {
        if (confirm.reference == null && confirm.action != null) confirm.action.Enable();
        if (rotate.reference == null && rotate.action != null) rotate.action.Enable();
    }

    public void BeginPlacement(FurnitureItem item)
    {
        Cancel();
        current = item;
        ghost = Instantiate(item.prefab);

        rends = ghost.GetComponentsInChildren<Renderer>();
        origMats = rends.Select(r => r.sharedMaterials).ToArray();
        box = ghost.GetComponentInChildren<BoxCollider>();
        if (box == null)
        {
            Debug.LogError($"У префаба {item.name} нет BoxCollider");
            Destroy(ghost); ghost = null; return;
        }
        box.isTrigger = true;
        SetLayerRecursive(ghost, LayerMask.NameToLayer("Ignore Raycast")); // луч не попадает в призрак
        yaw = 0f;
        valid = false;

        // луч смотрит только на пол, мебель, стены и UI
        int ui = LayerMask.NameToLayer("UI");
        ray.raycastMask = floorMask | blockMask | (1 << ui);
        ray.gameObject.SetActive(true);
    }

    void Update()
    {
        if (ghost == null) return;

        // отмена по Esc
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cancel();
            return;
        }

        // поворот: Q/E (симулятор) + стик (гарнитура)
        float rot = 0f;
        if (Keyboard.current != null)
            rot += (Keyboard.current.eKey.isPressed ? 1f : 0f) - (Keyboard.current.qKey.isPressed ? 1f : 0f);
        if (rotate.action != null) rot += rotate.action.ReadValue<Vector2>().x;
        yaw += rot * rotateSpeed * Time.deltaTime;

        if (ray.TryGetCurrent3DRaycastHit(out RaycastHit hit) &&
            (floorMask.value & (1 << hit.collider.gameObject.layer)) != 0)
        {
            Vector3 p = hit.point;
            if (gridStep > 0f)
            {
                p.x = Mathf.Round(p.x / gridStep) * gridStep;
                p.z = Mathf.Round(p.z / gridStep) * gridStep;
            }
            ghost.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            SnapToFloor(hit.point.y);

            valid = !Overlaps();
            SetColor(valid ? okMat : badMat);
        }
        else
        {
            valid = false;
            SetColor(badMat);
        }

        if (confirm.action != null && confirm.action.WasPressedThisFrame() && valid)
            Place();
    }

    // нижняя точка модели ровно на уровне пола (если pivot не по низу)
    void SnapToFloor(float floorY)
    {
        var b = new Bounds(rends[0].bounds.center, Vector3.zero);
        foreach (var r in rends) b.Encapsulate(r.bounds);
        ghost.transform.position += Vector3.up * (floorY - b.min.y);
    }

    bool Overlaps()
    {
        Transform t = box.transform;
        Vector3 center = t.TransformPoint(box.center);
        Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f * 0.98f;
        half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
        // чуть приподнимаем, чтобы пол не считался пересечением
        center += Vector3.up * 0.005f;
        var hits = Physics.OverlapBox(center, half, t.rotation, blockMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (!h.transform.IsChildOf(ghost.transform)) return true;
        return false;
    }

    void Place()
    {
        box.isTrigger = false;
        RestoreMaterials();
        SetLayerRecursive(ghost, LayerMask.NameToLayer("Furniture"));
        var pf = ghost.AddComponent<PlacedFurniture>();
        pf.item = current;
        ghost = null;
        ray.gameObject.SetActive(false);
    }

    public void Cancel()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        if (ray != null) ray.gameObject.SetActive(false);
    }

    void SetColor(Material m)
    {
        foreach (var r in rends)
        {
            var arr = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = m;
            r.sharedMaterials = arr;
        }
    }

    void RestoreMaterials()
    {
        for (int i = 0; i < rends.Length; i++) rends[i].sharedMaterials = origMats[i];
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }
}