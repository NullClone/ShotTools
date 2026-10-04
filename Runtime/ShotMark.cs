using UnityEngine;

namespace ShotTools
{
    //
    // マーク: カットの中のある時刻の、カメラの状態。
    //
    // 軸は時刻なので、同じ場所に時刻の違うマークを置けば「とどまる」
    //（三脚のパン・タメ・止め）になる。
    //
    // Vector3 と同じ、値をまとめただけの型なので、フィールドをそのまま見せる。
    //
    public struct ShotMark
    {
        // Fields

        // カットの中の時刻（0 = 頭、1 = 終わり）
        public float Time;

        // レールの上の場所（0 = 始点、1 = 終点。道のりの割合）
        public float Place;

        // 見る点（Spline のオブジェクトの空間）
        public Vector3 Look;

        // 縦の画角（度）。0 以下ならカメラの Lens のまま
        public float FieldOfView;

        // 傾き（度）
        public float Dutch;

        // このマークを通るときの変わり方（時刻 1 あたり）。前後のマークへのつながり方を決める
        public float PlaceTangent;
        public Vector3 LookTangent;
        public float FieldOfViewTangent;
        public float DutchTangent;
    }
}
