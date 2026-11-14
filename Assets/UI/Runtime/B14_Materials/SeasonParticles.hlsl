// The season particles' motion, run in the vertex stage of SeasonParticles.shadergraph (its Custom Function node).
//
// SeasonParticles.cs draws each particle once, at its start position, and a mesh modifier writes the particle's motion
// into the extra vertex channels of all its vertices. This moves them every frame on the GPU: the CPU only sets the
// global _BagelTime.
//
//   UV1  start x, start y (fractions of the element), fall (heights a second, < 0 rises), sway (widths)
//   UV2  sway frequency (radians a second), phase (radians), spin (degrees a second; a firefly's flicker), kind
//   UV3  element width, height (points), clock, frozen
//
// Kind: 1 maple leaf, 2 sakura petal, 3 firefly, 4 snowflake, 5 snow crystal; 0 leaves the vertex where it is.
// Frozen 0: the particle's time is Time minus the clock. Frozen 1: the clock is its time, so it holds still.

void SeasonParticle_float(float3 Position, float4 UV1, float4 UV2, float4 UV3, float Time, out float3 Out, out float Alpha)
{
    Out = Position;
    Alpha = 1;
    float kind = UV2.w;
    if (kind < 0.5)
        return;

    float t = UV3.w > 0.5 ? UV3.z : Time - UV3.z;
    float2 size = UV3.xy;
    // Where the particle was drawn: its vertices are around it.
    float2 local = Position.xy - UV1.xy * size;

    // Fall from just above the element to just below it, then start again at the top (fireflies rise the other way),
    // in a new column each time round so the field doesn't repeat.
    const float top = -0.12;
    const float span = 1.24;
    float travel = UV1.y - top + UV1.z * t;
    float lap = floor(travel / span);
    float y = top + travel - lap * span;
    float x = frac(UV1.x + lap * 0.618034);

    float sway = sin(t * UV2.x + UV2.y);
    x += sway * UV1.w;

    float start = degrees(UV2.y);
    float rotation = 0;
    float squash = 1;
    if (kind < 1.5)
    {
        // A maple leaf tumbles: it spins, and turning over squashes it across. Squashed, never mirrored: a negative
        // squash would turn its triangles' backs to the camera, and UI Toolkit's stencil clipping (rounded corners with
        // overflow: hidden) treats back faces as the end of a mask, so they'd draw outside it. The leaf is symmetric.
        rotation = start + UV2.z * t + sway * 25;
        squash = max(0.18, abs(cos(t * UV2.x * 2 + UV2.y)));
    }
    else if (kind < 2.5)
    {
        rotation = start + UV2.z * t + sway * 40;
        squash = 0.55 + 0.45 * abs(cos(t * UV2.x * 1.2 + UV2.y));
    }
    else if (kind < 3.5)
    {
        // A firefly wanders and flickers: the fragment stage reads Alpha through a custom interpolator.
        x += sin(t * 0.7 + UV2.y * 2) * 0.03;
        Alpha = saturate(0.3 + 0.7 * sin(t * UV2.z + UV2.y));
    }
    else if (kind > 4.5)
    {
        rotation = start + UV2.z * t;
    }

    float s, c;
    sincos(radians(rotation), s, c);
    local.x *= squash;
    local = float2(local.x * c - local.y * s, local.x * s + local.y * c);
    Out = float3(float2(x, y) * size + local, Position.z);
}

void SeasonParticle_half(half3 Position, half4 UV1, half4 UV2, half4 UV3, half Time, out half3 Out, out half Alpha)
{
    float3 position;
    float alpha;
    SeasonParticle_float(Position, UV1, UV2, UV3, Time, position, alpha);
    Out = position;
    Alpha = alpha;
}
