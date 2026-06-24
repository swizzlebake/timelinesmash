# TimelineSmash

> Collaborative cinematic assembly for Unity Timeline — build long, intricate cinematics as a team
> **without merge conflicts**.

This repository is a Unity 6 project that **develops and hosts** the TimelineSmash package, embedded at
[`Packages/com.swizzlebake.timelinesmash`](Packages/com.swizzlebake.timelinesmash). The package is the
deliverable; the project around it is the development + test harness.

## Install (in your own project)

Package Manager → **Add package from git URL…**:

```
https://github.com/swizzlebake/timelinesmash.git?path=Packages/com.swizzlebake.timelinesmash
```

Pin a version by appending a tag, e.g. `#0.3.0`. Requires Unity **6000.3+** and `com.unity.timeline`;
`com.unity.recorder` is optional and enables video / image-sequence export.

## What it does

- Each artist authors isolated **sub-timelines** (`.playable`) and a per-artist **contributor file** —
  one owner per file, so parallel work merges cleanly.
- A deterministic **assembler** flattens the composition into one native master `TimelineAsset` plus a
  regenerable stage scene. The master is regenerated, never hand-merged.
- **Nested compositions** to any depth (flattened at assemble time → no runtime desync) and **grouping**
  of segments into reusable, movable sub-compositions.
- **Composable binding manifests** — split bindings across files; they compile into one master lookup at
  assemble time (first definition of a key wins).
- Preview/scrub/play in the native **Timeline window**; optional **Unity Recorder** export.

Full documentation lives with the package:
- [Package README](Packages/com.swizzlebake.timelinesmash/README.md) — concepts + workflow
- [Architecture notes](Packages/com.swizzlebake.timelinesmash/Documentation~/index.md)
- [Changelog](Packages/com.swizzlebake.timelinesmash/CHANGELOG.md)

## Develop / test

Open this project in Unity **6000.3.10f1**. Run the EditMode tests via **Window ▸ General ▸ Test Runner ▸
EditMode**, or headless against a project that embeds the package:

```
<UnityEditor> -batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode \
  -testResults ./TestResults.xml -accept-apiupdate
```

See [CLAUDE.md](CLAUDE.md) for development conventions and gotchas.

## License

MIT — see [LICENSE](LICENSE).
