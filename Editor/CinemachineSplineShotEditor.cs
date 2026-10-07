using UnityEditor;

namespace ShotTools.Editor
{
    //
    // Cinemachine Spline Shot の Inspector。
    // Manual Time は、Time Source が Manual のときだけ出す。
    //
    [CustomEditor(typeof(CinemachineSplineShot))]
    [CanEditMultipleObjects]
    internal sealed class CinemachineSplineShotEditor : UnityEditor.Editor
    {
        // Fields

        private SerializedProperty _spline;
        private SerializedProperty _timeSource;
        private SerializedProperty _manualTime;


        // Methods

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_spline);
            EditorGUILayout.PropertyField(_timeSource);

            if (!_timeSource.hasMultipleDifferentValues && _timeSource.intValue == (int)ShotTimeSource.Manual)
            {
                EditorGUILayout.PropertyField(_manualTime);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _spline = serializedObject.FindProperty(nameof(_spline));
            _timeSource = serializedObject.FindProperty(nameof(_timeSource));
            _manualTime = serializedObject.FindProperty(nameof(_manualTime));
        }
    }
}
