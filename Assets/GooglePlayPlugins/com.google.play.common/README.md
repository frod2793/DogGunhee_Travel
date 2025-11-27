# Google Play Common Plugin for Unity

## Overview

Google Play Plugins for Unity provide C# APIs for accessing Play in-game features from within the Unity Engine. These plugins also provide Unity Editor features for building a game before publishing it on [Google Play](https://play.google.com/console).  

The Play Common plugin provides the [Play Common Library](https://developer.android.com/guide/playcore) required by some other Google Play plugins, such as Play Asset Delivery, Play Integrity, Play In-app Reviews, and Play In-app Updates.

This plugin is a support plugin and does not provide any Play in-app features on its own. It will be installed automatically when you install other Google Play plugins that depend on this plugin using OpenUPM or GitHub.

## Pre-requisites

### Required Unity Version

To use the Google Play Common plugin, you must use a supported Unity version:

- All versions of Unity 2019.x, 2020.x, and newer are supported.
- If you use Unity 2018.x, version 2018.4 or newer are supported.
- If you use Unity 2017.x, only Unity 2017.4.40 is supported. All other versions aren't supported.

### Required Play Plugins

The following Google Play plugin will be installed automatically when you install the Google Play Common plugin using OpenUPM or when importing the package from GitHub:

- [External Dependency Manager plugin for Unity (EDM4U)](https://github.com/googlesamples/unity-jar-resolver). This plugin resolves AAR dependencies such as the Play Common library.

## Install the Plugin

To install the Google Play Common plugin using Open UPM, follow the instructions to [install via package manager](https://openupm.com/packages/com.google.play.common/#modal-manualinstallation) or [install via command-line](https://openupm.com/packages/com.google.play.common/#modal-commandlinetool).

Alternatively, you can download the latest `.unitypackage` from the Google Play Common plugin [GitHub releases page](https://github.com/google/play-common-unity/releases) and import it [using these instructions](https://developers.google.com/unity/instructions#install-unitypackage) on the Google APIs for Unity site.

## Support

To request features or report issues with the plugin, please use the [GitHub issue tracker](https://github.com/google/play-common-unity/issues).

## Related Plugins

Browse other [Google Play plugins for Unity](https://developers.google.com/unity/packages#google_play).
