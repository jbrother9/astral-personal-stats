# UI performance review (2026-10-31)

Both projects use the game's FairyGUI. Our profile entry is created once and reused; repeated discovery and style synchronization are the main avoidable work visible in static review.

- FindProfile walks the UI tree every second without visibility pruning or reuse of the previously found visible profile.
- Scan reapplies LayoutEntry and MatchEntryStyle every second while the own profile is visible, allocating a fresh TextFormat.
- Fit reapplies window position and scale every frame while open.
- NativeUI caches types and invoked methods, but its Get/Set member lookup paths do not cache fields/properties.

BAP's visible-branch pruning and metadata/window caches are useful design references. This does not establish that the reported combined-use crash was caused by these paths. No BAP code was copied. No UI optimization is included in 0.8.7: it adds operation-scoped timings, scan visit/allocation counters and bounded diagnostics first. CLR allocation and whole-game memory are not exclusively attributable to this plugin. Native rendering, asynchronous loading and diagnostic-write overhead are not completely included in own-operation timings.

Reference reviewed: https://github.com/TSM701/BetterAstralParty/tree/1.6.5 . Actual affected versions and logs remain necessary for diagnosis.
