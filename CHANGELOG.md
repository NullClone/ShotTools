# Change Log
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).


## [Unreleased]

### Removed
- Removed Cinemachine Follow Focus and `IShotFocusSubject`. The package no longer references the Universal Render Pipeline
- Removed the mark list and the add, delete, and smooth buttons from the Inspector of Cinemachine Shot Move. Marks are written from a script with `ShotMarks` and adjusted in the Scene view


## [0.1.0] - 2026-10-04

### Added
- Added Shot Marks: the state of the camera at a time in the shot (place on the rail, look point, field of view, dutch), stored in the Spline
- Added Cinemachine Shot Move: advances the time of the shot and applies the marks to the camera
- Added the Inspector and Scene view handles for editing marks
- Added Cinemachine Follow Focus (URP)
