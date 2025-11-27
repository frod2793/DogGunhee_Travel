# Google Play Core Plugin for Unity

## Overview

Google Play Plugins for Unity provide C# APIs for accessing Play in-game features from within the Unity Engine. These plugins also provide Unity Editor features for building a game before publishing it on [Google Play](https://play.google.com/console).  

The Play Core plugin provides the [Play Core Library](https://developer.android.com/guide/playcore) required by some other Google Play plugins, such as Play Asset Delivery, Play Integrity, Play In-app Reviews, and Play In-app Updates.

This plugin is a support plugin and does not provide any Play in-app features on its own. It will be installed automatically when you install other Google Play plugins that depend on this plugin using OpenUPM or GitHub.

## Pre-requisites

### Required Unity Version

To use the Google Play Core plugin, you must use a supported Unity version:

- All versions of Unity 2019.x, 2020.x, and newer are supported.
- If you use Unity 2018.x, version 2018.4 or newer are supported.
- If you use Unity 2017.x, only Unity 2017.4.40 is supported. All other versions aren't supported.

### Required Play Plugins

The following Google Play plugin will be installed automatically when you install the Google Play Core plugin using OpenUPM or when importing the package from GitHub:

- [External Dependency Manager plugin for Unity (EDM4U)](https://github.com/googlesamples/unity-jar-resolver). This plugin resolves AAR dependencies such as the Play Core library.

## Install the Plugin

To install the Google Play Core plugin using Open UPM, follow the instructions to [install via package manager](https://openupm.com/packages/com.google.play.core/#modal-manualinstallation) or [install via command-line](https://openupm.com/packages/com.google.play.core/#modal-commandlinetool).

Alternatively, you can download the latest `.unitypackage` from the Google Play Core plugin [GitHub releases page](https://github.com/google/play-core-unity/releases) and import it [using these instructions](https://developers.google.com/unity/instructions#install-unitypackage) on the Google APIs for Unity site.

## Support

To request features or report issues with the plugin, please use the [GitHub issue tracker](https://github.com/google/play-core-unity/issues).

## Known Issues
### Play Core library conflicts

When building an Android App Bundle with Unity's build system (e.g. "File > Build and Run"), Unity may include the
[monolithic Play Core library](https://maven.google.com/web/index.html?q=core#com.google.android.play:core) in a way that
causes conflicts with the [new Play libraries](https://developer.android.com/reference/com/google/android/play/core/release-notes#partitioned-apis)
included by Google Play Plugins for Unity.

To resolve these conflicts, follow the steps below:

1. Enable "Custom Main Gradle Template" in "Android Player > Publishing Settings"
2. Enable "Patch mainTemplate.gradle" in "Assets > External Dependency Manager > Android Resolver > Settings"
3. Include [this](https://dl.google.com/games/registry/unity/com.google.play.core/playcore_empty_m2repo.zip) empty monolithic
Play Core library as a local maven repository

These steps will allow [EDM4U](https://github.com/googlesamples/unity-jar-resolver) to update the mainTemplate.gradle to
include the empty monolithic Play Core library as a gradle dependency. This will override the version of the Play Core library
included by Unity and resolve the duplicate class errors and manifest merger failures.

## Related Plugins

Browse other [Google Play plugins for Unity](https://developers.google.com/unity/packages#google_play).
