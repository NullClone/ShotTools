# Change Log
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).


## [0.2.0] - 2026-10-07

### Changed
- Renamed Cinemachine Shot Move to Cinemachine Spline Shot. Components already in a scene keep working
- Cinemachine Spline Shot now references the Spline directly and places the camera on it. The camera no longer needs a Spline Dolly. To upgrade a camera made with 0.1.0, assign the Spline of its Spline Dolly to Spline Shot and remove the Spline Dolly
- Spline Shot no longer has a Director property. It finds the Timeline that has the camera in a clip, instead of the first Timeline found in the scene
- Changes to the Timeline are picked up without calling `ShotClipLookup.Invalidate`. `ShotClipLookup` is now internal

### Fixed
- Fixed tracks in a muted group being read as not muted

### Removed
- Removed the `On Live` time source, with `Duration` and `Wrap Mode`
- Removed Cinemachine Follow Focus and `IShotFocusSubject`. The package no longer references the Universal Render Pipeline
- Removed the mark list and the add, delete, and smooth buttons from the Inspector of Cinemachine Spline Shot. Marks are written from a script with `ShotMarks` and adjusted in the Scene view


## [0.1.0] - 2026-10-04

### Added
- Added Shot Marks: the state of the camera at a time in the shot (place on the rail, look point, field of view, dutch), stored in the Spline
- Added Cinemachine Shot Move: advances the time of the shot and applies the marks to the camera
- Added the Inspector and Scene view handles for editing marks
- Added Cinemachine Follow Focus (URP)
