Prompt for codex for an overview of the intended futur changes for the project: 
What i am giving you here is the prototype for the grass rendering project, but the main point is that it is a project that needs tweeking of algorithms, code, variables and calculations etc... to reach a full optimization and the benchmark that i want which is AAA grade anime realistic. Therefore at many stages or levels of the code or project, we need to be able to switch seamlessly the algorithms, code, or variables responsible for the rendering, so in this prompt i will give you a "$$(i)" sign under any technical choice that will need an adaptable code to change the solution easily, (i) is just a way to label that technical dilemma or choice or that something can be tweaked ( i don't need that sign for variables because they could be changed easily, i am more talking about algorithms or high level choices.)
1- Grass Representation 
- Deterministic low-storage reprensentation of grass : 
    - The full distribution of the grass blades in the terrain will be determined by a big boolean matrix called GrassOriginalMatrix
        - one cell in the matrix is equivalent to 0.067 meters
        - the terrain is 80 meters in width and 400 meters in length
        - 1 means we will render a grass blade in the middle of that cell, 0 means no grass blade there
        - in the vertex shader we will not be using this full matrix, but a circular buffer below, and the test is somehow gonna be to just iterate over the number cells of the circular buffer, and each time we find the value 1 we decide to render, and the algorithm to iterate over the circular buffer is the euclidian division and rest for the row number "r" and column number "n".
    - Caring about only the grass in the frustum we will need a circular buffer called grassCircularBuffer : 
        - Attributes of the circular buffer data structure : 
            - windowSize.x x windowSize.y boolean matrix that represents as small window in the grassOriginalMatrix
            - the number columns of the boolean matrix is actually the same as the grassOriginalMatrix but not the number of rows
            - originalCursor, which is the row in the originalGrassMatrix in which the window starts
            - that matrix is not representative a 100% of the reachable camera window because it contains a margin
            - startCursor is the row in which we start rendering blades in the window, why not 0, well because this circular buffer is gonna be used with a margin, as we are afraid because of the parallel processing of frames, that this shared circular buffer in gpu memory will somehow be updated before we have the time to do the shader calculations, and as the first row is the one susceptible to be re-written when there is a new frame, we can't be using those rows to render, therefore those first rows are margins (the first 128 rows will not be used, for example they will contain grass behind the camera), therefore the start cursor is the element needed to keep track of what row we need to render first (so we don't render grass behind the camera). so normally the cell of the grass blade is : matrix(r+startCursor,n).
            - The problems of synchronization are gonne be solved this way, we do know that the circular buffer is susceptible to change with every frame, therefore if our gpu rendering is still going and the cpu is ahead of the gpu, we might have a problem, that's why we will actually get the original cursor and the start cursor as frame variables for the gpu vertex shader as constant params let's calle them originalCursor(n) and startCursor(n) and each time we are actually fetching the window to check if we need to render the grass we are not getting only the matrix(r+startCursor,n) that's wrong, i just put it as explanation before, but we need to first get originalCursor(n+k) and startCursor(n+k) i put k becuse we don't know how much the gpu circular memory buffer was updated and how much the cpu is ahead, and we need to do a substraction to get d = originalCursor(n+k) and startCursor(n+k) - originalCursor(n) + startCursor(n) and therefore we need to access the matrix in : matrix(r+startCursor-d,c)
        - Therefore if the camera advances we need to update the window, instead of all the time deleting the first raw and writing the last row and keeping the margin always to 128, we are gonna wait until the margin is lesser than 
2- LODs: 3 gpu draws for each LOD, and they are all going to be using the circular buffer, but for LOD1, we are going to add a constant called LOD1Distance which is an integer that will be added to the startCursor, same thing for the LOD2.
3- Shadows : i think in the grassflow project without even casting shadows the shadow-illusion is almost perfect, as the grass is somehow drawn at the root in a darker shade and the tip more light, i think therefore that illusion is sufficient, so i need you to disable the shadows for now, so delete any trace of the shadows in the vertex for now.
4- Grass vertex shader : 
    - frame constant inputs are : camera position, time, startCursor and originalCursor of the grassCircularBuffer (each time there is a new frame they are updated and at the same time passed as constant parameters for the gpu program, they serve as a chronological reference to the frame, because the grassCircularBuffer can be updated and they can be modified it is very important to get a fixed instant value of them when the frame is first defined, so that we render that frame information and not the next frames infos ( as the grassCircular buffer is shared for many gpu programs therefore many frames), finally the instant grassCircularBuffer is going to be called from memory, texture for the terrain is going to be called from memory, 
    - instead of using if and elses, we need the same programm for all of the gpu calculations, because chatgpt said gpu thrives in repeated calculations not ones that different from instance to instance
    - so we get the position x and y from instance count as i said before wiht the euclidian division and rest.
    - we do the occupancy test by first getting the instant startCursor(n+k) and originalCursor(n+k) from memory then we access that grassCircularBuffer by doing this : matrix(r+startCursor-d,c). 
    - then we get the height of the vertex in a random low-volume calculaiton maybe hash and also use the terrain texture for the real height coordinate. $$(3)
    - The wind movement should incorporate 3 things, one macro movement with the sum of 2 sin waves, then gust movement for heterogeneity in amplitude of the movmeent, then flutter movmement, and for the normal bend $$(1)
    - compute the tangent of the bend, and then the normal.
    - for the roundness effect : Replace the current two-normal roundness system with a single per-vertex fake curved normal. Compute the normal angle from uv.x so left vertices tilt one way and right vertices tilt the other, ideally rotating around the blade’s local tangent instead of the Y axis. Output only that single normal to the fragment shader and let rasterizer interpolation create the smooth curvature across the blade. In the fragment shader, only normalize the interpolated normal before lighting.
    - finaly get the final position of the vertex.
    - compute the tangent 
    - for now use terrain height as a texture and get 


Dilemmas (you need an architectural and best practice way to implement in each dilemmas the 2 or 3 confronting methods in order for me to tweak the simulaton or scene however i want and make the right decision, for example for the grass bending i need to be able to enable either normal bending or bezier bending without affecting the performances of the program ( check the state of the art or best practice way to do this))

$$(1) : normal grass bending VS bezier bending
$$(2) : either 1 sin or 2 sin waves
$$(3) : either use the terrain height from us painting the unity terrain, or actually have a mathematical texture as height so we don't load the height texture from memory to the vertex shader.
$$(3) :

# GrassFlow — Vertex/Fragment Shading Optimization Supplement for Codex

> **IMPORTANT — APPEND-ONLY SUPPLEMENT**
>
> This document is designed to be appended to the existing GrassFlow Codex prompt.
> **Do not rewrite, reinterpret, delete, or replace any part of the original prompt.**
> The original prompt remains the authority for grass representation, circular-buffer behavior, occupancy logic, LOD draw ranges, wind/bending dilemmas, terrain-height dilemmas, and all previously defined requirements.
>
> This supplement adds only:
>
> 1. the fragment-shader architecture,
> 2. extra vertex-stage preprocessing whose purpose is to reduce fragment cost,
> 3. the exact vertex → varying → fragment data flow,
> 4. the intended fragment feature set for LOD0 / LOD1 / LOD2,
> 5. an experimental architecture for switching expensive shading algorithms without turning the shader into an unmaintainable uber-shader.
>
> Use the Unity/Codex plugins and workflows already installed in the project. Do **not** add MCP setup, packages, managers, rendering frameworks, or unrelated project architecture as part of this supplement.

---

# 5 — Vertex-to-Fragment Shading Architecture

The goal is to keep the fragment shader as small as possible because the target scene can render roughly **1.5 million grass blades**, while still preserving AAA-grade stylized/anime-realistic vegetation lighting.

The main design rule is:

```text
Blade-level or slowly varying information
        ↓
calculate as early as practical in the vertex shader
        ↓
send only the minimum useful result

Fragment-specific lighting
        ↓
calculate in the fragment shader only when the visual gain
justifies doing it for every rasterized fragment
```

Do not move work to the vertex shader blindly.

A calculation should move from fragment → vertex only when at least one of these is true:

- it is effectively constant for the whole blade,
- it varies slowly enough that rasterizer interpolation is visually acceptable,
- the fragment shader currently repeats the same deterministic calculation many times,
- the vertex result can replace several existing varyings,
- the calculation is intentionally reduced in quality for a farther LOD.

Keep values as vertex-shader temporaries if the fragment stage does not actually need them.

---

# 5.1 — Current Forward-Pass Output to Replace

The current GrassFlow Forward pass conceptually outputs:

```hlsl
float4 positionCS     : SV_POSITION;
float3 positionWS     : TEXCOORD0;
float3 normalWS       : TEXCOORD1;
float  heightGradient : TEXCOORD2;
float3 rotatedNormal1 : TEXCOORD3;
float3 rotatedNormal2 : TEXCOORD4;
float2 widthAndAO     : TEXCOORD5;
float3 grassRootWS    : TEXCOORD6;
float4 shadowCoord    : TEXCOORD7; // when needed
```

The optimized design must aggressively reduce this payload.

For the current project phase, **grass shadows are disabled by the original prompt**, so:

```text
shadowCoord
```

must not be carried through the optimized grass Forward path unless the original prompt is later changed.

Do not reintroduce shadow work in this supplement.

---

# 5.2 — Required Vertex-Stage Preprocessing

The original vertex shader already performs or will perform:

```text
instance/cell reconstruction
occupancy decision
terrain/root reconstruction
blade height variation
wind
bending
bent position
bent tangent
bent normal
roundness normal
final clip-space position
```

Add the following preprocessing specifically to reduce fragment work.

---

## 5.2.1 — Stable Color Variation in the Vertex Shader

Current undesirable behavior:

```text
fragment
→ grassRootWS
→ hash
→ color noise
```

This repeats the same conceptual blade-level random calculation for many fragments belonging to the same blade.

Instead derive a stable scalar from the same deterministic world identity already available in the procedural vertex path.

Conceptually:

```text
stable grass/world cell ID
        ↓
cheap deterministic hash
        ↓
colorVariation
```

The exact hash implementation is an experimental implementation detail, but it must be:

- deterministic,
- stable between frames,
- stable under camera movement,
- independent from the moving draw-instance ordinal if that ordinal changes with the active window,
- very low cost.

Output:

```hlsl
half colorVariation;
```

Prefer packing it with other scalar grass data rather than consuming a standalone interpolator.

### New varying contribution

```text
colorVariation → one `half`
```

---

## 5.2.2 — Coherent Patch/Clump Color Variation

Do not rely only on independent per-blade white-noise color.

AAA-quality grass should also support low-frequency coherent variation between neighboring blades.

Use the world grid identity already reconstructed in the vertex shader:

```text
world row
column
    ↓
cheap patch grouping
    ↓
patchID
```

Conceptual example:

```text
patchRow    = worldRow >> patchShift
patchColumn = column   >> patchShift
```

Then combine:

```text
patchVariation
+
smaller bladeVariation
```

into the final:

```hlsl
half colorVariation;
```

No additional fragment varying is required.

$$(4) **Color variation source**

Architecturally support clean comparison between at least:

```text
A. Pure per-blade deterministic variation
B. Patch/clump variation + small per-blade variation
```

Do not compile both expensive algorithms into the same hot path when one mode is selected for a benchmark/shipping profile.

---

## 5.2.3 — Dry/Brown Grass Classification in the Vertex Shader

Current undesirable behavior:

```text
fragment
→ root world position
→ hash/random logic
→ dry mask
```

Instead calculate one stable blade-level dryness scalar in the vertex shader.

Preferred visual structure:

```text
coherent patch dryness
+
small blade-level variation
        ↓
dryness ∈ [0,1]
```

Output:

```hlsl
half dryness;
```

The fragment shader should only perform the final color application:

```hlsl
baseColor = lerp(baseColor, _DryColor, dryness);
```

### New varying contribution

```text
dryness → one `half`
```

No `grassRootWS` should remain in the fragment interface solely for color/dryness hashing.

---

## 5.2.4 — Final Rounded Normal in the Vertex Shader

Keep the original prompt's roundness requirement.

After bending, the vertex shader has:

```text
T = bent tangent
S = blade side direction
F = flat bent normal = normalize(cross(S, T))
```

Use the cross-blade coordinate already derivable from mesh UV / `SV_VertexID`:

```text
left side  → -1
center/tip →  0 when appropriate
right side → +1
```

Produce **one final fake-rounded normal per vertex**.

Conceptually:

```hlsl
half3 roundedNormalWS =
    normalize(
        flatBentNormal * roundCos +
        sideDirection  * widthCoord * roundSin
    );
```

or an equivalent tangent-axis rotation implementation.

The resulting normal must already contain:

```text
bending orientation
+
fake cross-blade roundness
```

before leaving the vertex shader.

Output only:

```hlsl
half3 normalWS;
```

The rasterizer interpolates this normal.

The fragment shader performs only:

```hlsl
half3 N = normalize(input.normalWS);
```

Remove the optimized Forward-path need for:

```text
rotatedNormal1
rotatedNormal2
fragment width coordinate used only for roundness
```

$$(5) **Roundness algorithm**

Architecturally support comparison between:

```text
A. Off / flat bent normal
B. Single per-vertex tilted normal — intended optimized default
C. Analytical per-fragment curved normal — reference/quality experiment
```

Mode C may require an additional interpolated width coordinate and side/frame information.

It must therefore be a deliberately selected experimental path, not permanent overhead imposed on the optimized path.

---

## 5.2.5 — LOD-Dependent Normal Stabilization in the Vertex Shader

Distant individual blade normals can cause noisy, unstable lighting.

Before outputting `normalWS`, optionally blend the individual rounded blade normal toward a more coherent patch/terrain/up-oriented normal.

Conceptually:

```hlsl
normalWS = normalize(
    lerp(
        roundedBladeNormal,
        stableFieldNormal,
        stabilizationAmount
    )
);
```

Recommended intent:

```text
LOD0 → little or no stabilization
LOD1 → moderate stabilization
LOD2 → strong stabilization
```

This improves temporal stability and reduces distant glitter.

It requires:

```text
NO new fragment varying
```

because it modifies the existing final `normalWS`.

$$(6) **Far normal stabilization source**

Allow experimentation between appropriate sources already available in the project, for example:

```text
A. World-up normal
B. Terrain normal, if cheaply available
C. Patch/clump representative normal
```

Do not add a costly new texture fetch unless the visual improvement is measured to justify it.

---

## 5.2.6 — Approximate View Direction for the Fixed Camera

The camera has a fixed viewing angle and primarily translates forward.

Therefore the optimized grass fragment shader should not automatically require:

```text
positionWS
→ cameraPosition - positionWS
→ normalize
```

for every fragment.

Instead expose a per-frame/per-draw constant:

```hlsl
float3 _GrassApproxViewDirWS;
```

derived from the fixed camera orientation.

Then LOD0/LOD1 view-dependent effects can use:

```hlsl
float3 V = _GrassApproxViewDirWS;
```

without transporting `positionWS`.

$$(7) **View-direction calculation**

Support clean comparison between:

```text
A. Fixed-camera approximate uniform view direction
B. Exact per-fragment view direction
```

Exact mode may reintroduce `positionWS` or an interpolated `viewDirWS`.

Do not pay that cost in mode A.

---

## 5.2.7 — Side-On Blade Widening Experiment

Because the camera angle is highly constrained, support an optional vertex-only experiment that prevents nearly edge-on blades from collapsing into pathological ultra-thin screen triangles.

Conceptual flow:

```text
blade side/orientation
+
fixed camera orientation
        ↓
side-on factor
        ↓
small view-space width correction
        ↓
final vertex position
```

This modifies position only.

It adds:

```text
NO fragment varying
```

$$(8) **Side-on width treatment**

Support:

```text
A. Disabled / physically unchanged ribbon
B. Small controlled side-on view-space widening
```

The widening must be capped so blades do not visibly become camera-facing billboards.

---

# 5.3 — Scalar Packing for LOD0 and LOD1

Pack the three scalar values required by the near/mid fragment shaders:

```text
heightT
colorVariation
dryness
```

into:

```hlsl
half3 grassData : TEXCOORD1;
```

with the strict documented mapping:

```text
grassData.x = heightT
grassData.y = colorVariation
grassData.z = dryness
```

Do not casually change this mapping in different shader files.

If the actual compiler/platform packs another representation better, keep one central definition/helper so all stages agree.

---

# 6 — Fragment Shader Architecture

The three LODs must reduce **fragment shading complexity**, not just blade geometry.

The intended progression is:

```text
LOD0
high-quality fragment shading

        ↓

LOD1
reduced fragment shading

        ↓

LOD2
almost all lighting moved to vertex stage
fragment shader nearly becomes a color write
```

Do not keep one large near-quality fragment shader for every LOD simply because sharing one function is convenient.

Shared helper functions are good.

Shared runtime cost is not.

---

# 6.1 — Global Feature Mapping

For the current no-shadow phase:

| Feature | LOD0 — Near | LOD1 — Mid | LOD2 — Far |
|---|---|---|---|
| Root → mid → tip color | Fragment | Fragment | Vertex |
| Stable blade/patch color variation | VS scalar → FS applies | VS scalar → FS applies | Fully applied in VS |
| Dry/brown variation | VS scalar → FS applies | VS scalar → FS applies | Fully applied in VS |
| Fake roundness | Rounded normal from VS | Rounded + stabilized normal from VS | Strongly stabilized/simplified in VS |
| Main sun diffuse | Fragment | Fragment | Vertex |
| Real-time grass shadow receiving | OFF | OFF | OFF |
| Height-dependent shadow softening | REMOVE | REMOVE | REMOVE |
| Transmission/backlight | High-quality cheap version | Cheaper version | Optional vertex approximation / OFF |
| Specular/sheen | Cheap near-only | OFF by default | OFF |
| Environment/sky ambient | Hemisphere in FS | Hemisphere in FS | Hemisphere in VS |
| Root fake AO | Derived from heightT in FS | Derived from heightT in FS | Applied in VS |
| Exact per-fragment view direction | Experimental only | Experimental/off | OFF |
| Fixed-camera approximate view direction | Default when needed | Default when needed | Usually unnecessary |
| Normal stabilization | Minimal | Moderate | Strong |
| Final FS output complexity | Full optimized grass lighting | Reduced grass lighting | Return interpolated color |

---

# 6.2 — LOD0 Fragment Shader — Near Grass

LOD0 is the visual reference among the optimized LODs.

It should preserve high-value near-camera features while removing redundant computations and varyings.

## LOD0 input target

```hlsl
struct GrassLOD0Varyings
{
    float4 positionCS : SV_POSITION;
    half3  normalWS   : TEXCOORD0;

    // x = heightT
    // y = colorVariation
    // z = dryness
    half3  grassData  : TEXCOORD1;
};
```

No shadow coordinate in the current phase.

No `grassRootWS`.

No default `positionWS`.

No `rotatedNormal1`.

No `rotatedNormal2`.

No `widthAndAO`.

---

## LOD0 feature 1 — Root → Mid → Tip Color

Keep the vertical color gradient in the fragment shader.

Input:

```hlsl
half heightT = input.grassData.x;
```

The shader should support root/mid/tip colors without an expensive operation.

Conceptually:

```text
0.0 root
0.5 middle
1.0 tip
```

The exact interpolation implementation may be branchless or use a coherent branch depending on generated shader quality.

Do not prioritize micro-optimizing this over larger costs.

---

## LOD0 feature 2 — Apply Stable Color Variation

Input:

```hlsl
half colorVariation = input.grassData.y;
```

The expensive/deterministic hash generation has already occurred in VS.

Fragment shader performs only cheap application such as:

```text
multiply / lerp / subtle hue/value shift
```

No root-position hash in FS.

---

## LOD0 feature 3 — Apply Dryness

Input:

```hlsl
half dryness = input.grassData.z;
```

Fragment shader performs:

```hlsl
baseColor = lerp(baseColor, _DryColor, dryness);
```

or a similarly cheap art-controlled application.

Dryness generation itself must not happen in FS.

---

## LOD0 feature 4 — Rounded Lighting Normal

Input:

```hlsl
half3 normalWS = input.normalWS;
```

Fragment shader:

```hlsl
half3 N = normalize(normalWS);
```

This is the one normal used by:

```text
sun diffuse
ambient
transmission
specular
```

Do not rebuild fake roundness from two normals in the optimized mode.

---

## LOD0 feature 5 — Main Sun Diffuse

Keep main directional-light orientation shading per fragment for near grass.

Use a cheap foliage-friendly response.

Example conceptual options:

```text
standard saturate(dot(N,L))
or
wrapped/two-sided grass diffuse
```

$$(9) **Direct diffuse model**

Support clean benchmarking between:

```text
A. Standard Lambert-like diffuse
B. Cheap wrapped/Half-Lambert-style foliage diffuse
```

Do not use a full general-purpose PBR BRDF unless the existing art direction proves that it adds visible value.

---

## LOD0 feature 6 — Transmission / Backlighting

Keep a cheap vegetation backlight term.

Recommended low-cost conceptual form:

```hlsl
half back =
    saturate(dot(-N, lightDir));

half transmission =
    back
    * _TransmissionStrength
    * heightT;
```

Optional cheap shaping:

```hlsl
transmission *= transmission;
```

Avoid arbitrary `pow()` in the optimized mode if a fixed multiply chain gives the desired look.

$$(10) **Transmission model**

Support:

```text
A. Off
B. Cheap normal/light/height backlight
C. Existing/reference transmission implementation
```

Only the selected algorithm should define the active hot path for the benchmark/shipping mode.

---

## LOD0 feature 7 — Near-Only Specular / Sheen

Keep specular only because LOD0 is close enough for it to matter visually.

Prefer a fixed low-cost exponent.

Example:

```hlsl
half NoH = saturate(dot(N, H));

half s2 = NoH * NoH;
half s4 = s2 * s2;
half s8 = s4 * s4;

half specular =
    s8 * _SpecularStrength;
```

The view direction should use the fixed-camera approximation by default.

$$(11) **Specular model**

Support:

```text
A. Off
B. Cheap fixed-power sheen
C. Existing/reference specular
```

Do not keep specular enabled in farther LODs by default.

---

## LOD0 feature 8 — Hemisphere Environment Lighting

Replace the optimized default SH evaluation with a cheap hemisphere model.

Conceptually:

```hlsl
half skyAmount =
    N.y * 0.5h + 0.5h;

half3 ambient =
    lerp(
        _GroundAmbient,
        _SkyAmbient,
        skyAmount
    );
```

$$(12) **Ambient model**

Support:

```text
A. Off
B. Hemisphere ambient
C. Existing Unity SH/reference ambient
```

Hemisphere is the intended mobile default.

---

## LOD0 feature 9 — Root Fake AO

Do not output a dedicated AO varying.

Derive root darkness from:

```hlsl
half heightT;
```

Example:

```hlsl
half h2 = heightT * heightT;

half rootAO =
    lerp(
        _RootDarkness,
        1.0h,
        h2
    );
```

This replaces the old `widthAndAO.y` path when that value existed only for this effect.

$$(13) **Root AO curve**

Allow cheap curve experimentation such as:

```text
A. Linear height
B. Squared height
C. Tunable low-order polynomial/smoothstep
```

This is a numerical/art choice and should not require a large shader architecture change.

---

## LOD0 final conceptual flow

```text
grassData.x → heightT
grassData.y → colorVariation
grassData.z → dryness
normalWS    → normalize
        ↓

root/mid/tip base color
        ↓
apply color variation
        ↓
apply dryness
        ↓

sun diffuse
+
transmission
+
hemisphere ambient
+
near specular
        ↓

multiply root AO
        ↓

final color
```

---

# 6.3 — LOD1 Fragment Shader — Mid Grass

LOD1 should preserve the major visual identity of grass but deliberately remove expensive near-only detail.

## LOD1 input target

```hlsl
struct GrassLOD1Varyings
{
    float4 positionCS : SV_POSITION;
    half3  normalWS   : TEXCOORD0;

    // x = heightT
    // y = colorVariation
    // z = dryness
    half3  grassData  : TEXCOORD1;
};
```

The structure intentionally matches LOD0 where doing so does not add unnecessary cost.

However the **meaning of `normalWS` differs**:

```text
LOD0 normalWS
= bent + rounded

LOD1 normalWS
= bent + rounded + more strongly stabilized
```

---

## LOD1 keeps

```text
root/mid/tip color
color variation application
dryness application
normalized rounded/stabilized normal
main sun diffuse
hemisphere ambient
root AO
cheap backlight if enabled
```

---

## LOD1 removes by default

```text
specular
exact view-direction calculation
near-quality transmission
all shadow work
```

---

## LOD1 transmission

Use a cheaper/weaker version of the LOD0 effect.

Ideally:

```text
normal
light direction
heightT
```

only.

Do not reintroduce world-position math.

---

## LOD1 normal stabilization

Use a stronger stabilization amount than LOD0.

This is important not only for speed but for temporal image quality:

```text
less high-frequency normal variation
→ fewer unstable highlights
→ less shimmer
```

---

## LOD1 final conceptual flow

```text
heightT
colorVariation
dryness
stabilized normal
        ↓

base color
        ↓
color variation
        ↓
dryness
        ↓

sun diffuse
+
cheap/optional transmission
+
hemisphere ambient
        ↓

root AO
        ↓

final color
```

---

# 6.4 — LOD2 Vertex-Lit / Minimal Fragment Shader — Far Grass

LOD2 is not merely a lower-vertex-count blade.

Its shading representation must also become cheaper.

Move nearly all lighting from the fragment shader into the vertex shader.

---

## LOD2 vertex shader computes

At each LOD2 vertex:

```text
heightT
        ↓
root/mid/tip color

stable color variation
        ↓
apply variation

dryness
        ↓
apply dry color

strongly stabilized normal
        ↓
sun diffuse

stabilized normal
        ↓
hemisphere ambient

heightT
        ↓
root AO

optional extremely cheap transmission
        ↓

combine all
        ↓
finalVertexColor
```

Output:

```hlsl
half3 color;
```

---

## LOD2 output target

```hlsl
struct GrassLOD2Varyings
{
    float4 positionCS : SV_POSITION;
    half3  color      : TEXCOORD0;
};
```

This is the intended final interface.

Do not pass:

```text
normalWS
heightT
colorVariation
dryness
positionWS
root position
width
AO
view direction
shadow coordinate
```

to the LOD2 fragment shader when their effect has already been baked into the vertex color.

---

## LOD2 fragment shader target

```hlsl
half4 GrassLOD2Fragment(
    GrassLOD2Varyings input
) : SV_Target
{
    return half4(input.color, 1.0h);
}
```

If alpha handling or URP integration requires a small additional operation, keep it minimal and document why it exists.

---

## LOD2 explicitly removes

```text
per-fragment normal normalization
per-fragment diffuse
per-fragment ambient
per-fragment root AO
per-fragment dryness application
per-fragment color variation application
specular
real shadows
exact view direction
near transmission model
SH evaluation
```

---

# 7 — Final Varying Comparison

## Old Forward payload

```hlsl
float4 positionCS     : SV_POSITION;
float3 positionWS     : TEXCOORD0;
float3 normalWS       : TEXCOORD1;
float  heightGradient : TEXCOORD2;
float3 rotatedNormal1 : TEXCOORD3;
float3 rotatedNormal2 : TEXCOORD4;
float2 widthAndAO     : TEXCOORD5;
float3 grassRootWS    : TEXCOORD6;
float4 shadowCoord    : TEXCOORD7;
```

---

## New LOD0

```hlsl
struct GrassLOD0Varyings
{
    float4 positionCS : SV_POSITION;
    half3  normalWS   : TEXCOORD0;
    half3  grassData  : TEXCOORD1;
};
```

Logical contents:

```text
positionCS
normalWS
heightT
colorVariation
dryness
```

---

## New LOD1

```hlsl
struct GrassLOD1Varyings
{
    float4 positionCS : SV_POSITION;
    half3  normalWS   : TEXCOORD0;
    half3  grassData  : TEXCOORD1;
};
```

Logical contents:

```text
positionCS
more-stabilized normalWS
heightT
colorVariation
dryness
```

---

## New LOD2

```hlsl
struct GrassLOD2Varyings
{
    float4 positionCS : SV_POSITION;
    half3  color      : TEXCOORD0;
};
```

Logical contents:

```text
positionCS
final pre-lit color
```

---

# 8 — Old → New Varying Mapping

| Old value | New treatment |
|---|---|
| `positionCS` | KEEP — required as `SV_POSITION` |
| `positionWS` | REMOVE by default; use fixed-camera view uniform where possible |
| old `normalWS` | REPURPOSE as the final bent + rounded + optionally stabilized shading normal |
| `heightGradient` | KEEP logically as `heightT`, packed into `grassData.x` for LOD0/1 |
| `rotatedNormal1` | REMOVE from optimized mode |
| `rotatedNormal2` | REMOVE from optimized mode |
| `widthAndAO.x` | REMOVE when used only for fragment roundness |
| `widthAndAO.y` | REMOVE; derive root AO from `heightT` |
| `grassRootWS` | REMOVE; color/dryness randomness generated before FS |
| `shadowCoord` | REMOVE for current phase because original prompt disables grass shadows |

---

# 9 — Fragment Features Moved or Preprocessed in VS

| Feature/calculation | Old location | New location | New output |
|---|---|---|---|
| Stable blade color hash | Fragment | Vertex | `grassData.y` |
| Patch/clump color variation | absent / fragment-like | Vertex | folded into `grassData.y` |
| Dryness generation | Fragment | Vertex | `grassData.z` |
| Fake roundness construction | VS + FS dual-normal blend | Vertex | `normalWS` |
| Normal stabilization | absent / later work | Vertex | folded into `normalWS` |
| LOD2 root/mid/tip color | Fragment | Vertex | folded into `color` |
| LOD2 color variation | Fragment | Vertex | folded into `color` |
| LOD2 dryness | Fragment | Vertex | folded into `color` |
| LOD2 sun diffuse | Fragment | Vertex | folded into `color` |
| LOD2 hemisphere ambient | Fragment | Vertex | folded into `color` |
| LOD2 root AO | Fragment | Vertex | folded into `color` |
| LOD2 optional transmission | Fragment | Vertex | folded into `color` |

Remember:

```text
traditional vertex shader work
= per vertex invocation

not literally once per blade
```

unless a separate blade-level preprocessing mechanism is intentionally introduced.

Do not falsely document these calculations as “once per blade.”

---

# 10 — Features That Stay in the Fragment Shader

## LOD0

```text
root/mid/tip interpolation
apply colorVariation
apply dryness
normalize interpolated normal
direct sun diffuse
cheap transmission
hemisphere ambient
near-only specular
root AO
final color combination
```

## LOD1

```text
root/mid/tip interpolation
apply colorVariation
apply dryness
normalize stabilized normal
direct sun diffuse
cheap/optional transmission
hemisphere ambient
root AO
final color combination
```

## LOD2

```text
ideally only:
return interpolated color
```

---

# 11 — Features Removed From the Optimized Fragment Pipeline

Remove from the currently selected optimized path:

```text
per-fragment root-position hash for color
per-fragment root-position hash for dryness
dual-normal roundness interpolation
dedicated AO varying
height-dependent shadow-softening
grass shadow coordinates
grass shadow sampling
LOD1 specular
LOD2 specular
LOD2 fragment lighting
unnecessary positionWS reconstruction/input
```

Reference implementations may remain accessible behind an experimental mode when needed for visual comparison, but they must not burden the selected optimized path.

---

# 12 — Precision Rules

The target is a mobile GPU.

Use `half` where visual precision permits:

```text
normalWS
heightT
colorVariation
dryness
base color
ambient
diffuse
AO
transmission
specular intermediates
LOD2 final color
```

Keep `float` where precision is materially important:

```text
world positions
large-coordinate calculations
clip-space position
terrain/root reconstruction where half can visibly jitter
```

Do not mechanically convert every value to `half`.

$$(14) **Precision profile**

Allow controlled comparison when necessary between:

```text
A. Conservative float-heavy reference
B. Mobile mixed precision
```

The optimized default should use mixed precision.

---

# 13 — Branching Rules

Do not follow the incorrect rule:

> never use `if` in GPU shaders.

Instead:

- avoid expensive highly divergent per-fragment branches,
- use compile-time/static feature selection for fundamentally different algorithms,
- use uniform branches where appropriate,
- allow cheap coherent control flow when it produces better generated code,
- profile rather than assuming branchless code is always faster.

The original occupancy behavior remains governed by the original prompt and is outside this fragment-shading supplement.

---

# 14 — Experimental Architecture for the `$$(i)` Choices

The project is intentionally experimental.

For each `$$(i)` dilemma, Codex must implement the alternatives so that:

1. algorithms share one canonical interface,
2. the selected algorithm can be changed without rewriting unrelated code,
3. the inactive algorithm does not permanently impose its full runtime cost,
4. the implementation does not generate an uncontrolled keyword-combination explosion,
5. benchmarks can select one coherent configuration.

Use the project's existing material/settings/profile architecture.

Do not create a parallel generic framework unless the real project lacks any suitable owner.

Prefer conceptual grouped modes such as:

```text
BendingMode
    Simple/Normal
    Bezier

MacroWindMode
    OneWave
    TwoWave

TerrainHeightMode
    Texture
    Mathematical

RoundnessMode
    Off
    VertexTilted
    FragmentAnalytic

ColorVariationMode
    PerBlade
    PatchPlusBlade

AmbientMode
    Off
    Hemisphere
    ReferenceSH

TransmissionMode
    Off
    Cheap
    Reference

SpecularMode
    Off
    FixedPower
    Reference

ViewDirectionMode
    FixedApproximation
    Exact

SideOnWideningMode
    Off
    On
```

These names are examples.

Codex must adapt them to the actual existing GrassFlow architecture rather than blindly introducing a new settings framework.

---

# 15 — Shader Variant Discipline

Do not make every numeric tweak a shader keyword.

Use:

```text
uniform/material values
```

for:

```text
strength
threshold
colors
roundness angle
AO strength
transmission strength
specular strength
normal stabilization amount
```

Use static/local shader variants only for choices that substantially change the code path, such as:

```text
Bezier vs simple bend
fragment-analytic vs vertex roundness
reference SH vs hemisphere if needed
exact-view path vs approximate-view path if their interfaces differ
LOD2 vertex-lit vs reference fragment-lit
```

Prefer stage-local keywords where Unity/URP supports the intended behavior.

Strip unused shipping variants after the winning configurations are chosen.

---

# 16 — LOD Shader Organization

Codex may share HLSL helper functions, but the resulting compiled LOD paths must remain deliberately different.

Conceptually:

```text
shared:
    deterministic hash
    base color helpers
    bend helpers
    normal helpers
    hemisphere helper
    transmission helper

LOD0:
    high-quality optimized FS

LOD1:
    reduced FS

LOD2:
    vertex-lit + trivial FS
```

Do not force LOD2 through the LOD0 fragment feature graph merely to maximize source-code reuse.

Source reuse is good.

Runtime work reuse is not automatically good.

---

# 17 — Mobile-Specific Priorities

When deciding between equivalent visual implementations, prioritize:

1. fewer unnecessary grass fragments,
2. fewer pathological microtriangles,
3. fewer interpolated components,
4. fewer fragment texture reads,
5. fewer repeated fragment hashes,
6. fewer fragment normalizations/transcendentals,
7. reduced precision where safe,
8. low register/interpolator pressure,
9. coherent data access,
10. simple far-LOD shading.

Do not assume ALU is free.

Do not assume memory is always the bottleneck.

Measure the actual project on the actual target when possible.

---

# 18 — Validation Requirements for This Supplement

Use the Unity/Codex validation workflows already installed in the project.

For every meaningful shader architecture change:

```text
compile
→ inspect Console
→ verify shader variants
→ visually validate LOD0
→ visually validate LOD1
→ visually validate LOD2
→ inspect LOD transitions
→ verify bending remains consistent
→ verify normals do not flip
→ verify two-sided grass remains correct
→ compare against reference
→ profile before/after where possible
```

Do not claim a performance win because the source “looks cheaper.”

For each `$$(i)` algorithm comparison:

```text
same scene
same camera
same density
same render scale
same LOD distances
same lighting
same device/API where possible
```

Change one meaningful algorithm at a time.

---

# 19 — Required Codex Implementation Report

After implementing this supplement, Codex must report:

## A. Final LOD0 Varyings

Show the complete actual structure.

## B. Final LOD1 Varyings

Show the complete actual structure.

## C. Final LOD2 Varyings

Show the complete actual structure.

## D. Vertex Preprocessing Map

For every moved/preprocessed fragment feature:

```text
feature
old stage
new stage
new varying/result
```

## E. Fragment Feature Matrix

Return:

| Feature | LOD0 | LOD1 | LOD2 |
|---|---|---|---|

using the actual implemented state.

## F. Removed Fragment Inputs

List every removed varying and why it is no longer needed.

## G. `$$(i)` Experiment Mapping

List each implemented algorithm dilemma and exactly where/how it is selected.

## H. Shader Variants

List newly introduced keywords/modes and explain how keyword explosion was avoided.

## I. Validation

State exactly what was tested:

```text
Unity compilation
Console
shader compilation
LOD0 visual
LOD1 visual
LOD2 visual
LOD transitions
Android/Vulkan compatibility
performance measurements
```

Do not claim checks that were not performed.

---

# 20 — Final Intended Dataflow

## LOD0

```text
VERTEX
────────────────────────────────
cell / root reconstruction
height
wind
bend
tangent
bent normal
vertex fake roundness
minimal normal stabilization
stable color variation
dryness
final position
        ↓
OUTPUT:
positionCS
normalWS
heightT
colorVariation
dryness


RASTERIZER
────────────────────────────────
interpolate


FRAGMENT
────────────────────────────────
normalize normal
root/mid/tip color
apply variation
apply dryness
sun diffuse
cheap transmission
hemisphere ambient
cheap near specular
root AO
final color
```

---

## LOD1

```text
VERTEX
────────────────────────────────
same geometric foundation
rounded normal
stronger normal stabilization
stable color variation
dryness
final position
        ↓
OUTPUT:
positionCS
normalWS
heightT
colorVariation
dryness


RASTERIZER
────────────────────────────────
interpolate


FRAGMENT
────────────────────────────────
normalize normal
root/mid/tip color
apply variation
apply dryness
sun diffuse
cheap/optional transmission
hemisphere ambient
root AO
final color

NO specular by default
```

---

## LOD2

```text
VERTEX
────────────────────────────────
geometry
simplified/appropriate bend
strong normal stabilization
root/mid/tip color
variation
dryness
sun diffuse
hemisphere ambient
root AO
optional tiny transmission
        ↓
finalVertexColor
        ↓
OUTPUT:
positionCS
color


RASTERIZER
────────────────────────────────
interpolate color


FRAGMENT
────────────────────────────────
return color
```

---

# 21 — Non-Goals of This Supplement

Do not use these instructions to alter or “fix” unrelated parts of the original prompt.

Specifically, this supplement does **not** authorize redesigning:

```text
GrassOriginalMatrix
grassCircularBuffer
originalCursor/startCursor semantics
frame synchronization design
occupancy addressing
three-draw LOD architecture
terrain streaming
camera-window architecture
instance/cell mapping
current choice to disable shadows
the original bending dilemma
the original wind dilemma
the original terrain-height dilemma
```

Those remain controlled by the original prompt and its existing `$$(i)` choices.

The purpose of this supplement is only to make the **vertex shading output and fragment shading pipeline** compact, modular, experimentally tunable, and appropriate for an extremely high grass-instance count on mobile hardware.
