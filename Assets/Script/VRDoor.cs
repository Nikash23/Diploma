using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Дверь для VR. Вращается строго вокруг вертикальной (мировой) оси,
/// проходящей через точку петель (Hinge Point).
/// Вешается на саму Door (или на DoorPivot). Collider должен быть на этом объекте или его дочерних.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class VRDoor : MonoBehaviour
{
    [Header("Настройки двери")]
    [Tooltip("Угол открытия в градусах (минус - в другую сторону)")]
    public float openAngle = 90f;

    [Tooltip("Скорость открывания, градусов в секунду")]
    public float speed = 120f;

    [Header("Петли")]
    [Tooltip("Пустой объект, стоящий на оси петель (край двери). Если не задан - берётся позиция этого объекта.")]
    public Transform hingePoint;

    [Header("Звуки (необязательно)")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    private XRSimpleInteractable interactable;
    private float currentAngle = 0f;
    private bool isOpen = false;

    Vector3 HingePosition => hingePoint != null ? hingePoint.position : transform.position;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
    }

    void OnEnable() { interactable.selectEntered.AddListener(OnSelect); }
    void OnDisable() { interactable.selectEntered.RemoveListener(OnSelect); }

    private void OnSelect(SelectEnterEventArgs args)
    {
        isOpen = !isOpen;

        if (audioSource != null)
        {
            AudioClip clip = isOpen ? openSound : closeSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }
    }

    void Update()
    {
        float target = isOpen ? openAngle : 0f;
        float next = Mathf.MoveTowards(currentAngle, target, speed * Time.deltaTime);
        float delta = next - currentAngle;

        if (Mathf.Abs(delta) > 0.0001f)
        {
            // Поворот вокруг мировой вертикальной оси через точку петель
            transform.RotateAround(HingePosition, Vector3.up, delta);
            currentAngle = next;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 p = HingePosition;
        Gizmos.DrawSphere(p, 0.03f);
        Gizmos.DrawLine(p + Vector3.down * 1.2f, p + Vector3.up * 1.2f);
    }
}