using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Splines;

namespace ShotTools
{
    //
    // カットの中の時刻を進め、Spline に埋め込まれたマークをカメラに反映する。
    // CinemachineCamera に Extension として付け、同じカメラの Spline Dolly が使う Spline を読む。
    //
    // 動きのデータはすべて Spline の側にある。この部品が持つのは時刻だけなので、
    // カメラを作り直しても、Spline を別のカメラに付け替えても、動きは変わらない。
    //
    // 場所は Spline Dolly の Camera Position を動かす。
    // 向きは見る点を画面の中央に置く。相手を追いかけて計算しないので、遅れも揺れも出ない。
    // 画角と傾きは、その場の Lens を上書きする。カメラの Lens の設定そのものは書き換えない。
    //
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cinemachine/Procedural/Extensions/Cinemachine Shot Move")]
    public sealed class CinemachineShotMove : CinemachineExtension
    {
        // Fields

        // これより近い見る点には向けない（向きが決まらない）
        private const float MinLookDistanceSqr = 1e-10f;

        // 視線と上方向がこれより平行に近いときは、上方向を使わずに向ける
        private const float MinUpCrossSqr = 1e-8f;


        [SerializeField]
        [Tooltip("What drives the time of the shot. Timeline Clip: from the start to the end of this camera's clip on a Cinemachine Track. On Live: from the moment this camera goes live. Manual: the Manual Time below.")]
        private ShotTimeSource _timeSource = ShotTimeSource.TimelineClip;

        [SerializeField]
        [Tooltip("The Timeline used by Timeline Clip. If empty, one is searched for in the scene.")]
        private PlayableDirector _director;

        [SerializeField]
        [Tooltip("Length of the shot in seconds, used by On Live.")]
        [Min(0.01f)]
        private float _duration = 4f;

        [SerializeField]
        [Tooltip("What happens after the shot ends, used by On Live.")]
        private ShotWrapMode _wrapMode = ShotWrapMode.Once;

        [SerializeField]
        [Tooltip("Time of the shot used by Manual (0 = start, 1 = end). Also used as the preview time when the other sources are not available.")]
        [Range(0f, 1f)]
        private float _manualTime;


        private float _liveStart = -1f;
        private bool _wasLive;
        private bool _hasMark;
        private ShotMark _mark;
        private Transform _splineSpace;


        // Properties

        public ShotTimeSource TimeSource
        {
            get => _timeSource;
            set => _timeSource = value;
        }

        public PlayableDirector Director
        {
            get => _director;
            set => _director = value;
        }

        public float Duration
        {
            get => _duration;
            set => _duration = Mathf.Max(0.01f, value);
        }

        public ShotWrapMode WrapMode
        {
            get => _wrapMode;
            set => _wrapMode = value;
        }

        public float ManualTime
        {
            get => _manualTime;
            set => _manualTime = Mathf.Clamp01(value);
        }

        // 今のカットの中の時刻（0 = 頭、1 = 終わり）
        public float NormalizedTime { get; private set; }


        // Methods

        // 今の時刻のカメラの状態（マークの間をつないだもの）。マークがなければ false
        public bool TryGetCurrent(out ShotMark mark)
        {
            mark = _mark;

            return _hasMark;
        }

        public override void PrePipelineMutateCameraStateCallback(
            CinemachineVirtualCameraBase vcam,
            ref CameraState curState,
            float deltaTime)
        {
            NormalizedTime = EvaluateTime(vcam);
            _hasMark = false;

            if (!vcam.TryGetComponent<CinemachineSplineDolly>(out var dolly)) return;
            if (dolly.Spline == null) return;

            var spline = dolly.Spline.Spline;

            if (!ShotMarks.Evaluate(spline, NormalizedTime, out _mark)) return;

            _hasMark = true;
            _splineSpace = dolly.Spline.transform;

            dolly.CameraPosition = dolly.PositionUnits == PathIndexUnit.Normalized
                ? _mark.Place
                : spline.ConvertIndexUnit(_mark.Place, PathIndexUnit.Normalized, dolly.PositionUnits);

            if (_mark.FieldOfView > 0f)
            {
                curState.Lens.FieldOfView = _mark.FieldOfView;
            }

            curState.Lens.Dutch = _mark.Dutch;
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (!_hasMark || stage != CinemachineCore.Stage.Aim) return;

            var point = _splineSpace.TransformPoint(_mark.Look);
            var direction = point - state.GetCorrectedPosition();

            if (direction.sqrMagnitude < MinLookDistanceSqr) return;

            var up = state.ReferenceUp;

            state.RawOrientation = Vector3.Cross(direction.normalized, up).sqrMagnitude < MinUpCrossSqr
                ? Quaternion.FromToRotation(Vector3.forward, direction)
                : Quaternion.LookRotation(direction, up);

            state.ReferenceLookAt = point;
        }

        private float EvaluateTime(CinemachineVirtualCameraBase vcam)
        {
            switch (_timeSource)
            {
                case ShotTimeSource.TimelineClip:
                    return EvaluateClipTime(vcam);

                case ShotTimeSource.OnLive:
                    return EvaluateLiveTime(vcam);

                default:
                    return _manualTime;
            }
        }

        private float EvaluateClipTime(CinemachineVirtualCameraBase vcam)
        {
            if (_director == null)
            {
                _director = FindAnyObjectByType<PlayableDirector>();
            }

            return ShotClipLookup.TryGetNormalizedTime(_director, vcam, out var time) ? time : _manualTime;
        }

        private float EvaluateLiveTime(CinemachineVirtualCameraBase vcam)
        {
            if (!Application.isPlaying) return _manualTime;

            var live = CinemachineCore.IsLive(vcam);

            if (live && !_wasLive)
            {
                _liveStart = CinemachineCore.CurrentTime;
            }

            _wasLive = live;

            if (_liveStart < 0f) return 0f;

            var time = (CinemachineCore.CurrentTime - _liveStart) / _duration;

            switch (_wrapMode)
            {
                case ShotWrapMode.Loop:
                    return Mathf.Repeat(time, 1f);

                case ShotWrapMode.PingPong:
                    return Mathf.PingPong(time, 1f);

                default:
                    return Mathf.Clamp01(time);
            }
        }
    }
}
