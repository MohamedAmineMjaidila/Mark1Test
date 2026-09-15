# GrassFlow: scene setup, algorithm selection and tuning

## Start here — your existing scene

1. Open `Assets/Scenes/SampleScene.unity`.
2. Select **GrassSystem** in the Hierarchy.
3. In **Grass Renderer**, select **Rendering Mode → Immutable Field**.
4. **Experiment Profile** chooses the saved profile asset. The integrated scene uses `Assets/Grass/Scenes/SampleScene/GrassSystem Tuning.asset`.
5. Expand **Active profile — algorithms and numeric tuning**, then **Algorithms**.
6. Change any algorithm dropdown. Choices are independent: for example **Bending → Cubic Bezier**, **Wind → One Wave**, **Normals → Analytic Fragment**, **Stabilization → Terrain**, and **Specular → Off** can run together.
7. Wait for **Specialized shaders ready**. A new combination may pause briefly while Unity compiles its shaders. Numeric sliders do not compile shaders.
8. Enter Play Mode to inspect the result. Profile asset edits persist after Play Mode; GameObject/component edits during Play Mode generally do not. Save the project to persist asset edits to disk.

**Duplicate profile for a new experiment** copies the selected asset and assigns the copy. Use this before changing a working look. The circle beside **Experiment Profile** opens Unity's asset picker; dragging a profile asset into that field also selects it. Editing a shared profile affects every renderer referencing it.

If another scene still uses Legacy Reference, assign its Terrain and camera, then click **Set up immutable field and editable profile on this object** outside Play Mode. This bakes its mask and terrain into a new field and creates an editable profile. Save that scene afterwards.

**Starting recipe → Apply algorithms** fills the independent algorithm dropdowns. It preserves numeric tuning. Recipes are shortcuts, not mutually exclusive renderer modes. Applying another recipe replaces all algorithm choices, so duplicate your profile first if you want to keep a combination.

The Inspector hides numeric settings that do not apply to the chosen algorithm. For example, **Specular Power** appears only for **Specular → Power**. There is no need to edit shader source to select any implemented choice below.

## What is connected, and what changed

The scene had remained on Legacy Reference with no baked field/profile assigned. Its saved near/middle/far distances were 80/40/100 m, making the middle LOD unreachable. The integrated profile uses ordered 15/40/100 m distances and 5/3/1 ribbon segments. This changes geometry cost and appearance; it is not an equal-quality performance comparison with the previous 15/1/1 setting.

Your current SampleScene TerrainData is **70 × 70 m**, with 40 m vertical range. The separate reproducible benchmark is **80 × 400 m**. Scene integration preserves the existing terrain, painted mask, density falloff (10 m), minimum density (0.01) and width compensation (0), and derives cell spacing from the old grid density. It does not enlarge or flatten your terrain. The new placement is stable cell-based placement, so individual blades will not match the old procedural generator exactly.

The new renderer uses three LOD draw submissions to its assigned camera, casts/receives no grass shadows, and does not implement Hi-Z occlusion. Legacy controls for these features do not secretly enable them on the immutable renderer. Scene lights can still cast shadows for other objects.

Numeric edits update uniforms. Algorithm changes replace the three materials; segment changes replace the affected ribbon meshes. These changes preserve resident occupancy and visibility buffers. Changing the field, draw path, profile asset, or increasing required visibility capacity may rebuild buffers. Painting the mask does not re-upload occupancy until Bake Field.

An initialization error in this Unity version was also fixed: `IndirectDrawIndexedArgs.instanceCount` is an auto-property, so looking it up with `Marshal.OffsetOf` threw before rendering. `GrassIndirectLayout` now locates the value using a one-time blittable layout probe. GPU correctness tests validate the resulting draw counts.

The debug overlay now reports the active backend/profile and actual immutable-renderer statistics. Disabled/pending GPU counters are labeled as unavailable instead of presenting stale legacy counts. Keep the overlay and counters off for timing runs.

## Choose the draw path

| Draw Path | Selection work | Use |
|---|---|---|
| **Compacted Ids** | CPU rejects empty/outside tiles. Compute checks cells and writes surviving 32-bit cell IDs into three LOD lists. Vertex shaders consume those IDs. | Starting point for dense fields and sparse masks; avoids drawing rejected candidates. |
| **Direct Grid** | CPU prepares candidate tiles per LOD. Vertex shaders derive cell IDs, read occupancy and reject candidates themselves. No visibility-compaction dispatch. | Compare on the actual target GPU. Rejected candidates still run selection per mesh vertex; it can be slower for sparse fields or detailed near blades. |

Both paths read the same immutable occupancy field. Tiles are 32×32 cells and are a selection unit. **Neither path is a circular buffer.** The full field stays resident. A streaming/circular backend remains a separate future task.

## Independent algorithm reference

The controls below are inside **Active profile → Algorithms**, or directly on any Experiment Profile asset.

| Control | Choices | What changes / where it executes |
|---|---|---|
| Bending | Normalized Curve / Cubic Bezier | Vertex centerline and tangent calculation. Normalized curve pins the root and bounds blade length; Bézier evaluates a cubic centerline and derivative. |
| Wind | Two Waves / One Wave / Off | Vertex wind: one or two main sine waves, plus configurable gust/flutter and per-blade stiffness. Off compiles wind work out; natural bend remains. |
| Height | Baked Texture / Mathematical Fixture | Root height from the baked height texture, or the benchmark's exact analytic formula. Mathematical mode requires an analytically compatible field and is rejected for sculpted scenes. |
| Normals | Rounded Vertex / Flat / Dual Normal Fragment / Analytic Fragment | Vertex-rounded normal, ribbon-plane normal, interpolated pair of rotated normals, or per-fragment angular rounding. Analytic mode combines with Up/Terrain/Patch/Off stabilization. |
| Color Variation | Smooth Patch And Blade / Per Blade / Legacy Fragment Hash | Stable mixed patch/blade noise, blade hash only, or position hash in the lighting stage. |
| Dryness | Smooth Patch And Blade / Per Blade / Legacy Fragment Hash | Independently chooses the source of dry-color variation. |
| View | Shared Camera Direction / Per Fragment | Shared view and half-vector, or local view from world position. Far vertex lighting evaluates the local view at vertices. |
| Diffuse | Wrapped / Lambert / Unlit | Wrapped back-facing response, clamped N·L, or base color only. Unlit is a diagnostic comparison; ambient/specular/transmission/AO controls do not affect it. |
| Ambient | Hemisphere / Spherical Harmonics / Off | Sky/ground blend, Unity SH environment lighting, or no ambient. |
| Transmission | Cheap Backlight / Power / Off | Near and middle only. Cheap backlight uses negative N·L; Power adds a view-dependent exponent. |
| Specular | Fixed Power 8 / Power / Off | Near LOD only. Fixed Power 8 uses multiplies; Power uses the editable exponent. |
| Stabilization | Up / Terrain / Patch / Off | Blend toward up, four-sample terrain normal, a stable patch direction, or skip stabilization. Terrain costs additional height samples. |
| Far Lighting | Vertex / Fragment | Far color at vertices with color interpolation, or full lighting stage per fragment. |
| Precision | Half / Float | Precision of shading intermediates and varyings. Positions/grid addressing stay float/integer. Hardware/compiler determines the actual benefit. |
| Ambient Occlusion | Weighted Curves / Legacy | Weighted height curves or the reference height-based darkening formula. This is blade-root shading, not screen-space AO. |
| View Widening | Off / On | Adds camera-facing width near edge-on angles, controlled by Widening. |

### LOD-specific limits

Near and middle lighting run per fragment. Far lighting defaults to vertex color. With far vertex lighting, fragment-normal choices use rounded vertex normals and legacy hashes run at vertices; select **Far Lighting → Fragment** to evaluate those algorithms per pixel in the far LOD. Specular remains near-only and transmission near/middle-only even when far lighting is Fragment. These are explicit workload limits in the current implementation.

Algorithm choices are shared across the three LODs; segment count and stabilization strength have separate near/middle/far values. Arbitrary independent algorithms per LOD are not currently a separate UI feature.

## Numeric tuning reference

### Geometry and coverage

- **LOD 0 Distance / LOD 1 Distance / Max Draw Distance**: near-to-middle boundary, middle-to-far boundary, and culling radius in metres. Must satisfy `0 < near < middle < far`.
- **LOD 0/1/2 Segments**: 1–15 ribbon segments. Each blade uses `2 × segments + 1` vertices and `2 × segments − 1` triangles. Increasing near detail multiplies work for every visible near blade.
- **Blade Heights X/Y**: minimum/maximum height in metres, derived deterministically from cell ID and seed.
- **Blade Width**: root width in metres, tapered to zero at the tip.
- **Natural Bend**: static bend amount, independent of whether wind is enabled.
- **Density Falloff Start**: distance at which deterministic density thinning begins.
- **Minimum Density**: fraction retained near the far radius. `1` disables thinning; it does not fill empty occupancy cells.
- **Width Compensation**: amount of widening to compensate for thinning.
- **Maximum Width Scale**: cap on that compensation.
- **Transition Width**: stochastic LOD blend width in metres; clamped to fit the distance bands. Each selected cell belongs to one LOD, avoiding overlapping duplicates.

For a shader-only A/B test, freeze all of these and the field/camera/resolution. Reducing segments, density or distance is a quality/workload change and should be reported separately.

### Wind

- **Wind Direction X/Y**: world X/Z direction; normalized before upload.
- **Wind Strength / Wind Speed**: amplitude and time multiplier.
- **Wave A / Wave B**: X/Y are spatial frequencies in radians per metre along world X/Z; Z is temporal speed multiplier; W is phase offset in radians.
- **Second Wave Weight**: blend between A and B in Two Waves mode.
- **Gust Strength**: amplitude modulation from the low-frequency gust wave.
- **Flutter Strength**: amplitude of the high-frequency per-blade term.
- **Gust Wave**: X/Y spatial gust frequencies, Z gust time multiplier, W flutter time multiplier.
- **Stiffness Range X/Y**: minimum/maximum divisor for deterministic blade stiffness; upload clamps each to at least 0.05.

### Color, normals and lighting

- **Root / Mid / Tip Color**: blade color gradient; **Dry Color** is blended by dryness.
- Numeric **Color Variation**: amount of color variation; the same-named Algorithms dropdown selects its noise source.
- Numeric **Dryness**: dry-color coverage/strength; its Algorithms dropdown selects the noise source.
- **Patch Size Cells**: smooth color/dryness patch scale in grid cells. World scale is approximately this value × cell spacing.
- **Color Patch Weight / Dryness Patch Weight**: patch-versus-blade mix, independently.
- **Legacy Noise Frequency**: world-space frequency for the reference hash.
- **Roundness Radians**: angle used for normal rounding. Flat mode ignores it.
- **Normal Stabilization X/Y/Z**: near/middle/far blend strength, clamped 0–1. Off ignores it.
- **Sky Ambient / Ground Ambient**: hemisphere colors; SH/Off ignore these colors.
- **Root Darkness**: root light multiplier in Weighted Curves AO (1 means no root darkening). Legacy AO uses the corresponding reference strength mapping.
- **AO Curve Weights X/Y/Z**: linear `t`, squared `t²`, smoothstep `t²(3−2t)` weights. Negative weights clamp to zero; weights normalize on upload. All-zero falls back to squared.
- **Transmission / Transmission Power**: strength and optional exponent for near/middle backlight.
- **Specular / Specular Power**: strength and optional exponent for near highlights. Fixed Power 8 ignores Specular Power.
- **Widening**: view-dependent extra width when View Widening is enabled.
- **Collect Counters / Counter Interval**: occasional asynchronous GPU count readback in Compacted Ids. Direct Grid has candidate counts, not exact surviving blade counts. Counters are diagnostic overhead; keep off for final measurements.

## Occupancy: file → CPU memory → GPU

The occupancy matrix is real baked data, stored inside a Unity **GrassFieldData `.asset`**, not hard-coded placement in a shader and not a separate hand-loaded `.bin` file.

1. **Bake inputs**: TerrainData, painted density mask, Bake Cell Spacing, Bake Seed and Density Threshold.
2. **Bake**: `GrassFieldBaker.Bake` samples mask coverage per cell and makes deterministic occupancy decisions. Empty bits remain zero. It also writes tile occupancy counts/bounds and a normalized RFloat height-texture subasset.
3. **Disk**: Unity serializes `occupancyWords` inside the field `.asset`. Text serialization displays the packed array as a long encoded numeric payload; it will not look like a readable `bool[,]` grid.
4. **CPU**: the scene's serialized Field Data reference causes Unity to load the asset and its `int[]` words. Each 32-bit word holds 32 adjacent cells.
5. **GPU**: `GrassFieldRenderer` creates a structured buffer and calls `occupancy.SetData(field.Words)` once when constructing field resources. CPU signed words retain their bit pattern when read as GPU `uint`.
6. **Read**: `GrassOccupied` in `GrassField.hlsl` tests `word & (1u << (column & 31))`. Compute selection and direct-grid vertex selection both use it.

Addressing: `cellId = row × columns + column`; `wordIndex = row × ceil(columns / 32) + column / 32`. Padding bits at row ends are zero. Cell positions are stable across camera movement.

**See the matrix:** select the field asset, click **Export occupancy PNG (one pixel per cell)**. White is occupied, black is empty, image X/Y map to terrain X/Z. This export is an inspection image; painting it does not automatically update the field.

**Change placement:** outside Play Mode, paint the mask on GrassSystem, click Save Mask, adjust Bake Cell Spacing/Seed/Threshold if desired, then **Bake Field from Terrain and Painted Mask**. Sculpting terrain also requires rebaking heights. Save the scene if you assigned a new field. Bake changes are asset changes and affect every scene using that field.

Smaller spacing increases candidate cells approximately with the inverse square of spacing. Changing the seed changes occupancy probability and blade variation. Null density mask means full coverage. The current baker does not exclude Terrain holes automatically; paint those cells out of the mask before baking.

## Benchmark workflow and performance claims

The Mac iteration target is **150 FPS**, approximately **6.67 ms per frame**. The physical S23 target is sustained **60 FPS**, approximately **16.67 ms per frame**, with room for other scene work. These are acceptance targets, not verified guarantees. A fixed 2.5× conversion from Mac FPS to S23 FPS is not valid.

1. Save your current scene and open `Assets/Grass/Benchmark/GrassBenchmark.unity`.
2. The benchmark component's **Profiles** array contains prepared assets. **Selected Profile** selects the entry, including live changes during Play Mode; Previous/Next in its runtime controls call the same selection function.
3. To compare a custom combination, duplicate a profile asset, change Algorithms/numbers, then add it to Profiles outside Play Mode. Store both A and B there.
4. Run portrait Game view at a fixed resolution. The benchmark clones its URP asset with Render Scale = 1 and disables v-sync. Desktop is now **uncapped**; Android requests 60 FPS.
5. Edit source profile assets to tune while playing. The benchmark synchronizes them into its runtime copy and stops an active measurement on a source revision. Dense Stress uses a runtime copy with minimum density = 1 and max distance = 150; it does not modify your source asset.
6. Freeze settings, disable counter capture, and run the warmup/measurement route. Defaults are 5 minutes warmup and 15 minutes measurement. Do not include shader compilation, baking or Inspector manipulation in timings.
7. Read exports under `Application.persistentDataPath/GrassBenchmarks`: frame CSV, run metadata and full serialized profile. Missing GPU samples remain missing; they are not zero milliseconds.
8. Use **Tools → GrassFlow → Build Android experiments** for prepared comparisons. **Build Android optimized release** keeps the benchmark component’s **Release Profile**. If that field is empty, it keeps **Selected Profile** from the Profiles array. Any independent algorithm combination is supported; the starting recipe name does not affect inclusion. The build tool currently builds the benchmark scene, not SampleScene.
9. Validate on the physical S23 at the chosen native portrait resolution and thermal steady state. Editor measurements cannot certify mobile performance.

Mac Editor CPU frame time can contain waits for GPU/v-sync, plus Scene view, Inspector and Editor work. A CPU frame-time increase does not prove grass CPU submission became equally expensive. Compare the **GrassFlow.SelectAndSubmit** CPU marker and **GrassFlow.SelectTiles** GPU sample alongside whole-frame timing. The renderer's submit CPU statistic does not include the rest of Unity.

See [the validation report](Documentation/Configuration_Validation.md) for the current short-run measurements. The previous 19–20 ms GPU report cannot be attributed precisely without an equivalent capture. Confirmed issues were the scene still using legacy, unreachable middle LOD, misleading old overlay, profile copies hiding live edits, and broad rebuilds. Fixing those is not proof of 150 FPS; actual validation results are recorded separately.

## Architecture and exact editing entry points

You only need code changes to add an algorithm that does not exist yet. Existing options use dropdowns.

| Responsibility | File / function |
|---|---|
| Scene owner and backend switch | `Assets/Grass/Assets/Scripts/GrassRenderer.cs` → `LateUpdate`, `RenderField`, `ConfigureExperimental` |
| Independent choices and recipe migration | `Assets/Grass/Assets/Scripts/Experimental/GrassAlgorithms.cs` → `Defines`, `FromPreset` |
| Saved numeric controls / validation | `Assets/Grass/Assets/Scripts/Experimental/GrassExperimentProfile.cs` → fields, `NotifyChanged`, `IsValid` |
| Profile Inspector | `Assets/Grass/Assets/Scripts/Experimental/Editor/GrassExperimentProfileEditor.cs` → `OnInspectorGUI`, `IsRelevant` |
| Automatic shader specialization | `Assets/Grass/Assets/Scripts/Experimental/Editor/GrassProfileCompiler.cs` → `Compile` |
| Allocation vs uniform/material updates | `Assets/Grass/Assets/Scripts/Experimental/GrassFieldRenderer.cs` → constructor, `NeedsRebuild`, `ApplyProfile`, `Render` |
| Vertex stage entry point | `Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl` → **`GrassVertex`** |
| Fragment stage entry point | same file → **`GrassFragment`** |
| Bending / wind / stabilization | `Includes/GrassGeometry.hlsl` → `GrassBend`, `GrassWindOffset`, `GrassStableTarget`, `GrassStabilize` |
| Lighting / color / dryness | `Includes/GrassLighting.hlsl` → `GrassShade`, `GrassBladeData`, `GrassPatch` |
| Occupancy / height / selection | `Includes/GrassField.hlsl` → `GrassOccupied`, `GrassRoot`, `GrassSelect` |
| GPU ID compaction / arguments | `Assets/Grass/Assets/Shaders/Experimental/GrassSelect.compute` → `SelectTiles`, `BuildArguments` |
| CPU bit layout / immutable storage | `GrassGrid.cs` → `SetOccupied`, `Occupied`; `GrassFieldData.cs` → `occupancyWords`, `IsOccupied` |
| Editor field writer | `Editor/GrassFieldBaker.cs` → `Bake` |

`Includes/` above is relative to `Assets/Grass/Assets/Shaders/Experimental/`. Runtime data files above are under `Assets/Grass/Assets/Scripts/Experimental/`.

The actual selected near/middle/far shader assets live in `Assets/Grass/Generated/Shaders/`. Each contains `#pragma vertex GrassVertex`, `#pragma fragment GrassFragment`, a fixed `GRASS_LOD`, and defines for exactly the selected combination. Matching profiles share the generated trio. Each shader retains only the two draw-path variants. This avoids an exponential cross product of every independent dropdown in the build.

Do not hand-edit generated wrappers. Edit the shared implementation to add a new algorithm, add an enum choice and its define in GrassAlgorithms, implement the corresponding HLSL branch, then add a combination test. Commit the profile asset and generated shader assets plus their `.meta` files. Scripts that edit numeric profile fields should call `NotifyChanged()` afterwards. Runtime players switch prepared profiles; they do not generate shader source or compile newly invented configurations on device.

Original reference files remain `Assets/Grass/Assets/Shaders/GrassShader.shader` (both original vertex and fragment functions) and the legacy compute shader. Selecting a new profile does not change those reference shader algorithms.

## Troubleshooting

- **Old UI:** select GrassSystem's Grass Renderer, not its material or Terrain; check Rendering Mode = Immutable Field. Compilation errors prevent new Inspector code from loading. Re-select the object after compilation.
- **Pending specialization:** wait for Editor compilation. Use Tools → GrassFlow → Compile profile algorithms if needed, then check Console. A pending combination temporarily skips grass submission so mismatched height/selection shaders are not mixed.
- **No grass:** verify field/profile/camera/Terrain references, camera over the field, valid ordered distances, nonempty baked occupancy, and texture-height mode for sculpted terrain.
- **Changed mask but unchanged placement:** Save Mask and Bake Field outside Play Mode.
- **Profile edits disappear:** a runtime copy was edited instead of the source asset. Use the Project asset or the normal scene's embedded persistent profile. Benchmark source edits intentionally stop measurements.
- **Unexpectedly slow Direct Grid:** compare Compacted Ids with identical coverage and geometry; direct selection runs per candidate vertex.
- **Validation:** Tools → GrassFlow → Run correctness tests. Save dirty scenes first; the runner checks for this before starting. Results go to `Logs/GrassValidation/EditMode.xml`.
