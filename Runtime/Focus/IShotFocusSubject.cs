using UnityEngine;

namespace ShotTools
{
    //
    // CinemachineFollowFocus に、ピントを合わせる相手を教えるもの。
    // CinemachineCamera と同じ GameObject に付ける。
    //
    public interface IShotFocusSubject
    {
        // Methods

        // head: 今ピントを合わせる点（顔。ワールド）
        // height: その人の身長（m）
        // shotSerial: カットの番号。変わったら、送らずにその場でピントを合わせる
        // 撮っていなければ false
        bool TryGetFocusSubject(out Vector3 head, out float height, out int shotSerial);
    }
}
