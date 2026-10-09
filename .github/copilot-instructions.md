# DataMiner Version Pinning

Never set or raise `MinimumRequiredDmVersion` to a DataMiner Feature Release solely because a referenced SDK, toolkit, or installer package uses that version.

Before changing `MinimumRequiredDmVersion`, require either:

- the target DMA's exact reported version string, converted to the SDK-required `A.B.C.D-buildNumber` format when necessary; or
- an explicit user-approved Main-release floor.

If neither is available, do not change the property. Ask the user.

Keep package and contained artifact version floors aligned unless a documented runtime dependency requires a higher floor.