using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Splines;

namespace ShotTools.Editor
{
    //
    // Spline に埋め込んだマークを、Scene ビューで見て、直すための表示。
    // Cinemachine Shot Move の付いたカメラか、マークのある Spline を選ぶと出る。
    //
    // レールの上の印（その時刻にカメラがいる場所）と、見る点、その 2 つを結ぶ線を描く。
    // 印はレールに沿って、見る点は自由に動かせる。
    //
    // 3 つの埋め込みデータを別々に直すと並びがずれるので、
    // 直すときはいつも Apply を通して 3 つまとめて書き直す。
    //
    [InitializeOnLoad]
    internal static class ShotMarksSceneView
    {
        // Fields

        // 見る点の道を描くときの、マークとマークの間の分け方
        private const int PathSteps = 12;

        private const float LookHandleSize = 0.06f;
        private const float PlaceHandleSize = 0.05f;

        private static readonly Color _lookColor = new(1f, 0.75f, 0.2f);
        private static readonly Color _linkColor = new(1f, 0.75f, 0.2f, 0.4f);
        private static readonly Color _placeColor = new(0.4f, 0.8f, 1f);
        private static readonly Color _sightColor = new(0.3f, 1f, 0.5f);
        private static readonly List<ShotMark> _marks = new();


        // Properties

        // 選んでいるマークの番号。選んでいなければ負
        public static int Selected { get; set; } = -1;


        // Methods

        static ShotMarksSceneView()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        // 選んでいるオブジェクトから、マークのある Spline と、今の時刻（分からなければ負）を探す
        public static bool TryFind(GameObject target, out SplineContainer container, out float time)
        {
            container = null;
            time = -1f;

            if (target == null) return false;

            if (target.TryGetComponent<CinemachineShotMove>(out var move) &&
                target.TryGetComponent<CinemachineSplineDolly>(out var dolly))
            {
                container = dolly.Spline;
                time = move.NormalizedTime;
            }
            else
            {
                target.TryGetComponent(out container);
            }

            return container != null && container.Spline != null && ShotMarks.Has(container.Spline);
        }

        public static void Apply(SplineContainer container, List<ShotMark> marks, string undoName)
        {
            Undo.RecordObject(container, undoName);
            ShotMarks.Write(container.Spline, marks);
            EditorUtility.SetDirty(container);
            InternalEditorUtility.RepaintAllViews();
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!TryFind(Selection.activeGameObject, out var container, out var time)) return;

            ShotMarks.Read(container.Spline, _marks);

            DrawLookPath(container);

            if (DrawMarks(container)) return;
            if (DrawSelectedLook(container)) return;

            DrawSight(container, time);
        }

        private static void DrawLookPath(SplineContainer container)
        {
            var space = container.transform;

            Handles.color = _lookColor;

            for (var i = 0; i < _marks.Count - 1; i++)
            {
                var previous = space.TransformPoint(_marks[i].Look);

                for (var step = 1; step <= PathSteps; step++)
                {
                    var time = Mathf.Lerp(_marks[i].Time, _marks[i + 1].Time, step / (float)PathSteps);

                    ShotMarks.Evaluate(container.Spline, time, out var mark);

                    var next = space.TransformPoint(mark.Look);

                    Handles.DrawLine(previous, next, 2f);

                    previous = next;
                }
            }
        }

        // 見る点はクリックで選び、レールの上の印はつかんでレールに沿って動かす。
        // マークを書き換えたら true（そのフレームの残りは描かない）
        private static bool DrawMarks(SplineContainer container)
        {
            var spline = container.Spline;
            var space = container.transform;

            for (var i = 0; i < _marks.Count; i++)
            {
                var mark = _marks[i];
                var look = space.TransformPoint(mark.Look);
                var place = (Vector3)container.EvaluatePosition(Mathf.Clamp01(mark.Place));

                Handles.color = _linkColor;
                Handles.DrawDottedLine(place, look, 3f);

                var lookSize = HandleUtility.GetHandleSize(look) * LookHandleSize;

                Handles.color = i == Selected ? Color.white : _lookColor;

                if (Handles.Button(look, Quaternion.identity, lookSize, lookSize * 1.5f, Handles.SphereHandleCap))
                {
                    Select(i);
                }

                var placeSize = HandleUtility.GetHandleSize(place) * PlaceHandleSize;

                Handles.color = i == Selected ? Color.white : _placeColor;

                EditorGUI.BeginChangeCheck();

                var moved = Handles.FreeMoveHandle(place, placeSize, Vector3.zero, Handles.CubeHandleCap);

                if (EditorGUI.EndChangeCheck() && spline.Count > 1)
                {
                    SplineUtility.GetNearestPoint(
                        spline,
                        (float3)space.InverseTransformPoint(moved),
                        out _,
                        out var nearest);

                    mark.Place = nearest;
                    _marks[i] = mark;
                    Selected = i;

                    Apply(container, _marks, "Move Shot Mark Place");

                    return true;
                }

                Handles.Label(place, $"  {i}: {mark.Time * 100f:F0}%");
            }

            return false;
        }

        private static bool DrawSelectedLook(SplineContainer container)
        {
            if (Selected < 0 || Selected >= _marks.Count) return false;

            var space = container.transform;
            var mark = _marks[Selected];

            EditorGUI.BeginChangeCheck();

            var moved = Handles.PositionHandle(space.TransformPoint(mark.Look), Quaternion.identity);

            if (!EditorGUI.EndChangeCheck()) return false;

            mark.Look = space.InverseTransformPoint(moved);
            _marks[Selected] = mark;

            Apply(container, _marks, "Move Shot Mark Look Point");

            return true;
        }

        // 今の視線
        private static void DrawSight(SplineContainer container, float time)
        {
            if (time < 0f) return;
            if (!ShotMarks.Evaluate(container.Spline, time, out var now)) return;

            Handles.color = _sightColor;

            Handles.DrawLine(
                container.EvaluatePosition(Mathf.Clamp01(now.Place)),
                container.transform.TransformPoint(now.Look),
                2f);
        }

        private static void Select(int index)
        {
            Selected = index;

            InternalEditorUtility.RepaintAllViews();
        }
    }
}
