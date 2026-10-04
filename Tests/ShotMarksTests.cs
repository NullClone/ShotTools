using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace ShotTools.Tests
{
    //
    // マークの読み書きと、マークの間のつなぎ方を確かめる。
    //
    internal sealed class ShotMarksTests
    {
        // Fields

        private const float Tolerance = 1e-5f;


        // Methods

        [Test]
        public void Has_WithoutMarks_ReturnsFalse()
        {
            Assert.IsFalse(ShotMarks.Has(null));
            Assert.IsFalse(ShotMarks.Has(CreateSpline()));
        }

        [Test]
        public void Evaluate_WithoutMarks_ReturnsFalse()
        {
            Assert.IsFalse(ShotMarks.Evaluate(CreateSpline(), 0.5f, out _));
        }

        [Test]
        public void Read_AfterWrite_ReturnsMarksSortedByTime()
        {
            var spline = CreateSpline();
            var written = new List<ShotMark>
            {
                WithTangents(CreateMark(1f, 0.8f, new Vector3(0f, 1f, 4f), 30f)),
                CreateMark(0f, 0.1f, new Vector3(1f, 1.5f, 3f), 45f),
            };

            ShotMarks.Write(spline, written);

            var read = new List<ShotMark>();

            ShotMarks.Read(spline, read);

            Assert.IsTrue(ShotMarks.Has(spline));
            Assert.AreEqual(2, read.Count);
            Assert.AreEqual(0f, read[0].Time);
            Assert.AreEqual(1f, read[1].Time);
            Assert.AreEqual(0.8f, read[1].Place);
            Assert.AreEqual(new Vector3(0f, 1f, 4f), read[1].Look);
            Assert.AreEqual(30f, read[1].FieldOfView);
            Assert.AreEqual(2f, read[1].Dutch);
            Assert.AreEqual(0.25f, read[1].PlaceTangent);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), read[1].LookTangent);
            Assert.AreEqual(-5f, read[1].FieldOfViewTangent);
            Assert.AreEqual(0.5f, read[1].DutchTangent);
        }

        [Test]
        public void Evaluate_AtMarkTimeAndOutsideRange_ReturnsMarkValues()
        {
            var spline = CreateSplineWithMarks(
                CreateMark(0.2f, 0.1f, Vector3.zero, 40f),
                CreateMark(0.8f, 0.9f, Vector3.one, 20f));

            Assert.IsTrue(ShotMarks.Evaluate(spline, 0f, out var before));
            Assert.IsTrue(ShotMarks.Evaluate(spline, 0.2f, out var first));
            Assert.IsTrue(ShotMarks.Evaluate(spline, 0.8f, out var last));
            Assert.IsTrue(ShotMarks.Evaluate(spline, 1f, out var after));

            Assert.AreEqual(0.1f, before.Place, Tolerance);
            Assert.AreEqual(0.1f, first.Place, Tolerance);
            Assert.AreEqual(0.9f, last.Place, Tolerance);
            Assert.AreEqual(0.9f, after.Place, Tolerance);
            Assert.AreEqual(20f, after.FieldOfView, Tolerance);
        }

        [Test]
        public void Evaluate_BetweenMarksWithZeroTangents_EasesInAndOut()
        {
            var spline = CreateSplineWithMarks(
                CreateMark(0f, 0f, Vector3.zero, 40f),
                CreateMark(1f, 1f, new Vector3(2f, 0f, 0f), 20f));

            Assert.IsTrue(ShotMarks.Evaluate(spline, 0.5f, out var middle));
            Assert.IsTrue(ShotMarks.Evaluate(spline, 0.25f, out var quarter));

            Assert.AreEqual(0.5f, middle.Place, Tolerance);
            Assert.AreEqual(1f, middle.Look.x, Tolerance);
            Assert.AreEqual(30f, middle.FieldOfView, Tolerance);
            Assert.AreEqual(1.5f, middle.PlaceTangent, Tolerance);
            Assert.AreEqual(0.15625f, quarter.Place, Tolerance);
        }

        [Test]
        public void Evaluate_WithLinearTangents_IsLinear()
        {
            var first = CreateMark(0f, 0f, Vector3.zero, 40f);
            var last = CreateMark(0.5f, 1f, Vector3.zero, 40f);

            first.PlaceTangent = 2f;
            last.PlaceTangent = 2f;

            var spline = CreateSplineWithMarks(first, last);

            Assert.IsTrue(ShotMarks.Evaluate(spline, 0.1f, out var mark));

            Assert.AreEqual(0.2f, mark.Place, Tolerance);
            Assert.AreEqual(2f, mark.PlaceTangent, Tolerance);
        }

        [Test]
        public void Smooth_OnStraightLine_UsesTheSlope()
        {
            var marks = new List<ShotMark>
            {
                CreateMark(0f, 0f, Vector3.zero, 40f),
                CreateMark(0.5f, 0.5f, Vector3.zero, 40f),
                CreateMark(1f, 1f, Vector3.zero, 40f),
            };

            ShotMarks.Smooth(marks);

            Assert.AreEqual(1f, marks[0].PlaceTangent, Tolerance);
            Assert.AreEqual(1f, marks[1].PlaceTangent, Tolerance);
            Assert.AreEqual(1f, marks[2].PlaceTangent, Tolerance);
            Assert.AreEqual(0f, marks[1].FieldOfViewTangent, Tolerance);
        }

        [Test]
        public void Smooth_AtPeakOrHold_ReturnsZero()
        {
            var marks = new List<ShotMark>
            {
                CreateMark(0f, 0f, Vector3.zero, 40f),
                CreateMark(0.4f, 1f, Vector3.zero, 30f),
                CreateMark(0.7f, 0.5f, Vector3.zero, 30f),
                CreateMark(1f, 0.2f, Vector3.zero, 20f),
            };

            ShotMarks.Smooth(marks);

            Assert.AreEqual(0f, marks[1].PlaceTangent, Tolerance);
            Assert.AreEqual(0f, marks[1].FieldOfViewTangent, Tolerance);
            Assert.AreEqual(0f, marks[2].FieldOfViewTangent, Tolerance);
            Assert.Less(marks[2].PlaceTangent, 0f);
        }

        [Test]
        public void Smooth_WithIndex_ChangesOnlyThatMark()
        {
            var marks = new List<ShotMark>
            {
                CreateMark(0f, 0f, Vector3.zero, 40f),
                CreateMark(0.5f, 0.5f, Vector3.zero, 40f),
                CreateMark(1f, 1f, Vector3.zero, 40f),
            };

            ShotMarks.Smooth(marks, 1);

            Assert.AreEqual(0f, marks[0].PlaceTangent);
            Assert.AreEqual(1f, marks[1].PlaceTangent, Tolerance);
            Assert.AreEqual(0f, marks[2].PlaceTangent);
        }

        private static Spline CreateSpline()
        {
            var spline = new Spline();

            spline.Add(new BezierKnot(new float3(0f, 0f, 0f)));
            spline.Add(new BezierKnot(new float3(0f, 0f, 10f)));

            return spline;
        }

        private static Spline CreateSplineWithMarks(params ShotMark[] marks)
        {
            var spline = CreateSpline();

            ShotMarks.Write(spline, new List<ShotMark>(marks));

            return spline;
        }

        private static ShotMark CreateMark(float time, float place, Vector3 look, float fieldOfView)
        {
            return new ShotMark
            {
                Time = time,
                Place = place,
                Look = look,
                FieldOfView = fieldOfView,
            };
        }

        private static ShotMark WithTangents(ShotMark mark)
        {
            mark.Dutch = 2f;
            mark.PlaceTangent = 0.25f;
            mark.LookTangent = new Vector3(1f, 2f, 3f);
            mark.FieldOfViewTangent = -5f;
            mark.DutchTangent = 0.5f;

            return mark;
        }
    }
}
