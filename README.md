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
  <a href="#about">About</a> •
  <a href="#features">Features</a> •
  <a href="#installation">Installation</a> •
  <a href="#getting-started">Getting Started</a> •
  <a href="#components">Components</a> •
  <a href="#requirements">Requirements</a> •
  <a href="#license">License</a>
</p>

<p align="center">
  English | <a href="README_ja.md">日本語</a>
</p>

## About

Shot Tools is a set of tools for building camera shots on top of Cinemachine.

One camera is one shot. The whole motion of a shot — where the camera is on the rail, what it looks at, its field of view and dutch — is stored in the Spline as **marks**. The camera itself only keeps the time of the shot, so you can rebuild the camera or move the Spline to another camera without losing the motion.

## Features

- **Marks stored in the Spline**
  - A mark is the state of the camera at a time in the shot: place on the rail, look point, field of view, and dutch
  - The axis is time, so two marks at the same place make the camera stay there (a tripod pan, a hold, a stop)
  - Marks are joined by smooth curves, with a tangent per mark like an Animation Curve
- **Driven by the Timeline clip**
  - A shot runs from the start to the end of its clip on a Cinemachine Track
  - Moving or resizing the clip retimes the shot; no keyframes to fix
  - It can also run from a value you set from a script
- **Adjusting in the Scene view**
  - Drag a mark along the rail, or move its look point freely
- **No lag, no shake**
  - The camera is a function of the time and the Spline only. It keeps no state from the previous frame
  - The camera is aimed directly at the look point instead of chasing a target, so scrubbing and playback give the same picture

## Installation

1. Open the Package Manager: `Window > Package Manager`
2. Click the `+` button in the top-left corner and select `Add package from git URL...`.
3. Enter the following URL and click `Add`.

```
https://github.com/NullClone/ShotTools.git
```

## Getting Started

1. Create a `Spline` for the rail (`GameObject > Spline`).
2. Create a `Cinemachine Camera`. Leave its `Position Control` and `Rotation Control` set to `None`.
3. Add `Cinemachine Spline Shot` to the camera (`Add Extension` or `Add Component > Cinemachine > Procedural > Extensions`) and assign the Spline to it.
4. Put the camera in a clip on a `Cinemachine Track` in your Timeline.
5. Write the marks to the Spline from a script with `ShotMarks` (see [Marks](#marks)). There is no button for adding marks yet; they are meant to be written by a script or a tool.
6. Select the camera or the Spline, then adjust the marks in the Scene view: the blue square moves along the rail, the orange sphere is the look point.

A shot that does not move is a Spline with a single knot.

## Components

### Cinemachine Spline Shot

An extension for `Cinemachine Camera`. It decides the time of the shot (0 = start, 1 = end), reads the marks from the Spline, and applies them: the place on the rail to the position, the look point to the rotation, and the field of view and dutch to the lens. The lens settings of the camera are not rewritten.

Spline Shot keeps no reference to a Timeline. The Timeline that has the camera in a clip is found automatically, also when Timelines are nested. When a camera is in the clips of two Timelines, the one that is showing the camera is used. Do not show one camera from two Timelines at the same moment.

The camera needs no `Spline Dolly`. Spline Shot places the camera on the Spline itself, so the picture depends only on the time and the Spline, and is the same when scrubbing and when playing.

| Property | Description |
| --- | --- |
| Spline | The Spline that holds the rail and the marks of the shot. |
| Time Source | `Timeline Clip`: from the start to the end of this camera's clip on a Cinemachine Track. `Manual`: the value of `Manual Time`. |
| Manual Time | Time of the shot used by `Manual`. Shown only when `Time Source` is `Manual`. |

In the Scene view, selecting the camera or the Spline shows:

| Handle | Meaning | How to edit |
| --- | --- | --- |
| Blue square | Place on the rail | Drag it along the rail |
| Orange sphere | Look point | Click to select, then move it with the arrows |
| Dotted line | Pairs a place with its look point | — |
| Green line | Current line of sight | — |

### Marks

Marks are stored in the Spline as three embedded data sets of `float4`. The index of each data point is the time of the shot (0–1).

| Key | Value |
| --- | --- |
| `Shot Look` | xyz = look point (in the space of the Spline object), w = place on the rail (0 = start, 1 = end, as a ratio of the length) |
| `Shot Look Tangent` | xyz = tangent of the look point, w = tangent of the place |
| `Shot Lens` | x = vertical field of view in degrees (0 or less keeps the lens of the camera), y = dutch in degrees, z and w = their tangents |

The three data sets must keep the same times. Edit marks from the Scene view or through `ShotMarks`; editing the embedded data separately in the Spline Inspector breaks the pairing.

From a script, use `ShotMarks`:

```csharp
var marks = new List<ShotMark>
{
    new ShotMark { Time = 0f, Place = 0f, Look = new Vector3(0f, 1.4f, 0f), FieldOfView = 30f },
    new ShotMark { Time = 1f, Place = 1f, Look = new Vector3(0f, 1.5f, 0f), FieldOfView = 24f },
};

ShotMarks.Smooth(marks);
ShotMarks.Write(splineContainer.Spline, marks);
```

## Requirements

- Unity 6000.3 or later
- Cinemachine 3.1.7 or later
- Splines 2.9.0 or later
- Timeline 1.8.12 or later

## License

This project is licensed under the [MIT License](LICENSE.md).
