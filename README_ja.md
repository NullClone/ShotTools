<h1 align="center">
  Shot Tools
</h1>

<p align="center">
  <a href="https://github.com/NullClone/ShotTools/releases/latest">
    <img src="https://img.shields.io/github/v/release/NullClone/ShotTools" alt="Latest Release"></a>
  <a href="https://github.com/NullClone/ShotTools/blob/main/LICENSE.md">
    <img src="https://img.shields.io/badge/License-MIT-brightgreen.svg" alt="License MIT"></a>
</p>

<p align="center">
  <a href="#概要">概要</a> •
  <a href="#特徴">特徴</a> •
  <a href="#インストール">インストール</a> •
  <a href="#はじめに">はじめに</a> •
  <a href="#コンポーネント">コンポーネント</a> •
  <a href="#動作環境">動作環境</a> •
  <a href="#ライセンス">ライセンス</a>
</p>

<p align="center">
  <a href="README.md">English</a> | 日本語
</p>

## 概要

Shot Tools は、Cinemachine の上でカメラのカットを作るための道具です。

カメラ 1 台が 1 カットです。カットの動き（レールの上のどこにいるか、どこを見るか、画角、傾き）は、すべて**マーク**として Spline の中に入ります。カメラが持つのはカットの中の時刻だけなので、カメラを作り直しても、Spline を別のカメラに付け替えても、動きは失われません。

## 特徴

- **Spline に埋め込むマーク**
  - マークは、カットの中のある時刻のカメラの状態です（レールの上の場所・見る点・画角・傾き）
  - 軸は時刻なので、同じ場所にマークを 2 つ置けば、カメラはそこにとどまります（三脚のパン・タメ・止め）
  - マークの間はなめらかな曲線でつながり、アニメーションカーブと同じように、マークごとに接線を持ちます
- **Timeline のクリップで動く**
  - カットは、Cinemachine Track のクリップの頭から終わりまでで動きます
  - クリップを動かしたり長さを変えたりすると、カットの速さもそれに合います。キーフレームを直す必要はありません
  - スクリプトから時刻を入れることもできます
- **Scene ビューで直す**
  - マークをレールに沿ってつかんで動かし、見る点は自由に動かせます
- **遅れも揺れもない**
  - カメラの状態は、時刻と Spline だけで決まります。前のフレームの状態を持ちません
  - 相手を追いかけるのではなく、見る点にカメラを直接向けるので、止めて動かしたときも再生したときも同じ絵になります

## インストール

1. Package Manager を開きます: `Window > Package Manager`
2. 左上の `+` ボタンを押し、`Add package from git URL...` を選びます。
3. 次の URL を入れて `Add` を押します。

```
https://github.com/NullClone/ShotTools.git
```

## はじめに

1. レールにする `Spline` を作ります（`GameObject > Spline`）。
2. `Cinemachine Camera` を作ります。`Position Control` と `Rotation Control` は `None` のままにします。
3. カメラに `Cinemachine Spline Shot` を付け（`Add Extension`、または `Add Component > Cinemachine > Procedural > Extensions`）、Spline を指定します。
4. Timeline の `Cinemachine Track` のクリップに、そのカメラを入れます。
5. スクリプトから `ShotMarks` で、Spline にマークを書き込みます（[マーク](#マーク)を参照）。マークを足すボタンはまだありません。マークは、スクリプトやツールが書き込むものとして作っています。
6. カメラか Spline を選び、Scene ビューでマークを直します。水色の四角はレールに沿って動き、橙の丸が見る点です。

動かないカットは、点が 1 つだけの Spline にします。

## コンポーネント

### Cinemachine Spline Shot

`Cinemachine Camera` の Extension です。カットの中の時刻（0 = 頭、1 = 終わり）を決め、Spline からマークを読んで、カメラに反映します。レールの上の場所は位置に、見る点は向きに、画角と傾きは Lens に入ります。カメラの Lens の設定そのものは書き換えません。

Spline Shot は Timeline への参照を持ちません。このカメラをクリップに入れている Timeline を、自動で見つけます（Timeline を入れ子にしていても同じです）。1 台のカメラを 2 つの Timeline のクリップに入れたときは、今そのカメラを映している Timeline を使います。同じ瞬間に、2 つの Timeline から同じカメラを映すことはできません。

カメラに `Spline Dolly` は要りません。Spline Shot が自分でカメラを Spline の上に置くので、絵は時刻と Spline だけで決まり、止めて動かしたときも再生したときも同じになります。

| 項目 | 内容 |
| --- | --- |
| Spline | レールとマークを持つ Spline。 |
| Time Source | `Timeline Clip`: Cinemachine Track の、このカメラのクリップの頭から終わりまで。`Manual`: `Manual Time` の値。 |
| Manual Time | `Manual` のときの時刻。`Time Source` が `Manual` のときだけ表示されます。 |

Scene ビューでは、カメラか Spline を選ぶと次の印が出ます。

| 印 | 意味 | 直し方 |
| --- | --- | --- |
| 水色の四角 | レールの上の場所 | つかむとレールに沿って動きます |
| 橙の丸 | 見る点 | クリックで選んで、矢印で動かします |
| 点線 | 場所と見る点の組 | — |
| 緑の線 | 今の視線 | — |

### マーク

マークは、Spline の埋め込みデータ（`float4`）3 つとして入ります。それぞれのデータの位置（Index）が、カットの中の時刻（0〜1）です。

| キー | 値 |
| --- | --- |
| `Shot Look` | xyz = 見る点（Spline のオブジェクトの空間）、w = レールの上の場所（0 = 始点、1 = 終点。道のりの割合） |
| `Shot Look Tangent` | xyz = 見る点の接線、w = 場所の接線 |
| `Shot Lens` | x = 縦の画角（度。0 以下ならカメラの Lens のまま）、y = 傾き（度）、z・w = それぞれの接線 |

3 つのデータは、同じ時刻でそろっている必要があります。マークは Scene ビューか `ShotMarks` から直してください。Spline の Inspector で埋め込みデータを別々に直すと、組がずれます。

スクリプトからは `ShotMarks` を使います。

```csharp
var marks = new List<ShotMark>
{
    new ShotMark { Time = 0f, Place = 0f, Look = new Vector3(0f, 1.4f, 0f), FieldOfView = 30f },
    new ShotMark { Time = 1f, Place = 1f, Look = new Vector3(0f, 1.5f, 0f), FieldOfView = 24f },
};

ShotMarks.Smooth(marks);
ShotMarks.Write(splineContainer.Spline, marks);
```

## 動作環境

- Unity 6000.3 以降
- Cinemachine 3.1.7 以降
- Splines 2.9.0 以降
- Timeline 1.8.12 以降

## ライセンス

このプロジェクトは [MIT License](LICENSE.md) で公開しています。
