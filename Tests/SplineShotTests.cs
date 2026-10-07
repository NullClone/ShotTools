using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Splines;
using UnityEngine.Timeline;

namespace ShotTools.Tests
{
    //
    // Timeline のクリップからカットの中の時刻が決まることと、
    // その時刻のマークが、Spline Shot でカメラに反映されることを確かめる。
    //
    public sealed class SplineShotTests
    {
        // Fields

        private const float Tolerance = 1e-4f;

        private readonly List<Object> _created = new();


        // Methods

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void ClipTime_InsideAndOutsideClip_IsRatioClampedToClip()
        {
            var vcam = CreateCamera(out _);
            var director = CreateDirector(vcam, 2d, 4d, out _);

            director.time = 3d;

            Assert.IsTrue(ShotClipLookup.TryGetNormalizedTime(director, vcam, out var inside));
            Assert.AreEqual(0.25f, inside, Tolerance);

            director.time = 0d;

            Assert.IsTrue(ShotClipLookup.TryGetNormalizedTime(director, vcam, out var before));
            Assert.AreEqual(0f, before, Tolerance);

            director.time = 10d;

            Assert.IsTrue(ShotClipLookup.TryGetNormalizedTime(director, vcam, out var after));
            Assert.AreEqual(1f, after, Tolerance);
        }

        [Test]
        public void ClipTime_AfterClipIsMoved_UsesTheNewPlace()
        {
            var vcam = CreateCamera(out _);
            var director = CreateDirector(vcam, 2d, 4d, out var clip);

            director.time = 3d;

            ShotClipLookup.TryGetNormalizedTime(director, vcam, out _);

            clip.start = 3d;
            clip.duration = 2d;

            Assert.IsTrue(ShotClipLookup.TryGetNormalizedTime(director, vcam, out var time));
            Assert.AreEqual(0f, time, Tolerance);

            director.time = 4d;

            Assert.IsTrue(ShotClipLookup.TryGetNormalizedTime(director, vcam, out time));
            Assert.AreEqual(0.5f, time, Tolerance);
        }

        [Test]
        public void ClipTime_WithMutedTrackOrUnusedCamera_ReturnsFalse()
        {
            var vcam = CreateCamera(out _);
            var unused = CreateCamera(out _);
            var director = CreateDirector(vcam, 2d, 4d, out var clip);

            Assert.IsFalse(ShotClipLookup.TryGetNormalizedTime(director, unused, out _));
            Assert.IsFalse(ShotClipLookup.TryGetNormalizedTime(null, vcam, out _));

            clip.GetParentTrack().muted = true;

            Assert.IsFalse(ShotClipLookup.TryGetNormalizedTime(director, vcam, out _));
        }

        [Test]
        public void Update_WithTwoTimelines_UsesTheOneThatHasTheCamera()
        {
            var other = CreateCamera(out _);
            var vcam = CreateCamera(out var splineShot);
            var otherDirector = CreateDirector(other, 0d, 10d, out _);
            var director = CreateDirector(vcam, 2d, 4d, out _);

            otherDirector.time = 9d;
            director.time = 5d;

            vcam.UpdateCameraState(Vector3.up, -1f);

            Assert.AreEqual(0.75f, splineShot.NormalizedTime, Tolerance);
        }

        [Test]
        public void Update_WithCameraInNoTimeline_UsesManualTime()
        {
            var other = CreateCamera(out _);
            var vcam = CreateCamera(out var splineShot);
            var director = CreateDirector(other, 0d, 10d, out _);

            director.time = 5d;
            splineShot.ManualTime = 0.1f;

            vcam.UpdateCameraState(Vector3.up, -1f);

            Assert.AreEqual(0.1f, splineShot.NormalizedTime, Tolerance);
        }

        [Test]
        public void Update_WithMarks_PlacesCameraOnRailAndAimsAtLookPoint()
        {
            var vcam = CreateCamera(out var splineShot);
            var container = CreateRail(new Vector3(1f, 2f, 3f));
            var look = new Vector3(4f, 1f, 5f);

            ShotMarks.Write(container.Spline, new List<ShotMark>
            {
                new ShotMark { Time = 0f, Place = 0f, Look = look, FieldOfView = 30f, Dutch = 2f },
                new ShotMark { Time = 1f, Place = 1f, Look = look, FieldOfView = 20f, Dutch = 6f },
            });

            splineShot.Spline = container;
            splineShot.TimeSource = ShotTimeSource.Manual;
            splineShot.ManualTime = 0.5f;

            vcam.UpdateCameraState(Vector3.up, -1f);

            var state = vcam.State;
            var expectedPosition = (Vector3)container.EvaluatePosition(0.5f);
            var expectedForward = (container.transform.TransformPoint(look) - expectedPosition).normalized;

            Assert.Less(Vector3.Distance(expectedPosition, state.RawPosition), Tolerance);
            Assert.Less(Vector3.Distance(expectedForward, state.RawOrientation * Vector3.forward), Tolerance);
            Assert.AreEqual(25f, state.Lens.FieldOfView, Tolerance);
            Assert.AreEqual(4f, state.Lens.Dutch, Tolerance);
        }

        [Test]
        public void Update_WithoutSpline_LeavesCameraAsIs()
        {
            var vcam = CreateCamera(out var splineShot);

            vcam.transform.position = new Vector3(1f, 2f, 3f);
            splineShot.TimeSource = ShotTimeSource.Manual;

            vcam.UpdateCameraState(Vector3.up, -1f);

            Assert.IsFalse(splineShot.TryGetCurrent(out _));
            Assert.Less(Vector3.Distance(new Vector3(1f, 2f, 3f), vcam.State.RawPosition), Tolerance);
        }

        private CinemachineCamera CreateCamera(out CinemachineSplineShot splineShot)
        {
            var gameObject = new GameObject("Camera");

            _created.Add(gameObject);

            var vcam = gameObject.AddComponent<CinemachineCamera>();

            splineShot = gameObject.AddComponent<CinemachineSplineShot>();

            return vcam;
        }

        // 長さ 10 m のまっすぐなレール。offset は、Spline のオブジェクトの場所
        private SplineContainer CreateRail(Vector3 offset)
        {
            var gameObject = new GameObject("Path");

            _created.Add(gameObject);
            gameObject.transform.position = offset;

            var container = gameObject.AddComponent<SplineContainer>();

            container.Spline.Clear();
            container.Spline.Add(new BezierKnot(new float3(0f, 0f, 0f)));
            container.Spline.Add(new BezierKnot(new float3(0f, 0f, 10f)));

            return container;
        }

        // vcam を、start 秒から duration 秒のクリップに入れた Timeline
        private PlayableDirector CreateDirector(
            CinemachineVirtualCameraBase vcam,
            double start,
            double duration,
            out TimelineClip clip)
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();

            _created.Add(timeline);

            var track = timeline.CreateTrack<CinemachineTrack>();

            clip = track.CreateClip<CinemachineShot>();
            clip.start = start;
            clip.duration = duration;

            var gameObject = new GameObject("Director");

            _created.Add(gameObject);

            var director = gameObject.AddComponent<PlayableDirector>();
            var shot = (CinemachineShot)clip.asset;

            shot.VirtualCamera.exposedName = System.Guid.NewGuid().ToString();
            director.playableAsset = timeline;
            director.SetReferenceValue(shot.VirtualCamera.exposedName, vcam);

            return director;
        }
    }
}
