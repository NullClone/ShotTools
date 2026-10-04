using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ShotTools
{
    //
    // カメラが、Timeline の Cinemachine Track のどのクリップ（カット）で使われているかを調べる。
    // CinemachineShotMove が、クリップの頭から終わりまでを 0〜1 として読むために使う。
    //
    // Timeline を毎フレームたどると重いので、クリップの位置を覚えておく。
    // 再生中は Timeline が変わらないものとして、覚えたものを使い続ける。
    // エディタで直している間は、短い間隔で調べ直す。
    //
    public static class ShotClipLookup
    {
        // Fields

        // エディタで Timeline を直している間に、調べ直す間隔（秒）
        private const float EditRefreshInterval = 0.25f;

        private static readonly Dictionary<PlayableDirector, Entry> _cache = new();


        // Methods

        // 覚えているクリップの位置を捨てる。Timeline をスクリプトから書き換えたあとに呼ぶ
        public static void Invalidate() => _cache.Clear();

        // director の今の時刻での、vcam のカットの中の時刻（0 = クリップの頭、1 = 終わり）。
        // クリップの外なら、一番近いクリップの端。vcam がどのクリップにも使われていなければ false
        public static bool TryGetNormalizedTime(
            PlayableDirector director,
            CinemachineVirtualCameraBase vcam,
            out float time)
        {
            time = 0f;

            if (director == null || vcam == null) return false;

            var entry = GetEntry(director);

            if (entry == null) return false;
            if (!entry.Clips.TryGetValue(vcam, out var clips)) return false;

            var now = director.time;
            var nearest = double.MaxValue;

            foreach (var (start, duration) in clips)
            {
                var end = start + duration;
                var distance = now < start ? start - now : now > end ? now - end : 0d;

                if (distance >= nearest) continue;

                nearest = distance;
                time = duration > 0d ? Mathf.Clamp01((float)((now - start) / duration)) : 0f;
            }

            return nearest < double.MaxValue;
        }

        private static Entry GetEntry(PlayableDirector director)
        {
            var asset = director.playableAsset;

            if (asset is not TimelineAsset timeline) return null;

            var now = Time.realtimeSinceStartup;

            if (_cache.TryGetValue(director, out var entry) && entry.Asset == asset)
            {
                if (Application.isPlaying || now - entry.BuiltAt < EditRefreshInterval) return entry;
            }

            entry = new Entry(asset, now);

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is not CinemachineTrack || track.muted) continue;

                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is not CinemachineShot shot) continue;

                    var vcam = shot.VirtualCamera.Resolve(director);

                    if (vcam == null) continue;

                    if (!entry.Clips.TryGetValue(vcam, out var clips))
                    {
                        clips = new List<(double Start, double Duration)>();
                        entry.Clips[vcam] = clips;
                    }

                    clips.Add((clip.start, clip.duration));
                }
            }

            _cache[director] = entry;

            return entry;
        }


        private sealed class Entry
        {
            public readonly PlayableAsset Asset;
            public readonly float BuiltAt;
            public readonly Dictionary<CinemachineVirtualCameraBase, List<(double Start, double Duration)>> Clips = new();

            public Entry(PlayableAsset asset, float builtAt)
            {
                Asset = asset;
                BuiltAt = builtAt;
            }
        }
    }
}
