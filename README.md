# SensoryRoomHelper

This is part of my Bachelor's Thesis, carried out with the ViRVIG group of the UPC.

It is a Unity project that builds to an Android application, intended for a tablet. It is the helper application: it connects to the main [SensoryRoom](https://github.com/DarkJaslo/SensoryRoom) app, running in virtual reality, to customize settings from outside.

The Unity version used is `6000.0.59f2`.

### SensoryRoomCommon

This repository has a submodule called [SensoryRoomCommon](https://github.com/DarkJaslo/SensoryRoomCommon), where any code that is shared with [SensoryRoom](https://github.com/DarkJaslo/SensoryRoom) is located. When cloning this repository, use the `--recurse-submodules` flag:

```sh
git clone --recurse-submodules
```

To work with this project, I recommend understanding git submodules first to avoid surprises.