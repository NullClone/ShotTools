using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShotTools
{
    //
    // カメラが、Timeline の Cinemachine Track のどのクリップ（カット）で使われているかを調べる。
    // CinemachineSplineShot が、クリップの頭から終わりまでを 0〜1 として読むために使う。
    //
    // Cinemachine Track はクリップの中の時刻をカメラに知らせないので、カメラの側から Timeline を読む。
    //
    // 再生中は、Timeline を毎フレームたどらずに、クリップの位置を覚えておく。
    // 覚えたものは、Timeline が再生に使っているグラフが同じ間だけ使う。
    // Timeline 自身も、グラフを作り直すまではクリップの変更を見ないので、これで食い違わない。
    // 再生していないとき（エディタで直している間）は覚えずに、聞かれるたびに読む。
    //
    internal static class ShotClipLookup
    {
        // Fields

        private static readonly Dictionary<PlayableDirector, Entry> _cache = new();
        private static readonly List<PlayableDirector> _destroyed = new();


        // Methods

        // director の今の時刻での、vcam のカットの中の時刻（0 = クリップの頭、1 = 終わり）。
        // クリップの外なら、一番近いクリップの端。vcam がどのクリップにも使われていなければ false
        public static bool TryGetNormalizedTime(
            PlayableDirector director,
            CinemachineVirtualCameraBase vcam,
            out float time)
        {
            return TryGetNormalizedTime(director, vcam, out time, out _);
        }

        // vcam をクリップに持っている Timeline を、シーンから探す。なければ null。
        // いくつもあるときは、今の時刻が vcam のクリップにいちばん近いもの
        public static PlayableDirector FindDirector(CinemachineVirtualCameraBase vcam)
        {
            var found = default(PlayableDirector);
            var nearest = double.MaxValue;

            // 並べ方を選ぶ引数は Unity 6000.5 で廃止予定になり、引数なしの形は 6000.3 にはない
#if UNITY_6000_5_OR_NEWER
            var directors = Object.FindObjectsByType<PlayableDirector>();
#else
            var directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
#endif

            foreach (var director in directors)
            {
                if (!TryGetNormalizedTime(director, vcam, out _, out var distance) || distance >= nearest) continue;

                found = director;
                nearest = distance;
            }

            return found;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _cache.Clear();

        // distance は、今の時刻から vcam のクリップまでの秒数（クリップの中なら 0）
        private static bool TryGetNormalizedTime(
            PlayableDirector director,
            CinemachineVirtualCameraBase vcam,
            out float time,
            out double distance)
        {
            time = 0f;
            distance = double.MaxValue;

            if (director == null || vcam == null) return false;

            var entry = GetEntry(director);

            if (entry == null) return false;

            var now = director.time;

            foreach (var clip in entry.Clips)
            {
                if (!ReferenceEquals(clip.Camera, vcam)) continue;

                var end = clip.Start + clip.Duration;
                var away = now < clip.Start ? clip.Start - now : now > end ? now - end : 0d;

                if (away >= distance) continue;

                distance = away;
                time = clip.Duration > 0d ? Mathf.Clamp01((float)((now - clip.Start) / clip.Duration)) : 0f;
            }

            return distance < double.MaxValue;
        }

        private static Entry GetEntry(PlayableDirector director)
        {
            if (director.playableAsset is not TimelineAsset timeline) return null;

            var graph = director.playableGraph;
            var isPlaying = Application.isPlaying && graph.IsValid() && graph.GetRootPlayableCount() > 0;
            var root = isPlaying ? graph.GetRootPlayable(0).GetHandle() : default;

            if (_cache.TryGetValue(director, out var entry))
            {
                if (isPlaying && entry.Timeline == timeline && entry.Root == root) return entry;
            }
            else
            {
                RemoveDestroyed();

                entry = new Entry();
                _cache[director] = entry;
            }

            entry.Timeline = timeline;
            entry.Root = root;
            entry.Clips.Clear();

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is not CinemachineTrack || track.mutedInHierarchy) continue;

                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is not CinemachineShot shot) continue;

                    var vcam = shot.VirtualCamera.Resolve(director);

                    if (vcam == null) continue;

                    entry.Clips.Add(new Clip { Camera = vcam, Start = clip.start, Duration = clip.duration });
                }
            }

            return entry;
        }

        // シーンを閉じたあとに残った Timeline を、覚えから外す
        private static void RemoveDestroyed()
        {
            _destroyed.Clear();

            foreach (var director in _cache.Keys)
            {
                if (director == null)
                {
                    _destroyed.Add(director);
                }
            }

            foreach (var director in _destroyed)
            {
                _cache.Remove(director);
            }

            _destroyed.Clear();
        }


        private struct Clip
        {
            public CinemachineVirtualCameraBase Camera;
            public double Start;
            public double Duration;
        }

        private sealed class Entry
        {
            public TimelineAsset Timeline;

            // 覚えたときの、グラフの根。グラフが作り直されると変わる
            public PlayableHandle Root;

            public readonly List<Clip> Clips = new();
        }
    }
}
