using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Splines;

namespace ShotTools
{
    //
    // カットの中の時刻を決め、Spline に埋め込まれたマークをカメラに反映する。
    // CinemachineCamera に Extension として付ける。
    //
    // 動きのデータはすべて Spline の側にある。この部品が持つのは時刻の決め方だけなので、
    // カメラを作り直しても、Spline を別のカメラに付け替えても、動きは変わらない。
    //
    // Timeline への参照は保存しない。このカメラをクリップに入れている Timeline を、動くときに探す。
    // Cinemachine Track はクリップの中の時刻をカメラに知らせないので、カメラの側から読むしかないが、
    // 参照を持たなければ、シーンやプレハブを移しても切れるものがない。
    //
    // カメラの状態は、時刻と Spline だけで決まる。前のフレームの状態を持たないので、
    // 止めて動かしたときと再生したときで、同じ絵になる。
    // 場所を Spline Dolly に任せないのもこのため（Damping などの設定で、この前提が崩れる）。
    //
    // 向きは見る点を画面の中央に置く。相手を追いかけて計算しないので、遅れも揺れも出ない。
    // 画角と傾きは、その場の Lens を上書きする。カメラの Lens の設定そのものは書き換えない。
    //
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cinemachine/Procedural/Extensions/Cinemachine Spline Shot")]
    public sealed class CinemachineSplineShot : CinemachineExtension
    {
        // Fields

        // これより近い見る点には向けない（向きが決まらない）
        private const float MinLookDistanceSqr = 1e-10f;

        // 視線と上方向がこれより平行に近いときは、上方向を使わずに向ける
        private const float MinUpCrossSqr = 1e-8f;

        // 映っていないカメラが、自分をクリップに入れている Timeline を探し直す間隔（秒）。
        // 映っているカメラは、見つかるまで毎フレーム探す
        private const float SearchInterval = 0.5f;


        [SerializeField]
        [Tooltip("The Spline that holds the rail and the marks of the shot.")]
        private SplineContainer _spline;

        [SerializeField]
        [Tooltip("What drives the time of the shot. Timeline Clip: from the start to the end of this camera's clip on a Cinemachine Track. The Timeline that has this camera in a clip is found automatically. Manual: the Manual Time below.")]
        private ShotTimeSource _timeSource = ShotTimeSource.TimelineClip;

        [SerializeField]
        [Tooltip("Time of the shot used by Manual (0 = start, 1 = end). Also used when this camera is not in any clip.")]
        [Range(0f, 1f)]
        private float _manualTime;


        private PlayableDirector _director;
        private float _searchedAt = float.NegativeInfinity;
        private bool _hasMark;
        private ShotMark _mark;


        // Properties

        public SplineContainer Spline
        {
            get => _spline;
            set => _spline = value;
        }

        public ShotTimeSource TimeSource
        {
            get => _timeSource;
            set => _timeSource = value;
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
            _hasMark = _spline != null && ShotMarks.Evaluate(_spline.Spline, NormalizedTime, out _mark);

            if (!_hasMark) return;

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
            if (!_hasMark) return;

            if (stage == CinemachineCore.Stage.Body)
            {
                state.RawPosition = _spline.EvaluatePosition(Mathf.Clamp01(_mark.Place));
            }
            else if (stage == CinemachineCore.Stage.Aim)
            {
                Aim(ref state);
            }
        }

        private void Aim(ref CameraState state)
        {
            var point = _spline.transform.TransformPoint(_mark.Look);
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
            if (_timeSource != ShotTimeSource.TimelineClip) return _manualTime;

            if (ShotClipLookup.TryGetNormalizedTime(_director, vcam, out var time)) return time;

            var now = Time.realtimeSinceStartup;

            if (now - _searchedAt < SearchInterval && !CinemachineCore.IsLive(vcam)) return _manualTime;

            _searchedAt = now;
            _director = ShotClipLookup.FindDirector(vcam);

            return ShotClipLookup.TryGetNormalizedTime(_director, vcam, out time) ? time : _manualTime;
        }
    }
}
