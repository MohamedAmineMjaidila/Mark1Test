# Configuration repair and validation — 2026-09-15

## Delivered

- SampleScene's GrassSystem now references a baked field and a persistent, embedded tuning profile.
- Independent, combinable algorithm dropdowns replace the mutually exclusive preset workflow. Existing recipes migrate to independent settings.
- Selected combinations generate specialized shaders automatically; numeric edits update uniforms and preserve field buffers. Mesh/material edits also preserve those buffers.
- Fixed indirect argument initialization for Unity 6000.6: the previous field-offset lookup throws for `instanceCount`, which is an auto-property. A one-time layout probe now supplies the compute shader's byte offset.
- Analytic fragment normals respect Terrain/Patch/Up/Off stabilization choices.
- The mode-aware overlay no longer displays stale legacy counts on the immutable backend.
- Bake spacing/seed are exposed, and occupancy can be exported as an exact binary image.
- Desktop benchmark no longer requests a 60 FPS cap. Source profile edits synchronize into its runtime copy and interrupt measurement; numeric edits reuse that copy.
- Scene setup preserves the user's saved density falloff instead of silently applying the benchmark's denser defaults.

## Correctness

Unity 6000.6.0f1, Metal, Apple M5: **13 tests passed, 0 failed, 0 skipped**.

The suite checks grid dimensions/addressing/padding, deterministic identity, LOD boundaries, visibility capacity, GPU selection versus CPU expectations, overflow protection, migrated profiles, combined shader compilation, and buffer preservation during numeric/material/mesh changes.

Results: `Logs/GrassValidation/EditMode.xml` and `EditMode-summary.txt`. Shader tests exercise three combined configurations with analytic/dual normals, stabilization choices, Bézier, wind off, exact view, legacy color hash, per-blade dryness, power specular, fragment far lighting, float precision and widening, across all three LODs and both paths. This is representative combination coverage, not an exhaustive enumeration of every possible configuration.

The Metal compiler reported “potentially uninitialized variable” warnings in shared selection code in the GPU tests. Both occupancy-boundary and overflow tests passed. No shader compilation errors were reported. These warnings remain to investigate across compiler backends.

## Short scene measurements

These are diagnostic samples, not sustained performance certification. Output was **1440×2560 portrait**, active URP render scale **1**, v-sync **0**, requested desktop FPS **uncapped**, on Apple M5. Camera and terrain stayed fixed. FrameTimingManager measures the **whole rendered frame's GPU work**, not just grass.

| Configuration | Samples | Median GPU | Median grass submit CPU | Submit CPU p95 | Measured grass managed allocation |
|---|---:|---:|---:|---:|---:|
| New compacted, initial denser defaults | 300 | 10.84 ms | 0.128 ms | 0.158 ms | 0 bytes/frame |
| Direct grid, same initial numeric settings | 180 | 11.76 ms | 0.133 ms | 0.140 ms | 0 bytes/frame |
| Current saved legacy settings | 180 | 11.18 ms | Not instrumented | Not instrumented | Not instrumented |
| Grass disabled | 120 | 2.84 ms | — | — | — |
| **Compacted, saved density settings restored** | **180** | **7.22 ms** | **0.113 ms** | **0.123 ms** | **0 bytes/frame** |

The last row is the final profile's starting configuration: density falloff 10 m, minimum density 0.01 and width compensation 0; LOD distances 15/40/100 m, segments 5/3/1. It uses the optimized algorithm defaults. New and legacy geometry, placement and shading differ; the legacy row is not a shader-only equal-quality comparison. Initial denser defaults used falloff 30 m, minimum density 0.1 and width compensation 0.3.

The immutable field has 1,498,176 candidate cells, 1,408,081 occupied cells, and 964 candidate tiles at this camera pose. Compacted GPU buffer allocation was 13,540,280 bytes (12.91 MiB); direct-grid buffers were 209,284 bytes (0.20 MiB). This excludes height textures, meshes, terrain, materials and CPU copies. Visible blade counts are not the same as occupied field population; counters were disabled during timing.

The Editor produced approximately **100 ms frame intervals despite being uncapped**, consistent with Editor/background throttling or waiting. Therefore these runs **do not establish achieved FPS**, and their GPU timings may differ from a fully busy foreground/player run. The roughly 7.22 ms GPU sample also remains above the 6.67 ms budget for 150 FPS. No claim of meeting that target is made.

Subtracting the no-grass sample suggests grass accounts for roughly 4.4 ms of the final frame under these conditions. That subtraction is only a coarse diagnostic: it is not a GPU pass measurement and changing work can change GPU clocks and scheduling.

Raw JSON and inspected portrait captures: `Logs/GrassValidation/SampleScene-{Compacted,DirectGrid,Legacy,NoGrass,MatchedDensity}.{json,png}`. The short-probe JSON exposes zero submit statistics for Legacy because that path lacks this instrumentation; use the table's “not instrumented” interpretation, not zero CPU cost.

## Remaining acceptance work

- Foreground Mac/player profiling without the observed 100 ms frame pacing, including grass-specific GPU pass timing.
- Physical S23 native-portrait sustained measurements, thermal stability and full-scene workload budget.
- Completed Vulkan/Android build and on-device shader validation. Metal correctness does not certify Vulkan.
- Visual approval of LOD/lighting differences and temporal behavior. The captured grass is present and non-magenta; that does not establish AAA final-scene quality.
- Character, flowers, trees, rocks, leaves, scene shadows and other future workloads are not implemented by this change.

Use [the tuning guide](../README.md) for every algorithm, parameter, profile workflow and source entry point.
