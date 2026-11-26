// Наглядный предпросмотр проекта в Unity (нажать Play). Рисует комнату, стены, проёмы, объекты и светильники
// простыми цветными блоками и даёт кнопки, чтобы испытать команды, undo/redo и сохранение.
// Это инструмент проверки, не часть приложения. Работает в отдельной сцене, не трогая сцены команды.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XrInterior.ProjectData.UnityIntegration
{
    public class ProjectPreview : MonoBehaviour
    {
        public enum PreviewSource { NewTemplateRoom, ControlProject }
        private enum DemoSpot { Random, NearSouthWall, NearNorthWall, NearEastWall, Center, OnFakeTable }

        [Header("Что показать")]
        public PreviewSource source = PreviewSource.NewTemplateRoom;

        [Header("Шаблон помещения (для NewTemplateRoom), размеры в метрах")]
        public RoomTemplate template = RoomTemplate.Rectangular;
        public float width = 4f;
        public float depth = 3f;
        public float length = 8f;
        public float cutWidth = 2.5f;
        public float cutDepth = 2f;

        // Условные цвета материалов: настоящие текстуры сделает C, здесь важно лишь видеть, что материал сменился.
        private static readonly Dictionary<string, Color> MaterialColors = new Dictionary<string, Color>
        {
            { "mat_wood_oak_01", new Color(0.78f, 0.58f, 0.32f) },
            { "mat_wood_walnut_01", new Color(0.36f, 0.22f, 0.12f) },
            { "mat_laminate_grey_01", new Color(0.62f, 0.62f, 0.60f) },
            { "mat_tile_white_01", new Color(0.93f, 0.95f, 0.96f) },
            { "mat_tile_dark_01", new Color(0.18f, 0.20f, 0.24f) },
            { "mat_carpet_grey_01", new Color(0.45f, 0.45f, 0.50f) },
            { "mat_concrete_01", new Color(0.55f, 0.56f, 0.57f) },
            { "mat_paint_white_01", new Color(0.97f, 0.97f, 0.95f) },
            { "mat_paint_beige_01", new Color(0.90f, 0.82f, 0.66f) },
            { "mat_paint_grey_01", new Color(0.66f, 0.68f, 0.70f) },
            { "mat_paint_blue_01", new Color(0.30f, 0.52f, 0.85f) },
            { "mat_brick_01", new Color(0.70f, 0.30f, 0.22f) },
            { "mat_plaster_01", new Color(0.88f, 0.86f, 0.82f) },
            { "mat_ceiling_white_01", new Color(1f, 1f, 1f) },
            { "mat_fabric_grey_01", new Color(0.55f, 0.58f, 0.62f) },
            { "mat_leather_black_01", new Color(0.10f, 0.09f, 0.09f) },
            { "mat_metal_01", new Color(0.75f, 0.77f, 0.80f) },
            { "mat_glass_01", new Color(0.65f, 0.85f, 0.95f) },
            { "mat_wood_door_01", new Color(0.50f, 0.34f, 0.18f) }
        };

        private static Color MaterialColor(string materialId)
        {
            Color c;
            if (materialId != null && MaterialColors.TryGetValue(materialId, out c)) return c;
            return Color.magenta;   // материал, которого нет в таблице цветов
        }

        private string _storageDir;
        private ProjectStorage _storage;
        private ProjectService _projects;
        private CommandService _commands;
        private CatalogService _catalog;
        private EditorTools _tools;
        private readonly EditorSelection _selection = new EditorSelection();
        private bool _fakeReality;
        private readonly FakeReality _reality = new FakeReality();
        private GameObject _viewRoot;
        private bool _framed;

        // ---- Доступ для надстроек (например, XR-слоя XrEditorBridge). Здесь нет зависимостей от XR-пакетов. ----

        /// <summary>false - не двигать камеру сцены (нужно, когда в сцене есть XR-риг).</summary>
        public bool frameCamera = true;

        /// <summary>Пока значение больше нуля, сцена не перестраивается (например, предмет в руке).</summary>
        public int RebuildHolds { get; set; }

        public ProjectService Projects { get { return _projects; } }
        public CommandService Commands { get { return _commands; } }
        public CatalogService Catalog { get { return _catalog; } }
        public EditorTools Tools { get { return _tools; } }
        public EditorSelection Selection { get { return _selection; } }
        public Transform ViewRoot { get { return _viewRoot != null ? _viewRoot.transform : null; } }
        public string Status { get { return _status; } set { _status = value; } }

        /// <summary>Вызывается для каждого нарисованного предмета: корень стоит в точке привязки, форма и коллайдер внутри.</summary>
        public event Action<GameObject, PlacedObject> ObjectViewBuilt;

        /// <summary>Вызывается после каждой перерисовки сцены.</summary>
        public event Action ViewRebuilt;

        private bool _dirty;
        private readonly Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>();

        /// <summary>Просит перерисовать сцену при первой возможности (когда нет удержаний).</summary>
        public void RequestRebuild() { _dirty = true; }

        public float RoomElevation
        {
            get
            {
                Project p = _projects != null ? _projects.Current : null;
                return p != null && p.Rooms.Count > 0 ? p.Rooms[0].Elevation : 0f;
            }
        }

        public bool TryGetRoomBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = 0f; maxX = 4f; minZ = 0f; maxZ = 3f;
            Project p = _projects != null ? _projects.Current : null;
            if (p == null || p.Rooms.Count == 0) return false;
            GetBounds(p, out minX, out maxX, out minZ, out maxZ);
            return true;
        }

        private void Awake()
        {
            _selection.SelectionChanged += OnSelectionChanged;
        }

        private void Update()
        {
            if (_dirty && RebuildHolds <= 0 && _projects != null && _projects.Current != null)
            {
                _dirty = false;
                Rebuild();
            }
        }

        private void OnSelectionChanged(string id)
        {
            foreach (KeyValuePair<string, GameObject> m in _markers)
                if (m.Value != null) m.Value.SetActive(m.Key == id);
        }
        private string _status = "";
        private GUIStyle _label;
        private GUIStyle _button;
        private GUIStyle _statusLabel;
        private Vector2 _scroll;

        // Имитация результата сканирования: один "реальный стол". Настоящую проверку даст сервис A.
        private sealed class FakeReality : IRealityCollisionChecker
        {
            public static readonly Vector3 Center = new Vector3(2.5f, 0.4f, 1.5f);
            public static readonly Vector3 Size = new Vector3(1.2f, 0.8f, 0.7f);

            public bool IsBlocked(float centerX, float centerY, float centerZ,
                float sizeX, float sizeY, float sizeZ, float yawDegrees, out string reason)
            {
                float yaw = yawDegrees * Mathf.Deg2Rad;
                float hx = Mathf.Abs(Mathf.Cos(yaw)) * sizeX / 2f + Mathf.Abs(Mathf.Sin(yaw)) * sizeZ / 2f;
                float hz = Mathf.Abs(Mathf.Sin(yaw)) * sizeX / 2f + Mathf.Abs(Mathf.Cos(yaw)) * sizeZ / 2f;
                bool overlap = Mathf.Abs(centerX - Center.x) < hx + Size.x / 2f
                               && Mathf.Abs(centerZ - Center.z) < hz + Size.z / 2f
                               && Mathf.Abs(centerY - Center.y) < sizeY / 2f + Size.y / 2f;
                reason = overlap ? "Пересекается с реальным столом (имитация сканирования)." : null;
                return overlap;
            }
        }

        private void Start()
        {
            try { BuildProject(); }
            catch (Exception ex)
            {
                _status = "Ошибка: " + ex.Message;
                Debug.LogError(ex);
            }
        }

        private void OnDestroy()
        {
            try { if (!string.IsNullOrEmpty(_storageDir) && Directory.Exists(_storageDir)) Directory.Delete(_storageDir, true); }
            catch (IOException) { }
        }

        // ---------- проект ----------

        private void BuildProject()
        {
            try
            {
                _catalog = CatalogService.FromFile(
                    Path.Combine(Application.dataPath, "_Project/ProjectData/Samples/catalog_sample.json"));
            }
            catch (Exception ex)
            {
                _catalog = null;
                Debug.LogWarning("Каталог не загружен: " + ex.Message);
            }

            _storageDir = Path.Combine(Path.GetTempPath(), "xri_preview_" + Guid.NewGuid().ToString("N"));
            _storage = new ProjectStorage(_storageDir);
            _projects = new ProjectService(_storage);

            if (source == PreviewSource.ControlProject)
            {
                string path = Path.Combine(Application.dataPath, "_Project/ProjectData/Samples/control_project.json");
                if (!File.Exists(path)) { _status = "Не найден файл: " + path; return; }
                _projects.Import(path);
                _status = "Открыт контрольный проект из файла.";
            }
            else
            {
                var spec = new RoomSpec { Name = "Комната", Template = template, Params = ParamsFor(template) };
                try
                {
                    TemplateProjectBuilder.CreateFromTemplate(_projects, "Предпросмотр", spec);
                    _status = "Создан новый проект из шаблона " + template + ".";
                }
                catch (ArgumentException ex)
                {
                    _status = ex.Message;
                    return;
                }
            }

            AttachCommands();
            Rebuild();
        }

        private void AttachCommands()
        {
            _commands = new CommandService(_projects);
            _selection.Clear();
            _tools = _catalog != null ? new EditorTools(_projects, _commands, _catalog) : null;
            _projects.ProjectChanged += Rebuild;
        }

        private Dictionary<string, float> ParamsFor(RoomTemplate t)
        {
            var p = new Dictionary<string, float>();
            switch (t)
            {
                case RoomTemplate.Corridor:
                    p[TemplateParam.Length] = length;
                    p[TemplateParam.Width] = width;
                    break;
                case RoomTemplate.LShaped:
                    p[TemplateParam.Width] = width;
                    p[TemplateParam.Depth] = depth;
                    p[TemplateParam.CutWidth] = cutWidth;
                    p[TemplateParam.CutDepth] = cutDepth;
                    break;
                default:
                    p[TemplateParam.Width] = width;
                    p[TemplateParam.Depth] = depth;
                    break;
            }
            return p;
        }

        // ---------- рисование ----------

        private void Rebuild()
        {
            if (RebuildHolds > 0) { _dirty = true; return; }
            _markers.Clear();
            if (_viewRoot != null) Destroy(_viewRoot);
            _viewRoot = new GameObject("Предпросмотр");
            _viewRoot.transform.SetParent(transform, false);

            Project p = _projects.Current;
            if (p == null) return;
            _selection.Refresh(p);

            var rooms = new Dictionary<string, Room>();
            foreach (Room r in p.Rooms) rooms[r.Id] = r;
            var walls = new Dictionary<string, Wall>();
            foreach (Wall w in p.Walls) walls[w.Id] = w;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (Room r in p.Rooms)
            {
                DrawFloor(r);
                foreach (PlanPoint pt in r.Boundary)
                {
                    minX = Mathf.Min(minX, pt.X); maxX = Mathf.Max(maxX, pt.X);
                    minZ = Mathf.Min(minZ, pt.Z); maxZ = Mathf.Max(maxZ, pt.Z);
                }
            }
            foreach (Wall w in p.Walls) DrawWall(w, BaseY(w, rooms));
            foreach (Opening o in p.Openings) DrawOpening(o, walls, rooms);
            foreach (PlacedObject o in p.Objects) DrawObject(o);
            foreach (ProjectLight l in p.Lights) DrawLight(l);
            if (_fakeReality)
                Box("Реальный стол (имитация)", FakeReality.Center, FakeReality.Size, Quaternion.identity,
                    new Color(0.5f, 0.5f, 0.55f));

            if (frameCamera && !_framed && p.Rooms.Count > 0) FrameCamera(minX, maxX, minZ, maxZ);
            if (ViewRebuilt != null) ViewRebuilt();
        }

        private static float BaseY(Wall w, Dictionary<string, Room> rooms)
        {
            Room room;
            if (w.RoomId != null && rooms.TryGetValue(w.RoomId, out room)) return room.Elevation;
            return 0f;
        }

        private void DrawWall(Wall w, float baseY)
        {
            Vector3 a = new Vector3(w.A.X, 0f, w.A.Z);
            Vector3 b = new Vector3(w.B.X, 0f, w.B.Z);
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) return;

            float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
            Vector3 center = (a + b) / 2f + Vector3.up * (baseY + w.Height / 2f);
            Color color = w.State == ElementState.Existing ? new Color(0.85f, 0.80f, 0.70f) : new Color(0.55f, 0.85f, 0.55f);
            string wallMaterial = MaterialRules.GetMaterialId(_projects.Current, w.Id, MaterialSlots.Surface);
            if (wallMaterial != null) color = MaterialColor(wallMaterial);
            Box("Стена " + w.Id, center, new Vector3(len + w.Thickness, w.Height, w.Thickness), Quaternion.Euler(0f, yaw, 0f), color);
        }

        private void DrawOpening(Opening o, Dictionary<string, Wall> walls, Dictionary<string, Room> rooms)
        {
            Wall w;
            if (o.WallId == null || !walls.TryGetValue(o.WallId, out w)) return;

            Vector3 a = new Vector3(w.A.X, 0f, w.A.Z);
            Vector3 b = new Vector3(w.B.X, 0f, w.B.Z);
            Vector3 d = b - a;
            if (d.magnitude < 0.001f) return;
            Vector3 dir = d.normalized;

            float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
            Vector3 pos = a + dir * (o.Offset + o.Width / 2f);
            pos.y = BaseY(w, rooms) + o.SillHeight + o.Height / 2f;
            Color color = o.Type == OpeningType.Door ? new Color(0.45f, 0.30f, 0.15f) : new Color(0.5f, 0.8f, 1f);
            Box((o.Type == OpeningType.Door ? "Дверь " : "Окно ") + o.Id, pos,
                new Vector3(o.Width, o.Height, w.Thickness + 0.04f), Quaternion.Euler(0f, yaw, 0f), color);
        }

        private void DrawObject(PlacedObject o)
        {
            Quaternion rot = Quaternion.Euler(o.Rotation.X, o.Rotation.Y, o.Rotation.Z);
            Vector3 size = new Vector3(0.8f, 0.8f, 0.8f);
            Vector3 localCenter = new Vector3(0f, 0.4f, 0f);
            string label = o.CatalogId;

            CatalogItem item = _catalog != null ? _catalog.GetItem(o.CatalogId) : null;
            if (item != null)
            {
                size = new Vector3(item.Size.X, item.Size.Y, item.Size.Z);
                // Точка привязки: пол - центр основания; стена - центр задней стороны; потолок - центр верха.
                if (item.Placement == PlacementRule.Wall) localCenter = new Vector3(0f, 0f, item.Size.Z / 2f);
                else if (item.Placement == PlacementRule.Ceiling) localCenter = new Vector3(0f, -item.Size.Y / 2f, 0f);
                else localCenter = new Vector3(0f, item.Size.Y / 2f, 0f);
                label = item.Name;
            }

            Vector3 pos = new Vector3(o.Position.X, o.Position.Y, o.Position.Z) + rot * localCenter;
            float hue = Mathf.Abs((o.CatalogId ?? "").GetHashCode() % 100) / 100f;

            // Предметы, которые пересекаются со стеной, другим предметом или реальным объектом, красные.
            bool collides = false;
            if (_catalog != null && _projects.Current != null)
            {
                Project proj = _projects.Current;
                collides = CollisionService.CheckObject(proj, o, _catalog,
                    _fakeReality ? _reality : null, FloorOf(proj)).HasCollisions;
            }
            Color baseColor = Color.HSVToRGB(hue, 0.55f, 0.9f);
            string objectMaterial = MaterialRules.GetMaterialId(_projects.Current, o.Id, MaterialSlots.Surface);
            if (objectMaterial != null) baseColor = MaterialColor(objectMaterial);
            Color color = collides ? new Color(0.95f, 0.2f, 0.2f) : baseColor;

            // Корень стоит ровно в точке привязки предмета (к нему крепится хват в VR), форма смещена внутри.
            var root = new GameObject("Объект " + label + (collides ? " [пересечение]" : "") + " (" + o.Id + ")");
            root.transform.SetParent(_viewRoot.transform, false);
            root.transform.localPosition = new Vector3(o.Position.X, o.Position.Y, o.Position.Z);
            root.transform.localRotation = rot;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);   // коллайдер оставляем: за него берётся хват
            body.name = "Форма";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = localCenter;
            body.transform.localScale = size;
            body.GetComponent<Renderer>().material.color = color;

            // Жёлтый маркер над выбранным предметом (едет вместе с ним).
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Выбран";
            DestroyImmediate(marker.GetComponent<Collider>());   // сразу, чтобы хват не увидел лишний коллайдер
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = localCenter + Vector3.up * (size.y / 2f + 0.25f);
            marker.transform.localScale = Vector3.one * 0.15f;
            marker.GetComponent<Renderer>().material.color = new Color(1f, 0.9f, 0.1f);
            marker.SetActive(o.Id == _selection.SelectedId);
            _markers[o.Id] = marker;

            if (ObjectViewBuilt != null) ObjectViewBuilt(root, o);
        }

        private void DrawLight(ProjectLight l)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Светильник " + l.Id;
            go.transform.SetParent(_viewRoot.transform, false);
            go.transform.position = new Vector3(l.Position.X, l.Position.Y, l.Position.Z);
            go.transform.localScale = Vector3.one * 0.25f;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = l.Enabled ? new Color(1f, 0.9f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
        }

        private void DrawFloor(Room r)
        {
            var pts = new List<Vector2>();
            foreach (PlanPoint pt in r.Boundary) pts.Add(new Vector2(pt.X, pt.Z));
            int[] tris = Triangulate(pts);
            if (tris.Length == 0) return;

            int n = pts.Count;
            var verts = new Vector3[n * 2];
            var normals = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts[i] = new Vector3(pts[i].x, r.Elevation, pts[i].y);
                verts[i + n] = verts[i];
                normals[i] = Vector3.up;
                normals[i + n] = Vector3.down;
            }

            // Пол рисуем с обеих сторон, чтобы он был виден при любом направлении взгляда.
            var indices = new int[tris.Length * 2];
            for (int t = 0; t < tris.Length; t += 3)
            {
                indices[t] = tris[t];
                indices[t + 1] = tris[t + 2];
                indices[t + 2] = tris[t + 1];
                indices[tris.Length + t] = tris[t] + n;
                indices[tris.Length + t + 1] = tris[t + 1] + n;
                indices[tris.Length + t + 2] = tris[t + 2] + n;
            }

            var mesh = new Mesh { name = "Пол" };
            mesh.vertices = verts;
            mesh.triangles = indices;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Пол " + r.Name;
            go.transform.SetParent(_viewRoot.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;   // твёрдый пол: на нём стоит XR-риг
            string floorMaterial = MaterialRules.GetMaterialId(_projects.Current, r.Id, MaterialSlots.Floor);
            go.GetComponent<Renderer>().material.color =
                floorMaterial != null ? MaterialColor(floorMaterial) : new Color(0.40f, 0.40f, 0.46f);
        }

        private GameObject Box(string objName, Vector3 center, Vector3 size, Quaternion rot, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objName;
            go.transform.SetParent(_viewRoot.transform, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.transform.localScale = size;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }

        private void FrameCamera(float minX, float maxX, float minZ, float maxZ)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 center = new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f);
            float size = Mathf.Max(maxX - minX, maxZ - minZ, 2f);
            cam.transform.position = center + new Vector3(0f, size * 1.1f, -size * 0.9f);
            cam.transform.LookAt(center);
            _framed = true;
        }

        // Разбиение многоугольника на треугольники (метод "отсечения ушей").
        private static int[] Triangulate(List<Vector2> pts)
        {
            var result = new List<int>();
            int n = pts.Count;
            if (n < 3) return result.ToArray();

            var idx = new List<int>();
            for (int i = 0; i < n; i++) idx.Add(i);

            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f) idx.Reverse();

            int guard = 0;
            while (idx.Count > 3 && guard++ < 1000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[i0], b = pts[i1], c = pts[i2];
                    if (Cross(b - a, c - b) <= 0f) continue;

                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (PointInTriangle(pts[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;

                    result.Add(i0); result.Add(i1); result.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result.ToArray();
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        // ---------- кнопки ----------

        private void OnGUI()
        {
            EnsureStyles();
            // Панель подстраивается под размер окна Game.
            float scale = Mathf.Clamp(Screen.height / 720f, 0.8f, 1.6f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(8f, 8f, 400f, Screen.height / scale - 16f), GUI.skin.box);

            if (_projects == null || _projects.Current == null)
            {
                GUILayout.Label(_status, _label);
                GUILayout.EndArea();
                return;
            }

            Project p = _projects.Current;
            GUILayout.Label("Проект: " + p.Name + " (источник: " + p.Source + ")", _label);
            GUILayout.Label("Помещений: " + p.Rooms.Count + "   Стен: " + p.Walls.Count + "   Объектов: " + p.Objects.Count, _label);
            GUILayout.Label("Команд в истории: " + _commands.UndoCount, _label);
            GUILayout.Label("▶ " + _status, _statusLabel);   // статус всегда виден над кнопками
            if (p.Rooms.Count > 0)
            {
                MaterialRow("Пол", p.Rooms[0].Id, MaterialSlots.Floor);
                MaterialRow("Потолок", p.Rooms[0].Id, MaterialSlots.Ceiling);
            }
            if (p.Walls.Count > 0) MaterialRow("Последняя стена", p.Walls[p.Walls.Count - 1].Id, MaterialSlots.Surface);
            if (p.Objects.Count > 0) MaterialRow("Последний предмет", p.Objects[p.Objects.Count - 1].Id, MaterialSlots.Surface);
            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (GUILayout.Button("Добавить стул", _button)) Run(AddChair);

            GUI.enabled = p.Objects.Count > 0;
            if (GUILayout.Button("Сдвинуть последний объект", _button)) Run(MoveLast);
            if (GUILayout.Button("Удалить последний объект", _button)) Run(RemoveLast);
            GUI.enabled = true;

            GUILayout.Space(6f);
            if (GUILayout.Button("Добавить перегородку", _button)) Run(AddPartition);
            GUI.enabled = p.Walls.Count > 0;
            if (GUILayout.Button("Добавить окно в последнюю стену", _button)) Run(AddWindowToLastWall);
            if (GUILayout.Button("Добавить дверь в последнюю стену", _button)) Run(AddDoorToLastWall);
            if (GUILayout.Button("Сдвинуть последнюю стену", _button)) Run(MoveLastWall);
            if (GUILayout.Button("Удалить последнюю стену", _button)) Run(RemoveLastWall);
            GUI.enabled = true;

            GUILayout.Space(6f);
            GUILayout.Label("Из каталога (с правилами размещения):", _label);
            GUI.enabled = _catalog != null;
            if (GUILayout.Button("Стул на пол", _button)) Run(() => PlaceDemo("chair_wood_01", DemoSpot.Random));
            if (GUILayout.Button("Шкаф (прилипает к южной стене)", _button)) Run(() => PlaceDemo("wardrobe_01", DemoSpot.NearSouthWall));
            if (GUILayout.Button("Диван (прилипает к северной стене)", _button)) Run(() => PlaceDemo("sofa_3seat_01", DemoSpot.NearNorthWall));
            if (GUILayout.Button("Картина на восточную стену", _button)) Run(() => PlaceDemo("picture_wall_01", DemoSpot.NearEastWall));
            if (GUILayout.Button("Потолочный светильник", _button)) Run(() => PlaceDemo("lamp_ceiling_01", DemoSpot.Center));
            if (GUILayout.Button("Картина в центр (не получится: нет стены)", _button)) Run(() => PlaceDemo("picture_wall_01", DemoSpot.Center));
            if (GUILayout.Button("Стул в центр (нажми дважды: будет пересечение)", _button)) Run(() => PlaceDemo("chair_wood_01", DemoSpot.Center));

            GUILayout.Space(6f);
            GUILayout.Label("Реальная геометрия (имитация скана):", _label);
            if (GUILayout.Button(_fakeReality ? "Убрать «реальный стол»" : "Показать «реальный стол»", _button))
                Run(() => { _fakeReality = !_fakeReality; Rebuild(); });
            if (GUILayout.Button("Стул прямо на «реальный стол» (будет блокировка)", _button))
                Run(() => PlaceDemo("chair_wood_01", DemoSpot.OnFakeTable));
            GUI.enabled = true;

            GUILayout.Space(8f);

            GUILayout.Label("Редактирование выбранного (B-07):", _label);
            GUI.enabled = _tools != null && p.Objects.Count > 0;
            if (GUILayout.Button("Выбрать следующий предмет (жёлтый маркер)", _button))
                Run(SelectNextObject);
            GUI.enabled = _tools != null && _selection.HasSelection;
            if (GUILayout.Button("Повернуть на 15°", _button))
                Run(() => Report(_tools.RotateBy(_selection.SelectedId, 15f)));
            if (GUILayout.Button("Сдвинуть на 0,25 м вправо", _button))
                Run(() => MoveSelected(0.25f, 0f));
            if (GUILayout.Button("Сдвинуть на 0,25 м вперёд", _button))
                Run(() => MoveSelected(0f, 0.25f));
            if (GUILayout.Button("Дублировать", _button))
                Run(DuplicateSelected);
            if (GUILayout.Button("Удалить выбранный", _button))
                Run(() => Report(_tools.Delete(_selection.SelectedId)));
            GUI.enabled = true;

            GUILayout.Space(6f);
            GUILayout.Label("Материалы (B-12):", _label);
            GUI.enabled = _catalog != null && p.Rooms.Count > 0;
            if (GUILayout.Button("Пол: дуб", _button))
                Run(() => SetMaterial(p.Rooms[0].Id, MaterialSlots.Floor, "mat_wood_oak_01", "Пол: дуб."));
            if (GUILayout.Button("Пол: тёмная плитка", _button))
                Run(() => SetMaterial(p.Rooms[0].Id, MaterialSlots.Floor, "mat_tile_dark_01", "Пол: тёмная плитка."));
            if (GUILayout.Button("Потолок: белый", _button))
                Run(() => SetMaterial(p.Rooms[0].Id, MaterialSlots.Ceiling, "mat_ceiling_white_01", "Потолок: белый."));
            if (GUILayout.Button("Все стены комнаты: синяя краска", _button))
                Run(() => SetRoomWalls("mat_paint_blue_01", "Все стены комнаты: синяя краска."));
            if (GUILayout.Button("Все стены комнаты: кирпич", _button))
                Run(() => SetRoomWalls("mat_brick_01", "Все стены комнаты: кирпич."));
            if (GUILayout.Button("Сбросить пол и стены к умолчанию", _button))
                Run(ResetFloorAndWalls);
            GUI.enabled = _catalog != null && p.Walls.Count > 0;
            if (GUILayout.Button("Последняя стена: бетон", _button))
                Run(() => SetMaterial(p.Walls[p.Walls.Count - 1].Id, MaterialSlots.Surface, "mat_concrete_01", "Последняя стена: бетон."));
            if (GUILayout.Button("Ковёр на стену (будет отказ)", _button))
                Run(() => SetMaterial(p.Walls[p.Walls.Count - 1].Id, MaterialSlots.Surface, "mat_carpet_grey_01", "Ковёр на стену."));
            GUI.enabled = _catalog != null && p.Objects.Count > 0;
            if (GUILayout.Button("Последний предмет: следующий допустимый материал", _button))
                Run(CycleLastObjectMaterial);
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUI.enabled = _commands.CanUndo;
            if (GUILayout.Button("Отменить: " + _commands.NextUndoName, _button)) Run(_commands.Undo);
            GUI.enabled = _commands.CanRedo;
            if (GUILayout.Button("Повторить: " + _commands.NextRedoName, _button)) Run(_commands.Redo);
            GUI.enabled = true;

            GUILayout.Space(8f);
            if (GUILayout.Button("Сохранить и открыть заново", _button)) Run(ReopenFromDisk);

            GUILayout.Space(8f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _label.normal.textColor = Color.white;
            _statusLabel = new GUIStyle(_label) { fontStyle = FontStyle.Bold };
            _statusLabel.normal.textColor = new Color(1f, 0.9f, 0.3f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 28f };
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                _status = "Ошибка: " + ex.Message;
                Debug.LogError(ex);
            }
        }

        private void AddChair()
        {
            Project p = _projects.Current;
            float minX = 0f, maxX = 4f, minZ = 0f, maxZ = 3f;
            if (p.Rooms.Count > 0)
            {
                minX = float.MaxValue; maxX = float.MinValue; minZ = float.MaxValue; maxZ = float.MinValue;
                foreach (PlanPoint pt in p.Rooms[0].Boundary)
                {
                    minX = Mathf.Min(minX, pt.X); maxX = Mathf.Max(maxX, pt.X);
                    minZ = Mathf.Min(minZ, pt.Z); maxZ = Mathf.Max(maxZ, pt.Z);
                }
            }

            var chair = new PlacedObject
            {
                Id = IdGenerator.New("obj"),
                CatalogId = "chair_demo",
                Position = new Vec3(UnityEngine.Random.Range(minX + 0.5f, maxX - 0.5f), 0f,
                                    UnityEngine.Random.Range(minZ + 0.5f, maxZ - 0.5f)),
                Rotation = new Vec3(0f, UnityEngine.Random.Range(0, 24) * 15f, 0f)
            };
            _commands.Execute(new AddObjectCommand(chair, "Добавить стул"));
            _status = "Добавлен стул.";
        }

        private void MoveLast()
        {
            Project p = _projects.Current;
            PlacedObject o = p.Objects[p.Objects.Count - 1];
            _commands.Execute(new SetObjectTransformCommand(o.Id,
                new Vec3(o.Position.X + 0.5f, o.Position.Y, o.Position.Z), o.Rotation, o.Scale, "Сдвинуть объект"));
            _status = "Объект сдвинут на 0,5 м.";
        }

        private void RemoveLast()
        {
            Project p = _projects.Current;
            _commands.Execute(new RemoveObjectCommand(p.Objects[p.Objects.Count - 1].Id, "Удалить объект"));
            _status = "Объект удалён.";
        }

        private void PlaceDemo(string catalogId, DemoSpot spot)
        {
            CatalogItem item = _catalog.GetItem(catalogId);
            if (item == null) { _status = "В каталоге нет предмета " + catalogId + "."; return; }

            Project p = _projects.Current;
            float minX, maxX, minZ, maxZ;
            GetBounds(p, out minX, out maxX, out minZ, out maxZ);
            int floor = p.Rooms.Count > 0 ? p.Rooms[0].Floor : 1;

            float rx = UnityEngine.Random.Range(minX + 1f, Mathf.Max(minX + 1f, maxX - 1f));
            float rz = UnityEngine.Random.Range(minZ + 0.5f, Mathf.Max(minZ + 0.5f, maxZ - 0.5f));
            PlanPoint target;
            switch (spot)
            {
                case DemoSpot.NearSouthWall: target = new PlanPoint(rx, minZ + 0.6f); break;
                case DemoSpot.NearNorthWall: target = new PlanPoint(rx, maxZ - 0.6f); break;
                case DemoSpot.NearEastWall: target = new PlanPoint(maxX - 0.4f, rz); break;
                case DemoSpot.Center: target = new PlanPoint((minX + maxX) / 2f, (minZ + maxZ) / 2f); break;
                case DemoSpot.OnFakeTable: target = new PlanPoint(FakeReality.Center.x, FakeReality.Center.z); break;
                default: target = new PlanPoint(rx, rz); break;
            }

            float yaw = UnityEngine.Random.Range(0, 24) * 15f;
            // Привязка к сетке 0,05 м и повороту 15°
            PlacementResult r = PlacementService.Place(p, item, target, yaw, floor, SnapSettings.Default);
            if (!r.IsValid)
            {
                _status = "Не удалось поставить «" + item.Name + "»: " + r.Reason;
                return;
            }

            CollisionReport report = CollisionService.Check(p, item, r, _catalog, _fakeReality ? _reality : null, floor);
            if (report.HasBlocking)
            {
                _status = "Заблокировано: «" + item.Name + "». " + report.BlockingSummary;
                return;
            }

            _commands.Execute(new AddObjectCommand(PlacementService.CreateObject(item, r), "Поставить: " + item.Name));
            _status = "Поставлено: " + item.Name + (r.WallId != null ? " (у стены)." : ".");
            if (report.HasCollisions) _status += " Предупреждение: " + report.Summary;
        }

        private static int FloorOf(Project p)
        {
            return p.Rooms.Count > 0 ? p.Rooms[0].Floor : 1;
        }

        private void GetBounds(Project p, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = 0f; maxX = 4f; minZ = 0f; maxZ = 3f;
            if (p.Rooms.Count == 0) return;
            minX = float.MaxValue; maxX = float.MinValue; minZ = float.MaxValue; maxZ = float.MinValue;
            foreach (PlanPoint pt in p.Rooms[0].Boundary)
            {
                minX = Mathf.Min(minX, pt.X); maxX = Mathf.Max(maxX, pt.X);
                minZ = Mathf.Min(minZ, pt.Z); maxZ = Mathf.Max(maxZ, pt.Z);
            }
        }

        private void AddPartition()
        {
            Project p = _projects.Current;
            float minX, maxX, minZ, maxZ;
            GetBounds(p, out minX, out maxX, out minZ, out maxZ);

            float x = UnityEngine.Random.Range(minX + 0.5f, maxX - 0.5f);
            string roomId = p.Rooms.Count > 0 ? p.Rooms[0].Id : null;
            float height = p.Rooms.Count > 0 ? p.Rooms[0].Height : 2.7f;

            _commands.Execute(new AddWallCommand(new PlanPoint(x, minZ), new PlanPoint(x, maxZ),
                height, 0.1f, roomId, ElementState.Proposed, "Добавить перегородку"));
            _status = "Добавлена перегородка (зелёная).";
        }

        private void AddWindowToLastWall()
        {
            Project p = _projects.Current;
            Wall w = p.Walls[p.Walls.Count - 1];
            OpeningSize size = OpeningPresets.Window;
            _commands.Execute(new AddOpeningCommand(w.Id, OpeningType.Window, 0.05f,
                size.Width, size.Height, size.SillHeight, "mat_glass_01"));
            _status = "Добавлено окно в начале последней стены.";
        }

        private void AddDoorToLastWall()
        {
            Project p = _projects.Current;
            Wall w = p.Walls[p.Walls.Count - 1];
            OpeningSize size = OpeningPresets.Door;
            float offset = Mathf.Max(0f, WallRules.Length(w) - size.Width - 0.05f);
            _commands.Execute(new AddOpeningCommand(w.Id, OpeningType.Door, offset,
                size.Width, size.Height, size.SillHeight, "mat_door_01"));
            _status = "Добавлена дверь в конце последней стены.";
        }

        private void MoveLastWall()
        {
            Project p = _projects.Current;
            Wall w = p.Walls[p.Walls.Count - 1];
            _commands.Execute(new MoveWallCommand(w.Id,
                new PlanPoint(w.A.X + 0.5f, w.A.Z), new PlanPoint(w.B.X + 0.5f, w.B.Z), "Сдвинуть стену"));
            _status = "Стена сдвинута на 0,5 м.";
        }

        private void RemoveLastWall()
        {
            Project p = _projects.Current;
            _commands.Execute(new RemoveWallCommand(p.Walls[p.Walls.Count - 1].Id, "Удалить стену"));
            _status = "Стена удалена вместе с её окнами и дверями.";
        }

        // ---------- редактирование выбранного (B-07) ----------

        public void ApplyReality() { if (_tools != null) _tools.Reality = _fakeReality ? _reality : null; }

        private void Report(ToolResult r)
        {
            _status = r.Message + (r.Warning != null ? " Предупреждение: " + r.Warning : "");
        }

        private void SelectNextObject()
        {
            string id = _selection.SelectNext(_projects.Current);
            PlacedObject o = _projects.Current.Objects.Find(x => x.Id == id);
            CatalogItem item = (o != null && _catalog != null) ? _catalog.GetItem(o.CatalogId) : null;
            _status = "Выбран: " + (item != null ? item.Name : (o != null ? o.CatalogId : "ничего")) + " (" + id + ").";
            Rebuild();
        }

        private void MoveSelected(float dx, float dz)
        {
            ApplyReality();
            PlacedObject o = _projects.Current.Objects.Find(x => x.Id == _selection.SelectedId);
            if (o == null) return;
            Report(_tools.MoveTo(o.Id, new PlanPoint(o.Position.X + dx, o.Position.Z + dz)));
        }

        private void DuplicateSelected()
        {
            ApplyReality();
            ToolResult r = _tools.Duplicate(_selection.SelectedId);
            Report(r);
            if (r.Success) { _selection.Select(r.ObjectId); Rebuild(); }
        }

        // ---------- материалы (B-12) ----------

        private void MaterialRow(string title, string targetId, string slot)
        {
            string id = MaterialRules.GetMaterialId(_projects.Current, targetId, slot);
            MaterialInfo info = (id != null && _catalog != null) ? _catalog.GetMaterial(id) : null;
            string text = id == null ? "по умолчанию" : (info != null ? info.Name : id);

            GUILayout.BeginHorizontal();
            Rect swatch = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f), GUILayout.Height(18f));
            Color old = GUI.color;
            GUI.color = id == null ? new Color(0.5f, 0.5f, 0.5f) : MaterialColor(id);
            GUI.DrawTexture(swatch, Texture2D.whiteTexture);
            GUI.color = old;
            GUILayout.Label(title + ": " + text, _label);
            GUILayout.EndHorizontal();
        }

        private void SetMaterial(string targetId, string slot, string materialId, string okText)
        {
            _commands.Execute(new SetMaterialCommand(targetId, slot, materialId, _catalog));
            _status = okText;
        }

        private void SetRoomWalls(string materialId, string okText)
        {
            Project p = _projects.Current;
            _commands.Execute(MaterialCommands.ApplyToRoomWalls(p, p.Rooms[0].Id, materialId, _catalog));
            _status = okText;
        }

        private void ResetFloorAndWalls()
        {
            Project p = _projects.Current;
            var commands = new List<ICommand>();
            commands.Add(new SetMaterialCommand(p.Rooms[0].Id, MaterialSlots.Floor, null, _catalog));
            foreach (Wall w in p.Walls)
                if (w.RoomId == p.Rooms[0].Id)
                    commands.Add(new SetMaterialCommand(w.Id, MaterialSlots.Surface, null, _catalog));
            _commands.Execute(new CompositeCommand("Сбросить материалы", commands));
            _status = "Материалы пола и стен сброшены.";
        }

        private void CycleLastObjectMaterial()
        {
            Project p = _projects.Current;
            PlacedObject o = p.Objects[p.Objects.Count - 1];
            IReadOnlyList<MaterialInfo> allowed =
                MaterialRules.GetAllowedMaterials(p, _catalog, o.Id, MaterialSlots.Surface);
            if (allowed.Count == 0)
            {
                _status = "Для этого предмета материал менять нельзя (в каталоге нет допустимых). " +
                          "Попробуй предмет из блока «Из каталога», например стул или шкаф.";
                return;
            }

            string current = MaterialRules.GetMaterialId(p, o.Id, MaterialSlots.Surface);
            int index = -1;
            for (int i = 0; i < allowed.Count; i++)
                if (allowed[i].Id == current) index = i;
            MaterialInfo next = allowed[(index + 1) % allowed.Count];

            _commands.Execute(new SetMaterialCommand(o.Id, MaterialSlots.Surface, next.Id, _catalog));
            _status = "Материал предмета: " + next.Name + ".";
        }

        private void ReopenFromDisk()
        {
            _projects.Save();
            string id = _projects.Current.Id;

            _projects = new ProjectService(new ProjectStorage(_storageDir));
            _projects.Load(id);
            AttachCommands();
            Rebuild();

            _status = "Проект записан на диск и открыт заново. Объектов: " + _projects.Current.Objects.Count + ".";
        }
    }
}
