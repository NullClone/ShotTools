namespace ShotTools
{
    //
    // カットの中の時刻を、何から決めるか。
    //
    public enum ShotTimeSource
    {
        // Timeline の Cinemachine Track の、このカメラのクリップの頭から終わりまで
        TimelineClip,

        // このカメラが映った瞬間から Duration 秒
        OnLive,

        // Manual Time の値（スクリプトなどから動かす）
        Manual,
    }
}
