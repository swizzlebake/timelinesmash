using UnityEditor;
using UnityEngine;

namespace TimelineSmash.Editor
{
    /// <summary>
    /// An interactive lane×time view of a composition's direct segments. Drag a bar to move its start,
    /// drag its right edge to resize the duration (both snap to frames and are undoable). Lanes are rows;
    /// time runs left→right. Lane reassignment and other fields stay on the contributor inspector — this
    /// window is for visual arrangement.
    /// </summary>
    public class CinematicTimelineWindow : EditorWindow
    {
        static readonly Color[] s_OwnerColors =
        {
            new Color(0.26f, 0.59f, 0.98f), new Color(0.40f, 0.78f, 0.40f),
            new Color(0.95f, 0.61f, 0.27f), new Color(0.80f, 0.45f, 0.90f),
            new Color(0.95f, 0.40f, 0.45f), new Color(0.35f, 0.80f, 0.80f),
        };

        const float LabelW = 120f;
        const float RulerH = 18f;
        const float LaneH = 28f;
        const float RowPad = 3f;
        const float ResizeW = 6f;

        [SerializeField] CinematicComposition _comp;
        [SerializeField] float _pps = 40f;  // pixels per second (zoom)
        [SerializeField] bool _snap = true;
        Vector2 _scroll;

        SubTimelineSegment _dragSeg;
        ContributorSegmentSet _dragSet;
        int _dragMode;       // 1 = move, 2 = resize-right
        float _dragMouseX;
        double _dragStart, _dragDur;
        SubTimelineSegment _selected;

        [MenuItem("Window/TimelineSmash/Cinematic Timeline")]
        public static void Open()
        {
            var w = GetWindow<CinematicTimelineWindow>("Cinematic Timeline");
            w.minSize = new Vector2(460, 220);
            if (Selection.activeObject is CinematicComposition c)
                w._comp = c;
        }

        public static void Open(CinematicComposition comp)
        {
            var w = GetWindow<CinematicTimelineWindow>("Cinematic Timeline");
            w.minSize = new Vector2(460, 220);
            w._comp = comp;
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is CinematicComposition c && c != _comp)
            {
                _comp = c;
                Repaint();
            }
        }

        void OnGUI()
        {
            DrawToolbar();

            if (_comp == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a Cinematic Composition (or assign one above) to arrange its segments.",
                    MessageType.Info);
                return;
            }

            var model = CinematicTimelineModel.Build(_comp);
            double total = Mathf.Max(1f, (float)model.totalDuration);
            double fps = _comp.settings != null && _comp.settings.frameRate > 0 ? _comp.settings.frameRate : 30.0;

            float top = GUILayoutUtility.GetLastRect().yMax;
            var body = new Rect(0, top, position.width, position.height - top);

            float contentW = LabelW + (float)(total * _pps) + 60f;
            float contentH = RulerH + model.lanes.Count * LaneH + 8f;
            var view = new Rect(0, 0, Mathf.Max(contentW, body.width - 16f), Mathf.Max(contentH, body.height - 16f));

            _scroll = GUI.BeginScrollView(body, _scroll, view);
            DrawRuler(view, total);
            DrawLanes(model, view, fps);
            HandleDrag(fps);
            GUI.EndScrollView();

            if (Event.current.type == EventType.MouseUp)
                _dragSeg = null;
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _comp = (CinematicComposition)EditorGUILayout.ObjectField(
                    _comp, typeof(CinematicComposition), false, GUILayout.Width(220));

                GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(34));
                _pps = GUILayout.HorizontalSlider(_pps, 6f, 200f, GUILayout.Width(110));

                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(34)) && _comp != null)
                {
                    var m = CinematicTimelineModel.Build(_comp);
                    double total = Mathf.Max(1f, (float)m.totalDuration);
                    _pps = Mathf.Clamp((float)((position.width - LabelW - 60f) / total), 6f, 200f);
                }

                _snap = GUILayout.Toggle(_snap, "Snap", EditorStyles.toolbarButton, GUILayout.Width(46));

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_comp == null))
                    if (GUILayout.Button("Assemble", EditorStyles.toolbarButton, GUILayout.Width(70)))
                        CinematicAssembleService.Assemble(_comp, true);
            }
        }

        void DrawRuler(Rect view, double total)
        {
            EditorGUI.DrawRect(new Rect(0, 0, view.width, RulerH), new Color(0, 0, 0, 0.2f));
            int step = Mathf.Max(1, Mathf.RoundToInt(60f / _pps));
            for (int s = 0; s <= (int)total + step; s += step)
            {
                float x = LabelW + s * _pps;
                if (x > view.width) break;
                EditorGUI.DrawRect(new Rect(x, 0, 1, RulerH), new Color(1, 1, 1, 0.25f));
                GUI.Label(new Rect(x + 2, 0, 46, RulerH), s + "s", EditorStyles.miniLabel);
            }
        }

        void DrawLanes(CinematicTimelineModel model, Rect view, double fps)
        {
            var e = Event.current;
            for (int li = 0; li < model.lanes.Count; li++)
            {
                string lane = model.lanes[li];
                float rowY = RulerH + li * LaneH;

                EditorGUI.DrawRect(new Rect(LabelW, rowY, view.width - LabelW, LaneH - 1),
                    (li & 1) == 0 ? new Color(0, 0, 0, 0.06f) : new Color(0, 0, 0, 0.12f));
                GUI.Label(new Rect(4, rowY + 4, LabelW - 8, LaneH), lane, EditorStyles.miniBoldLabel);

                foreach (var item in model.items)
                {
                    if (item.lane != lane)
                        continue;

                    float x = LabelW + (float)(item.segment.start * _pps);
                    float w = Mathf.Max(4f, (float)(item.segment.duration * _pps));
                    var rect = new Rect(x, rowY + RowPad, w, LaneH - 2 * RowPad);

                    var col = OwnerColor(item.owner);
                    EditorGUI.DrawRect(rect, item.isGroup ? Lighten(col) : col);
                    if (item.segment == _selected)
                        DrawOutline(rect, Color.white);
                    if (w > 36)
                        BarLabel(rect, " " + item.name, col);

                    var resize = new Rect(rect.xMax - ResizeW, rect.y, ResizeW, rect.height);
                    EditorGUIUtility.AddCursorRect(resize, MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(new Rect(rect.x, rect.y, Mathf.Max(0, rect.width - ResizeW), rect.height),
                        MouseCursor.Pan);

                    if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                    {
                        _selected = item.segment;
                        _dragSeg = item.segment;
                        _dragSet = item.set;
                        _dragMode = resize.Contains(e.mousePosition) ? 2 : 1;
                        _dragMouseX = e.mousePosition.x;
                        _dragStart = item.segment.start;
                        _dragDur = item.segment.duration;
                        Undo.RecordObject(item.set, _dragMode == 2 ? "Resize segment" : "Move segment");
                        if (e.clickCount == 2)
                        {
                            Selection.activeObject = item.set;
                            EditorGUIUtility.PingObject(item.set);
                        }
                        e.Use();
                    }
                }
            }
        }

        void HandleDrag(double fps)
        {
            if (_dragSeg == null)
                return;

            var e = Event.current;
            if (e.type != EventType.MouseDrag)
                return;

            double delta = (e.mousePosition.x - _dragMouseX) / _pps;
            if (_dragMode == 2)
                _dragSeg.duration = System.Math.Max(1.0 / System.Math.Max(1.0, fps),
                    TimelineSnap.Snap(_dragDur + delta, fps, _snap));
            else
                _dragSeg.start = TimelineSnap.Snap(_dragStart + delta, fps, _snap);

            if (_dragSet != null)
                EditorUtility.SetDirty(_dragSet);
            e.Use();
            Repaint();
        }

        static Color OwnerColor(string owner)
        {
            int h = owner != null ? owner.GetHashCode() : 0;
            return s_OwnerColors[(h & 0x7fffffff) % s_OwnerColors.Length];
        }

        static Color Lighten(Color c) => Color.Lerp(c, Color.white, 0.35f);

        static void DrawOutline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }

        static void BarLabel(Rect bar, string text, Color bg)
        {
            float lum = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            var style = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            style.normal.textColor = lum > 0.55f ? Color.black : Color.white;
            GUI.Label(bar, text, style);
        }
    }
}
