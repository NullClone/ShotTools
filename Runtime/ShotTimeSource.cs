namespace ShotTools
{
    //
    // カットの中の時刻を、何から決めるか。
    //
    // 値は保存されるので、番号を変えない。
    //
    public enum ShotTimeSource
    {
        // Timeline の Cinemachine Track の、このカメラのクリップの頭から終わりまで
        TimelineClip = 0,

        // Manual Time の値（スクリプトなどから動かす）
        Manual = 2,
    }
}
