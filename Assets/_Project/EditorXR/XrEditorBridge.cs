// XR-надстройка над ProjectPreview (B-07, часть 2, FR-103, FR-113).
// ProjectPreview рисует проект и выполняет все действия, он не знает про XR. Этот компонент добавляет только то,
// что зависит от XR Interaction Toolkit: хват предмета лучом контроллера, кнопки контроллеров и клавиши 1-6
// (для проверки без гарнитуры), постановку XR-рига в комнату и подсказку перед глазами.
// Все изменения проекта выполняют EditorTools и команды (undo/redo): здесь правил нет.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XrInterior.ProjectData;
using XrInterior.ProjectData.UnityIntegration;

namespace XrInterior.EditorXR
{
    public class XrEditorBridge : MonoBehaviour
    {
        [Header("Предпросмотр проекта (если пусто, ищется на этом же объекте или в сцене)")]
        public ProjectPreview preview;

        [Header("XR-риг: перетащить сюда объект XR Origin (XR Rig)")]
        public Transform xrOrigin;

        [Header("Подсказка перед глазами: если текст слишком крупный или мелкий, меняйте это число")]
        public float hudScale = 0.05f;

        private ProjectPreview _preview;
        private int _grabCount;
        private bool _rigPlaced;
        private TextMesh _hud;
        private string _shownStatus;

        private InputAction _rotate;
        private InputAction _duplicate;
        private InputAction _delete;
        private InputAction _undo;
        private InputAction _redo;

        // ---------- запуск ----------

        private void Awake()
        {
            _preview = preview != null ? preview : GetComponent<ProjectPreview>();
            if (_preview == null) _preview = FindFirstObjectByType<ProjectPreview>();
            if (_preview == null)
            {
                Debug.LogError("XrEditorBridge: в сцене нет ProjectPreview.");
                enabled = false;
                return;
            }

            _preview.frameCamera = false;   // камерой управляет XR-риг, а не предпросмотр
            _preview.ObjectViewBuilt += OnObjectViewBuilt;
            _preview.ViewRebuilt += OnViewRebuilt;
            SetupInput();
        }

        private void Start()
        {
            CreateHud();
        }

        private void OnDestroy()
        {
            if (_preview != null)
            {
                _preview.ObjectViewBuilt -= OnObjectViewBuilt;
                _preview.ViewRebuilt -= OnViewRebuilt;
            }
            DisposeAction(_rotate); DisposeAction(_duplicate); DisposeAction(_delete);
            DisposeAction(_undo); DisposeAction(_redo);
        }

        private void Update()
        {
            if (_preview == null || _preview.Tools == null || _preview.Projects == null
                || _preview.Projects.Current == null) return;

            // Кнопки контроллеров Touch и дублирующие клавиши 1-6 (для проверки без гарнитуры).
            Keyboard kb = Keyboard.current;
            if (_rotate.WasPressedThisFrame() || KeyPressed(kb, 1)) Do(RotateSelected);
            if (_duplicate.WasPressedThisFrame() || KeyPressed(kb, 2)) Do(DuplicateSelected);
            if (_delete.WasPressedThisFrame() || KeyPressed(kb, 3)) Do(DeleteSelected);
            if (_undo.WasPressedThisFrame() || KeyPressed(kb, 4)) Do(UndoLast);
            if (_redo.WasPressedThisFrame() || KeyPressed(kb, 5)) Do(RedoLast);
            if (KeyPressed(kb, 6)) Do(SelectNextObject);

            if (_preview.Status != _shownStatus) UpdateHud();
        }

        // ---------- действия ----------

        private void Do(Action action)
        {
            try
            {
                _preview.ApplyReality();
                action();
            }
            catch (Exception ex)
            {
                _preview.Status = "Ошибка: " + ex.Message;
                Debug.LogError(ex);
            }
        }

        private void Report(ToolResult r)
        {
            _preview.Status = r.Message + (r.Warning != null ? " Предупреждение: " + r.Warning : "");
        }

        private void RotateSelected() { Report(_preview.Tools.RotateBy(_preview.Selection.SelectedId, 15f)); }

        private void DeleteSelected() { Report(_preview.Tools.Delete(_preview.Selection.SelectedId)); }

        private void DuplicateSelected()
        {
            ToolResult r = _preview.Tools.Duplicate(_preview.Selection.SelectedId);
            Report(r);
            if (r.Success) _preview.Selection.Select(r.ObjectId);
        }

        private void SelectNextObject()
        {
            string id = _preview.Selection.SelectNext(_preview.Projects.Current);
            _preview.Status = id == null ? "Предметов нет." : "Выбран: " + NameOf(id) + ".";
        }

        private void UndoLast()
        {
            CommandService commands = _preview.Commands;
            if (!commands.CanUndo) { _preview.Status = "Отменять нечего."; return; }
            string name = commands.NextUndoName;
            commands.Undo();
            _preview.Status = "Отменено: " + name + ".";
        }

        private void RedoLast()
        {
            CommandService commands = _preview.Commands;
            if (!commands.CanRedo) { _preview.Status = "Повторять нечего."; return; }
            string name = commands.NextRedoName;
            commands.Redo();
            _preview.Status = "Повторено: " + name + ".";
        }

        private string NameOf(string objectId)
        {
            Project p = _preview.Projects.Current;
            PlacedObject o = p == null ? null : p.Objects.Find(x => x.Id == objectId);
            if (o == null) return "?";
            CatalogItem item = _preview.Catalog != null ? _preview.Catalog.GetItem(o.CatalogId) : null;
            return item != null ? item.Name : o.CatalogId;
        }

        private static bool KeyPressed(Keyboard kb, int digit)
        {
            if (kb == null) return false;
            switch (digit)
            {
                case 1: return kb.digit1Key.wasPressedThisFrame;
                case 2: return kb.digit2Key.wasPressedThisFrame;
                case 3: return kb.digit3Key.wasPressedThisFrame;
                case 4: return kb.digit4Key.wasPressedThisFrame;
                case 5: return kb.digit5Key.wasPressedThisFrame;
                case 6: return kb.digit6Key.wasPressedThisFrame;
                default: return false;
            }
        }

        // ---------- хват предмета ----------

        private void OnObjectViewBuilt(GameObject root, PlacedObject o)
        {
            string id = o.Id;
            Transform visual = root.transform;

            // Хват: двигаем, не вращаем (поворот делается кнопкой A или клавишей 1, шаг 15°).
            XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.trackRotation = false;
            grab.useDynamicAttach = true;

            grab.selectEntered.AddListener(args => OnGrabbed(id));
            grab.selectExited.AddListener(args => OnReleased(id, visual));
        }

        private void OnGrabbed(string objectId)
        {
            _grabCount++;
            _preview.RebuildHolds++;   // пока предмет в руке, сцена не перерисовывается
            _preview.Selection.Select(objectId);
            _preview.Status = "Выбран: " + NameOf(objectId) + ".";
        }

        private void OnReleased(string objectId, Transform visual)
        {
            try
            {
                Project p = _preview.Projects.Current;
                PlacedObject o = p == null ? null : p.Objects.Find(x => x.Id == objectId);
                if (o == null || visual == null || _preview.Tools == null || _preview.ViewRoot == null) return;

                Vector3 local = _preview.ViewRoot.InverseTransformPoint(visual.position);
                float dx = local.x - o.Position.X;
                float dz = local.z - o.Position.Z;
                if (dx * dx + dz * dz < 0.02f * 0.02f)
                {
                    // Предмет почти не сдвинули: считаем это просто выбором, в историю ничего не пишем.
                    _preview.Status = "Выбран: " + NameOf(objectId) + ".";
                    return;
                }

                Do(() => Report(_preview.Tools.MoveTo(objectId, new PlanPoint(local.x, local.z))));
            }
            finally
            {
                _grabCount = Math.Max(0, _grabCount - 1);
                _preview.RebuildHolds = Math.Max(0, _preview.RebuildHolds - 1);
                _preview.RequestRebuild();   // при отказе предмет вернётся на прежнее место
            }
        }

        // ---------- XR-риг ----------

        private void OnViewRebuilt()
        {
            if (_rigPlaced || xrOrigin == null) return;

            float minX, maxX, minZ, maxZ;
            if (!_preview.TryGetRoomBounds(out minX, out maxX, out minZ, out maxZ)) return;

            // Встаём в комнате чуть южнее центра, лицом к центру (по оси Z).
            float z = Mathf.Max(minZ + 0.5f, (minZ + maxZ) / 2f - 1f);
            Vector3 target = _preview.transform.TransformPoint(new Vector3((minX + maxX) / 2f, _preview.RoomElevation, z));

            // Контроллер персонажа на риге иногда игнорирует прямую смену позиции, поэтому на миг выключаем его.
            CharacterController cc = xrOrigin.GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (wasEnabled) cc.enabled = false;
            xrOrigin.position = target;
            xrOrigin.rotation = _preview.transform.rotation;
            if (wasEnabled) cc.enabled = true;

            _rigPlaced = true;
        }

        // ---------- ввод (кнопки Touch-контроллеров) ----------

        private void SetupInput()
        {
            _rotate = Bind("rotate", "<XRController>{RightHand}/{PrimaryButton}");        // A
            _duplicate = Bind("duplicate", "<XRController>{RightHand}/{SecondaryButton}"); // B
            _delete = Bind("delete", "<XRController>{RightHand}/{Primary2DAxisClick}");    // нажатие правого стика
            _undo = Bind("undo", "<XRController>{LeftHand}/{PrimaryButton}");              // X
            _redo = Bind("redo", "<XRController>{LeftHand}/{SecondaryButton}");            // Y
        }

        private static InputAction Bind(string name, string path)
        {
            var action = new InputAction(name: name, type: InputActionType.Button, binding: path);
            action.Enable();
            return action;
        }

        private static void DisposeAction(InputAction a) { if (a != null) { a.Disable(); a.Dispose(); } }

        // ---------- подсказка перед глазами ----------

        private void CreateHud()
        {
            if (_hud != null) return;
            Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            Transform parent = cam != null ? cam.transform : transform;

            var go = new GameObject("Подсказка");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -0.30f, 1.0f);
            go.transform.localScale = Vector3.one * hudScale;

            _hud = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hud.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            _hud.fontSize = 48;
            _hud.characterSize = 0.1f;
            _hud.anchor = TextAnchor.MiddleCenter;
            _hud.alignment = TextAlignment.Center;
            _hud.color = new Color(1f, 0.92f, 0.3f);
            UpdateHud();
        }

        private void UpdateHud()
        {
            if (_hud == null || _preview == null) return;
            _shownStatus = _preview.Status;
            _hud.text = _shownStatus
                + "\nA/1: повернуть   B/2: копия   клик стика/3: удалить   X/4: отмена   Y/5: повтор   6: выбрать следующий";
        }
    }
}
