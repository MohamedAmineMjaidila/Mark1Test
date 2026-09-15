# Native-resolution grass experiment

This implementation follows the approved S23 plan. The current storage backend is a complete immutable, bit-packed occupancy field. **There is no circular occupancy buffer.** Both draw paths share this field; a streaming backend can be evaluated later without replacing blade identity or shading.

## Ownership

`GrassRenderer` remains the scene owner. `GrassFieldData` is the editor-baked placement/height asset. `GrassExperimentProfile` selects a bounded compiled shader preset and either compacted IDs or direct grid submission. Runtime resource ownership is local to the renderer, with no global manager or service.

## Data contract

- 80 × 400 metres, 0.067 metre cells: 1,194 columns × 5,970 rows; complete cells only.
- Stable blade ID: `row * columns + column`.
- Occupancy: `words[row * ceil(columns / 32) + column / 32]`, bit `column & 31`.
- Row padding is zero; invalid cells are never read.
- Occupancy is 907,440 bytes. This excludes terrain heights, tile metadata and transient visible IDs.
- Height texture: linear RFloat, normalized terrain heights, bilinear clamp, explicit texel-center mapping.
- Tile: 32 × 32 cells, occupied count and conservative local terrain bounds.
- Painting bakes deterministic probability from mask coverage after the density threshold. No runtime mask/terrain editing or streaming is implemented.

## Validation status

Implementation and validation results are recorded here as each stage is completed. Source simplicity, Editor frame rate and Apple GPU timings do not establish S23 performance. Native-resolution, sustained device measurements are the acceptance criterion.

## Running and editing

1. Open `Assets/Grass/Benchmark/GrassBenchmark.unity`. The original `SampleScene` remains the legacy reference.
2. Select **Grass benchmark**. Its `GrassRenderer` owns the resident field and renderer; its `GrassBenchmark` owns the camera route and measurement.
3. Enter Play Mode. Previous/Next selects one of 54 profile assets: 27 presets × two draw paths. The camera uses the same field, seed, fixed angle, route, and simulation time in each comparison.
4. Start a sustained run. The first 300 seconds warm the selected configuration while moving through the field. Measurement then restarts the route at time zero and records 900 seconds. Controls hide during the run.
5. Results go to `Application.persistentDataPath/GrassBenchmarks/<UTC timestamp>/`: `run.json`, `profile.json`, and `frames.csv`. A missing GPU timing or counter is marked unavailable, never interpreted as zero cost.
6. Use the dense field switch for a full-density, 150 m stress configuration. Full-field population is **not** visible population: enable diagnostic counters separately to determine the actual submitted blade count at each pose. The intended ~1.5 million visible-blade workload still needs calibration from these counts.
7. To paint: use the GrassRenderer painting tools, then **Bake Field from Terrain and Painted Mask**. Re-bake after sculpting terrain too. A sculpted field uses texture height; the MathematicalHeight preset only accepts the matching analytic fixture.

The fixture creation menu is idempotent and preserves existing assets. Numeric profile edits update uniforms; changing topology, field identity, draw path, shader assets, or required list capacity rebuilds owned buffers. Field data is shown read-only in its Inspector to avoid changing spacing without rebuilding occupancy and tile bounds.

## Rendering paths

### CompactedIds (initial default; device winner not established)

The CPU visits the tile rectangle overlapping the camera's draw radius. It rejects empty tiles, tiles beyond radial range, and conservatively expanded bounds outside the camera frustum. A 256-thread compute group processes one 32 × 32 tile in four batches. Surviving cell IDs are compacted per LOD using group-local counts and one global reservation per nonempty LOD per batch. A final kernel writes typed indirect argument counts.

Each visible blade occupies **4 bytes** in a transient LOD list. The vertex stage reconstructs its root, height, orientation, stiffness, color and dryness from immutable data plus its stable cell ID. The lists are bounded conservatively for an offset camera and transition region; attempted counts and overflow counts remain available for diagnostics. Draw counts are clamped even if a defect causes overflow.

### DirectGrid (comparison path)

The CPU uploads tile IDs partitioned by overlapping LOD bands. Each tile submits 1,024 candidate instances. The vertex stage performs the same occupancy, distance, density, frustum and LOD selection. All vertices of a rejected blade receive one finite position outside the clip volume. This saves visible-ID generation and storage, but repeats selection for each mesh vertex and submits rejected candidates. It must be measured on S23 to judge the tradeoff.

Both paths issue three `Graphics.RenderMeshIndirect` calls to the explicitly assigned camera. Neither uses grass shadow passes, a depth prepass, fragment discard, synchronous runtime GPU readback, HiZ, or a circular buffer. The original renderer retains its existing reference shaders and HiZ path.

### Data and memory

| Resource | Layout / size |
| --- | --- |
| Resident occupancy | 32 cells per uint; 907,440 bytes per field |
| Resident terrain height | 513 × 513 RFloat; 1,052,676 GPU bytes in the fixture |
| CPU tile metadata | 7,106 entries; ID, occupied count, conservative Bounds |
| Uploaded tile IDs | 3 × 7,106 uint slots; 85,272 bytes |
| Compacted visible IDs | uint per capacity slot in each LOD; radius-rectangle upper bounds, not expected population |
| Direct path inert ID bindings | Three uint slots total |
| Diagnostic counters | Four uints; 16 bytes |
| Indirect arguments | Three `IndirectDrawIndexedArgs`; runtime platform size and instance-count offset |
| Blade meshes | 11 / 7 / 3 vertices and 9 / 5 / 1 triangles per instance by default |

The telemetry's `grassBufferBytes` covers occupancy, uploaded tile IDs, visible-ID capacities, counters and indirect arguments. It excludes height textures, mesh buffers, materials, CPU copies, terrain rendering and benchmark recording memory. Both field assets remain loaded in the benchmark to allow switching; only the selected field's occupancy is uploaded by the grass renderer. A production scene should reference only its needed field.

At 0.067 m spacing, storage replaces the old 24-byte record per potential blade with a bit plus shared height samples. That is a structural memory reduction, **not a measured frame-time claim**. Half precision is a shader arithmetic request; Vulkan may retain 32-bit storage with relaxed precision.

## Compiled experiments

The preset keyword is one mutually exclusive local set, avoiding an exponential cross-product of unrelated switches. Geometry, field, LOD distances, density and camera must stay fixed for a shading A/B. Numeric AO weights deliberately allow linear, squared and smoothstep blends without multiplying compiled modes.

| Concern | Presets / controls |
| --- | --- |
| Shape and wind | Optimized bounded polynomial bend; Bezier; OneWave; numeric macro wind, gust, flutter and stiffness |
| Height | Texture height; MathematicalHeight on the analytic fixture only |
| Normals | Default one rounded normal; FlatNormal; LegacyRoundness (dual normals); AnalyticRoundness |
| Color and dryness | Default smooth spatial patches plus blade variation; BladeColor; LegacyColor; BladeDryness; LegacyDryness |
| View | Shared fixed-camera direction; ExactView |
| Diffuse and ambient | Wrapped diffuse; Lambert; hemispherical ambient; AmbientSH; AmbientOff |
| Transmission | Cheap near/mid transmission; TransmissionOff; TransmissionLegacy |
| Specular | Fixed-power near specular; SpecularOff; SpecularLegacy |
| Stability | Per-LOD up-normal blend; StabilizationOff; StabilizationTerrain; StabilizationPatch |
| Other probes | Widening; FloatPrecision; Lod2Fragment; ReferenceShading; MinimalLighting |

`ReferenceShading` combines selected legacy lighting calculations within the new data/geometry pipeline. For the original complete implementation, use `LegacyReference` in the original scene. It is not valid to describe ReferenceShading as a bit-exact baseline of the starter renderer.

The default LOD0/LOD1 varyings, excluding `SV_POSITION`, are one normal (`half3`) plus blade height/color/dryness (`half3`): **six scalar components across two TEXCOORD registers**. Default LOD2 carries pre-lit `half3` color only. Dual/analytic normal experiments add four scalars; exact view adds world position; legacy fragment hash adds the root position. FloatPrecision changes arithmetic precision. Unused fields are compiled out. Actual registers, instructions and spills require the target shader compiler/GPU capture.

Shader programs: three LOD files × two paths × 27 presets = **162 declared graphics combinations**, before compiler deduplication and Unity platform processing. Selection has two height variants per kernel. The optimized-release build filter retains only the default preset (six graphics combinations), removes mathematical compute height and prunes the benchmark's selectable profile list accordingly.

## Builds and measurements

Menus:

- **Tools → GrassFlow → Build Android experiments**: non-development ARM64 IL2CPP Vulkan player with the full finite preset catalog.
- **Build Android optimized release**: same native rendering setup, experiment variants removed.
- **Run correctness tests**: Unity EditMode CPU/GPU suite. Save open scene changes first; the runner now checks this before Unity can open a blocking native dialog.

Build tools apply portrait orientation, disabled Android resolution scaling, Vulkan-only graphics, frame timing statistics and native URP scale, and restore the previous project settings afterward. Output: `Builds/Android/GrassFlow-experiments.apk` or `GrassFlow-optimized.apk`. The build includes only the benchmark scene. Runtime clones the benchmark pipeline and restores prior quality/FPS settings on exit.

Each tested preset receives its own five-minute route warm-up before its measured run. Compare runs with identical device brightness, refresh mode, charging state, resolution, thermal starting state and route. Record these external conditions alongside the export. Avoid counters and on-screen debug UI in the primary timing run. Capture diagnostic counts and grass-specific GPU timing separately. FrameTimingManager reports **whole-frame** GPU time, not the grass pass's time.

Provisional acceptance budgets:

- Sustained 60 FPS at fixed native portrait resolution on S23 or better.
- Grass CPU p95 ≤ 0.5 ms and zero steady-state allocations in `GrassFlow.SelectAndSubmit`.
- Grass GPU p95 ≤ 4 ms including selection, verified with target GPU profiling.
- Terrain + grass GPU frame ≤ 8 ms to leave room for later scenery and character work.
- No count overflow, missing tiles, unstable LOD boundaries, obvious grid patterns, or unacceptable temporal shimmer at native resolution.

The CPU/GC marker encloses tile selection, uniform updates, dispatch and draw submission. Owner validation and the benchmark recorder are outside it. The recorder preallocates its sample array; exporting/sorting happens after measurement. Editor or Apple GPU results establish correctness only. Device thermal traces, target compiled shader statistics, image review, and the visible-blade stress calibration are still required to accept these budgets.

## Verified so far / remaining

- Unity imported the initial runtime, Editor and test assemblies after fixing installed-URP API differences.
- Initial `ShaderUtil.GetShaderMessages` returned no messages for the three LOD shaders; this is not exhaustive variant or device validation.
- The independent serialized-asset check verified dimensions, all padding bits, occupancy popcounts, and the sum of 7,106 tile populations for both saved fields.
- Representative field: 5,025,730 occupied cells. Dense field: 7,128,180 occupied cells. Each occupancy map is 907,440 bytes.
- Android Build Support and all SDK/NDK/JDK dependencies were installed successfully for Unity 6000.6.0f1.
- All five current C# assemblies compiled successfully using Unity’s bundled Roslyn compiler and the project’s generated reference/define lists, with outputs isolated in `/tmp/grass-compile`. This includes the latest build tools and Editor changes.
- Full Unity test results, rendering captures, build results and physical S23 measurements are pending. The test launch encountered a native save-scene dialog; Unity also reported a revoked Personal license. Neither is evidence of a grass GPU failure.

## Known boundaries

This is the grass rendering foundation and benchmark, not the finished scenery. Character movement, flowers, trees, rocks, leaf simulation and their combined budget come later. Terrain holes are not currently baked into occupancy; paint those regions out. Terrain/grass origin assumes an unrotated, unit-scale Unity Terrain. The optimized lighting is intentionally explicit and does not yet integrate arbitrary scene shadow receivers or additional local lights. The device A/B must determine whether direct submission, compute compaction, normal approximations, density and LOD choices meet the required visual quality and performance.
