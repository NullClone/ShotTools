#if SHOT_TOOLS_URP
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShotTools
{
    //
    // フォローフォーカス: 撮っている顔にピントを合わせ続け、サイズに合わせてボケの量を変える。
    // CinemachineCamera に Extension として付ける。ピントの相手は、同じ GameObject の IShotFocusSubject が教える。
    //
    // URP の被写界深度（Depth of Field の Bokeh）を、自分で作った全体の Volume で動かす。
    // プロジェクトの Volume やアセットは書き換えない。Volume は保存されず、部品を外すと消える。
    //
    // ピントは顔までの距離（カメラの前方向）。
    // 絞り（F 値）は「画面の縦 ÷ 身長」が小さいほど開く（アップほどボケる）。
    // カットの頭ではその場で合わせ、カットの途中は Focus Pull Time でなめらかに送る。
    //
    // 出力のカメラで、URP の Post Processing がオンになっている必要がある。
    // Timeline を止めて動かしたときもボケを出す（Play しなくても見える）。
    //
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cinemachine/Procedural/Extensions/Cinemachine Follow Focus")]
    public sealed class CinemachineFollowFocus : CinemachineExtension
    {
        // Fields

        // 「画面の縦 ÷ 身長」がこれ以下ならアップ、これ以上なら引きとして扱う
        private const float CloseUpFrame = 0.25f;
        private const float WideFrame = 2.5f;

        // 焦点距離を出すときの、センサーの縦（mm）
        private const float SensorHeight = 24f;

        private const float MinFocusDistance = 0.1f;
        private const float MinSubjectHeight = 0.5f;


        [SerializeField]
        [Tooltip("Aperture (f-number) for close-ups, where the frame height is 1/4 of the subject's height or less. Smaller values blur more.")]
        [Range(1f, 32f)]
        private float _closeUpAperture = 2f;

        [SerializeField]
        [Tooltip("Aperture (f-number) for wide shots, where the frame height is 2.5 times the subject's height or more. Sizes in between change continuously.")]
        [Range(1f, 32f)]
        private float _wideAperture = 8f;

        [SerializeField]
        [Tooltip("Time in seconds to pull focus when the distance to the subject changes during a shot. 0 snaps immediately.")]
        [Range(0f, 1f)]
        private float _focusPullTime = 0.15f;

        [SerializeField]
        [Tooltip("Offset of the focus point in meters. Positive values focus behind the face.")]
        [Range(-1f, 1f)]
        private float _focusOffset;

        [SerializeField]
        [Tooltip("Number of aperture blades (the shape of the bokeh).")]
        [Range(3, 9)]
        private int _bladeCount = 9;

        [SerializeField]
        [Tooltip("Priority of the depth of field Volume. Set it higher than the project's Volumes to let this one win.")]
        private float _volumePriority = 100f;


        private Volume _volume;
        private VolumeProfile _profile;
        private DepthOfField _depthOfField;
        private float _focusVelocity;
        private int _shotSerial = int.MinValue;
        private bool _warned;


        // Properties

        // 今のピントの距離（m）
        public float FocusDistance { get; private set; }

        // 今の絞り（F 値）
        public float Aperture { get; private set; }


        // Methods

        protected override void OnDestroy()
        {
            ReleaseVolume();

            base.OnDestroy();
        }

        private void OnDisable() => ReleaseVolume();

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;
            if (!vcam.TryGetComponent<IShotFocusSubject>(out var subject)) return;
            if (!subject.TryGetFocusSubject(out var head, out var height, out var shotSerial)) return;

            if (!CinemachineCore.IsLive(vcam))
            {
                if (_volume != null)
                {
                    _volume.weight = 0f;
                }

                return;
            }

            EnsureVolume();
            WarnIfPostProcessingOff(vcam);

            var position = state.GetFinalPosition();
            var forward = state.GetFinalOrientation() * Vector3.forward;
            var target = Mathf.Max(MinFocusDistance, Vector3.Dot(head - position, forward) + _focusOffset);

            // deltaTime が負なのは、Cinemachine が前のフレームから続いていないと知らせるとき
            if (shotSerial != _shotSerial || deltaTime < 0f || _focusPullTime <= 0f)
            {
                FocusDistance = target;
                _focusVelocity = 0f;
                _shotSerial = shotSerial;
            }
            else
            {
                FocusDistance = Mathf.SmoothDamp(
                    FocusDistance,
                    target,
                    ref _focusVelocity,
                    _focusPullTime,
                    Mathf.Infinity,
                    deltaTime);
            }

            var tanHalfFov = Mathf.Tan(state.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);

            Aperture = EvaluateAperture(tanHalfFov, height);

            _volume.priority = _volumePriority;
            _volume.weight = 1f;
            _depthOfField.focusDistance.Override(FocusDistance);
            _depthOfField.aperture.Override(Aperture);
            _depthOfField.focalLength.Override(Mathf.Clamp(SensorHeight * 0.5f / tanHalfFov, 1f, 300f));
            _depthOfField.bladeCount.Override(_bladeCount);
        }

        // 「画面の縦 ÷ 身長」の対数で、アップの F 値と引きの F 値の間をつなぐ
        private float EvaluateAperture(float tanHalfFov, float height)
        {
            var frame = 2f * FocusDistance * tanHalfFov / Mathf.Max(MinSubjectHeight, height);

            var size = Mathf.InverseLerp(
                Mathf.Log(CloseUpFrame),
                Mathf.Log(WideFrame),
                Mathf.Log(Mathf.Max(frame, 1e-3f)));

            return Mathf.Exp(Mathf.Lerp(Mathf.Log(_closeUpAperture), Mathf.Log(_wideAperture), size));
        }

        private void EnsureVolume()
        {
            if (_volume != null) return;

            var volumeObject = new GameObject("Cinemachine Follow Focus")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            _volume = volumeObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = _volumePriority;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.HideAndDontSave;

            _depthOfField = _profile.Add<DepthOfField>(true);
            _depthOfField.mode.Override(DepthOfFieldMode.Bokeh);

            _volume.sharedProfile = _profile;
        }

        private void ReleaseVolume()
        {
            if (_volume != null)
            {
                Release(_volume.gameObject);
            }

            if (_profile != null)
            {
                Release(_profile);
            }

            _volume = null;
            _profile = null;
            _depthOfField = null;
        }

        private void WarnIfPostProcessingOff(CinemachineVirtualCameraBase vcam)
        {
            if (_warned) return;

            _warned = true;

            var brain = CinemachineCore.FindPotentialTargetBrain(vcam);
            var outputCamera = brain != null ? brain.OutputCamera : null;

            if (outputCamera == null) return;
            if (!outputCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return;
            if (data.renderPostProcessing) return;

            Debug.LogWarning(
                $"[Shot Tools] Post Processing is off on the output camera \"{outputCamera.name}\", so Follow Focus has no visible effect.",
                outputCamera);
        }

        private static void Release(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
#endif
