using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace ShotTools.Editor
{
    //
    // Cinemachine Shot Move の Inspector に、Spline のマークの一覧と操作を出す。
    //
    // マークは Spline の側にあるが、使う人がカットを直すときに選ぶのはカメラなので、ここに出す。
    // 選んでいるマークの番号は、Scene ビューの表示（ShotMarksSceneView）と共通。
    //
    [CustomEditor(typeof(CinemachineShotMove))]
    internal sealed class CinemachineShotMoveEditor : UnityEditor.Editor
    {
        // Fields

        private const float IndexButtonWidth = 28f;
        private const float FieldLabelWidth = 38f;

        // マークがないカメラに最初のマークを打つときの、カメラから見る点までの距離（m）
        private const float DefaultLookDistance = 5f;

        private readonly List<ShotMark> _marks = new();


        // Methods

        public override bool RequiresConstantRepaint() => true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var move = (CinemachineShotMove)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Current Time: {move.NormalizedTime * 100f:F1}%");

            if (!TryGetContainer(move, out var container))
            {
                EditorGUILayout.HelpBox(
                    "Add a Cinemachine Spline Dolly to this camera and assign a Spline. The motion (marks) is stored in the Spline.",
                    MessageType.Info);

                return;
            }

            ShotMarks.Read(container.Spline, _marks);

            EditorGUILayout.LabelField($"Marks in \"{container.name}\": {_marks.Count}", EditorStyles.boldLabel);

            if (DrawMarkList(container)) return;

            EditorGUILayout.Space();

            DrawButtons(move, container);
        }

        private static bool TryGetContainer(CinemachineShotMove move, out SplineContainer container)
        {
            container = null;

            if (!move.TryGetComponent<CinemachineSplineDolly>(out var dolly)) return false;
            if (dolly.Spline == null || dolly.Spline.Spline == null) return false;

            container = dolly.Spline;

            return true;
        }

        // マークを書き換えたら true（そのフレームの残りは描かない）
        private bool DrawMarkList(SplineContainer container)
        {
            var selected = ShotMarksSceneView.Selected;

            for (var i = 0; i < _marks.Count; i++)
            {
                var mark = _marks[i];
                var isSelected = i == selected;

                using (new EditorGUILayout.VerticalScope(isSelected ? EditorStyles.helpBox : GUIStyle.none))
                {
                    EditorGUI.BeginChangeCheck();

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var pressed = GUILayout.Toggle(
                            isSelected,
                            i.ToString(),
                            EditorStyles.miniButton,
                            GUILayout.Width(IndexButtonWidth));

                        if (pressed && !isSelected)
                        {
                            ShotMarksSceneView.Selected = i;
                            SceneView.RepaintAll();
                        }

                        EditorGUIUtility.labelWidth = FieldLabelWidth;

                        mark.Time = Mathf.Clamp01(EditorGUILayout.FloatField("Time", mark.Time));
                        mark.Place = Mathf.Clamp01(EditorGUILayout.FloatField("Place", mark.Place));
                        mark.FieldOfView = EditorGUILayout.FloatField("FOV", mark.FieldOfView);
                        mark.Dutch = EditorGUILayout.FloatField("Dutch", mark.Dutch);

                        EditorGUIUtility.labelWidth = 0f;
                    }

                    if (isSelected)
                    {
                        mark.Look = EditorGUILayout.Vector3Field("Look Point", mark.Look);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        _marks[i] = mark;
                        ShotMarksSceneView.Apply(container, _marks, "Edit Shot Mark");

                        return true;
                    }
                }
            }

            return false;
        }

        private void DrawButtons(CinemachineShotMove move, SplineContainer container)
        {
            var selected = ShotMarksSceneView.Selected;
            var hasSelection = selected >= 0 && selected < _marks.Count;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Mark at Current Time"))
                {
                    AddMark(move, container);

                    return;
                }

                using (new EditorGUI.DisabledScope(!hasSelection))
                {
                    if (GUILayout.Button("Delete Selected"))
                    {
                        _marks.RemoveAt(selected);
                        ShotMarksSceneView.Selected = -1;
                        ShotMarksSceneView.Apply(container, _marks, "Delete Shot Mark");

                        return;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasSelection))
                {
                    if (GUILayout.Button("Smooth Selected"))
                    {
                        ShotMarks.Smooth(_marks, selected);
                        ShotMarksSceneView.Apply(container, _marks, "Smooth Shot Mark");

                        return;
                    }
                }

                if (GUILayout.Button("Smooth All"))
                {
                    ShotMarks.Smooth(_marks);
                    ShotMarksSceneView.Apply(container, _marks, "Smooth Shot Marks");
                }
            }
        }

        // 今の道の上の状態をそのままマークにするので、打っても絵は変わらない
        private void AddMark(CinemachineShotMove move, SplineContainer container)
        {
            if (!ShotMarks.Evaluate(container.Spline, move.NormalizedTime, out var mark))
            {
                var transform = move.transform;
                var look = transform.position + transform.forward * DefaultLookDistance;

                mark = new ShotMark
                {
                    Time = move.NormalizedTime,
                    Look = container.transform.InverseTransformPoint(look),
                };

                if (move.TryGetComponent<CinemachineCamera>(out var vcam))
                {
                    mark.FieldOfView = vcam.Lens.FieldOfView;
                }
            }

            _marks.Add(mark);

            ShotMarksSceneView.Apply(container, _marks, "Add Shot Mark");
            ShotMarksSceneView.Selected = _marks.FindIndex(m => m.Time == mark.Time);
        }
    }
}
