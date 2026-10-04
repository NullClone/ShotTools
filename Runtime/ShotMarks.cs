using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace ShotTools
{
    //
    // マークの並びを Spline の埋め込みデータとして読み書きする。
    //
    // 動きのデータを Spline の側に置くので、カメラを作り直しても、
    // Spline を別のカメラに付け替えても、動きは変わらない。
    //
    // 埋め込みデータの軸は本来「レールの上の位置」だが、ここでは時刻として使う。
    // 単位を Normalized にしておくと、レールに点を足しても Unity は軸の値を書き換えない。
    //
    // 3 つの埋め込みデータは、同じ番号どうしで 1 つのマークになる。
    // 数がそろっていないものは、マークがないものとして扱う。
    //
    public static class ShotMarks
    {
        // Fields

        // xyz = 見る点、w = レールの上の場所
        public const string LookKey = "Shot Look";

        // xyz = 見る点の変わり方、w = 場所の変わり方
        public const string LookTangentKey = "Shot Look Tangent";

        // x = 画角、y = 傾き、z = 画角の変わり方、w = 傾きの変わり方
        public const string LensKey = "Shot Lens";

        // これより短い時刻の差は、同じ時刻として扱う
        private const float MinTimeSpan = 1e-7f;


        // Methods

        public static bool Has(Spline spline) => spline != null && TryGetData(spline, out _, out _, out _);

        // Spline のマークを時刻の順に読む
        public static void Read(Spline spline, List<ShotMark> marks)
        {
            marks.Clear();

            if (spline == null) return;
            if (!TryGetData(spline, out var look, out var tangent, out var lens)) return;

            for (var i = 0; i < look.Count; i++)
            {
                marks.Add(GetMark(look, tangent, lens, i));
            }
        }

        // Spline のマークを置き換える。marks は時刻の順に並べ直される
        public static void Write(Spline spline, List<ShotMark> marks)
        {
            marks.Sort((a, b) => a.Time.CompareTo(b.Time));

            var look = new SplineData<float4> { PathIndexUnit = PathIndexUnit.Normalized };
            var tangent = new SplineData<float4> { PathIndexUnit = PathIndexUnit.Normalized };
            var lens = new SplineData<float4> { PathIndexUnit = PathIndexUnit.Normalized };

            foreach (var mark in marks)
            {
                look.Add(mark.Time, new float4(mark.Look, mark.Place));
                tangent.Add(mark.Time, new float4(mark.LookTangent, mark.PlaceTangent));
                lens.Add(mark.Time, new float4(mark.FieldOfView, mark.Dutch, mark.FieldOfViewTangent, mark.DutchTangent));
            }

            spline.SetFloat4Data(LookKey, look);
            spline.SetFloat4Data(LookTangentKey, tangent);
            spline.SetFloat4Data(LensKey, lens);
        }

        // 時刻 time のカメラの状態。マークの間は、値と変わり方がつながる 3 次式でつなぐ。
        // マークがなければ false
        public static bool Evaluate(Spline spline, float time, out ShotMark mark)
        {
            mark = default;

            if (spline == null) return false;
            if (!TryGetData(spline, out var look, out var tangent, out var lens)) return false;

            var count = look.Count;

            if (count == 1 || time <= look[0].Index)
            {
                mark = GetMark(look, tangent, lens, 0);

                return true;
            }

            if (time >= look[count - 1].Index)
            {
                mark = GetMark(look, tangent, lens, count - 1);

                return true;
            }

            var low = 0;
            var high = count - 1;

            while (high - low > 1)
            {
                var middle = (low + high) >> 1;

                if (look[middle].Index <= time)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            var a = GetMark(look, tangent, lens, low);
            var b = GetMark(look, tangent, lens, high);
            var span = b.Time - a.Time;

            if (span <= MinTimeSpan)
            {
                mark = b;

                return true;
            }

            mark = Interpolate(a, b, time, span);

            return true;
        }

        // 前後のマークから、なめらかにつながる変わり方（Tangent）を決め直す。index が負なら全部
        public static void Smooth(List<ShotMark> marks, int index = -1)
        {
            for (var i = 0; i < marks.Count; i++)
            {
                if (index >= 0 && i != index) continue;

                var mark = marks[i];
                var previous = i > 0 ? marks[i - 1] : (ShotMark?)null;
                var next = i < marks.Count - 1 ? marks[i + 1] : (ShotMark?)null;

                mark.PlaceTangent = SmoothTangent(mark, previous, next, m => m.Place);
                mark.FieldOfViewTangent = SmoothTangent(mark, previous, next, m => m.FieldOfView);
                mark.DutchTangent = SmoothTangent(mark, previous, next, m => m.Dutch);
                mark.LookTangent = new Vector3(
                    SmoothTangent(mark, previous, next, m => m.Look.x),
                    SmoothTangent(mark, previous, next, m => m.Look.y),
                    SmoothTangent(mark, previous, next, m => m.Look.z));

                marks[i] = mark;
            }
        }

        private static bool TryGetData(
            Spline spline,
            out SplineData<float4> look,
            out SplineData<float4> tangent,
            out SplineData<float4> lens)
        {
            tangent = null;
            lens = null;

            if (!spline.TryGetFloat4Data(LookKey, out look)) return false;
            if (!spline.TryGetFloat4Data(LookTangentKey, out tangent)) return false;
            if (!spline.TryGetFloat4Data(LensKey, out lens)) return false;

            return look.Count > 0 && tangent.Count == look.Count && lens.Count == look.Count;
        }

        private static ShotMark GetMark(
            SplineData<float4> look,
            SplineData<float4> tangent,
            SplineData<float4> lens,
            int index)
        {
            var lookKey = look[index];
            var tangentValue = tangent[index].Value;
            var lensValue = lens[index].Value;

            return new ShotMark
            {
                Time = lookKey.Index,
                Place = lookKey.Value.w,
                Look = lookKey.Value.xyz,
                FieldOfView = lensValue.x,
                Dutch = lensValue.y,
                PlaceTangent = tangentValue.w,
                LookTangent = tangentValue.xyz,
                FieldOfViewTangent = lensValue.z,
                DutchTangent = lensValue.w,
            };
        }

        // a と b の間を、エルミートの 3 次式でつなぐ。span は 2 つの時刻の差
        private static ShotMark Interpolate(in ShotMark a, in ShotMark b, float time, float span)
        {
            var t = (time - a.Time) / span;
            var t2 = t * t;
            var t3 = t2 * t;

            var h0 = 2f * t3 - 3f * t2 + 1f;
            var h1 = (t3 - 2f * t2 + t) * span;
            var h2 = -2f * t3 + 3f * t2;
            var h3 = (t3 - t2) * span;

            // 変わり方は、上の 3 次式を時刻で微分したもの
            var d0 = (6f * t2 - 6f * t) / span;
            var d1 = 3f * t2 - 4f * t + 1f;
            var d2 = -d0;
            var d3 = 3f * t2 - 2f * t;

            return new ShotMark
            {
                Time = time,
                Place = h0 * a.Place + h1 * a.PlaceTangent + h2 * b.Place + h3 * b.PlaceTangent,
                Look = h0 * a.Look + h1 * a.LookTangent + h2 * b.Look + h3 * b.LookTangent,
                FieldOfView = h0 * a.FieldOfView + h1 * a.FieldOfViewTangent + h2 * b.FieldOfView + h3 * b.FieldOfViewTangent,
                Dutch = h0 * a.Dutch + h1 * a.DutchTangent + h2 * b.Dutch + h3 * b.DutchTangent,
                PlaceTangent = d0 * a.Place + d1 * a.PlaceTangent + d2 * b.Place + d3 * b.PlaceTangent,
                LookTangent = d0 * a.Look + d1 * a.LookTangent + d2 * b.Look + d3 * b.LookTangent,
                FieldOfViewTangent = d0 * a.FieldOfView + d1 * a.FieldOfViewTangent + d2 * b.FieldOfView + d3 * b.FieldOfViewTangent,
                DutchTangent = d0 * a.Dutch + d1 * a.DutchTangent + d2 * b.Dutch + d3 * b.DutchTangent,
            };
        }

        // 行き過ぎないつなぎ方にする。前後で向きが変わるところと、
        // 前後どちらかと同じ値のところ（とどまる）では 0。
        // previous・next は、その側にマークがなければ null
        private static float SmoothTangent(in ShotMark mark, ShotMark? previous, ShotMark? next, Func<ShotMark, float> value)
        {
            var slopeBefore = previous.HasValue ? Slope(previous.Value, mark, value) : 0f;
            var slopeAfter = next.HasValue ? Slope(mark, next.Value, value) : 0f;

            if (!previous.HasValue) return slopeAfter;
            if (!next.HasValue) return slopeBefore;
            if (slopeBefore * slopeAfter <= 0f) return 0f;

            return 2f * slopeBefore * slopeAfter / (slopeBefore + slopeAfter);
        }

        private static float Slope(in ShotMark from, in ShotMark to, Func<ShotMark, float> value)
        {
            var span = to.Time - from.Time;

            return span > MinTimeSpan ? (value(to) - value(from)) / span : 0f;
        }
    }
}
