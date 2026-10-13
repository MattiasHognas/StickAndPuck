#version 330
// Pixel post pass on the scene frame (480x270, or 240x135 / 160x90 for close shots, W59): snap
// every pixel to the palette, and draw a one-pixel outline just outside each player (where an
// empty mask pixel touches a player, or where two different players touch). Players are told
// apart by the red channel (Render/MaskIds.cs); green and blue hold the depth (shaders/mask.fs),
// and on close shots a line is also drawn on a player's pixel just behind a part of the same
// player that is clearly nearer (an arm or stick across the body).
// W60: a colour that falls between two close palette colours is not snapped hard to the nearer
// one: near the midpoint the two are mixed with a 4x4 ordered Bayer pattern on the scene's pixel
// grid, so gradients (a dimmed rink, light falling off) step through checker rows, not a seam.
// The pattern starts at ditherOrigin, as in toon.fs, so it moves with the rink, not the screen.
in vec2 fragTexCoord;
uniform sampler2D texture0;
uniform sampler2D maskTex;
uniform vec3 palette[64];
uniform int paletteSize;
uniform int kitStart;       // palette[kitStart..] are kit colours, used only on player pixels
uniform vec3 outline;
uniform vec2 texel;
uniform float innerDepth;   // metres; 0 turns the inner lines off
uniform float ditherSpread; // how far either side of the midpoint the mix runs, 0-0.5; 0 turns it off
uniform vec2 ditherOrigin;  // scene pixels, see Render/DitherGrid.cs
uniform float ditherGap;    // only palette pairs closer than this (rgb distance) are mixed
out vec4 finalColor;

// 0 for no player, otherwise the player's mask id
float id(vec4 m) {
    if (m.a < 0.5) return 0.0;
    return floor(m.r * 255.0 + 0.5);
}

// distance from the camera in metres
float depth(vec4 m) {
    return (floor(m.g * 255.0 + 0.5) * 256.0 + floor(m.b * 255.0 + 0.5)) / 65535.0 * 128.0;
}

// the 4x4 Bayer threshold of a scene pixel, in (0, 1)
float bayer(vec2 p) {
    const float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
    ivec2 i = ivec2(mod(floor(p), 4.0));
    return (m[i.y * 4 + i.x] + 0.5) / 16.0;
}

void main() {
    vec2 uv = fragTexCoord;                      // both render textures share the flipped draw, so the same uv works
    vec4 m = texture(maskTex, uv);
    float here = id(m);
    float behind = depth(m);
    bool edge = false;
    vec2 d[4] = vec2[4](vec2(texel.x, 0), vec2(-texel.x, 0), vec2(0, texel.y), vec2(0, -texel.y));
    for (int i = 0; i < 4; i++) {
        vec4 nm = texture(maskTex, uv + d[i]);
        float n = id(nm);
        // outside a player, or the lower id where two players meet, so a shared edge is one pixel
        if (n > 0.0 && (here == 0.0 || here < n)) edge = true;
        // inside one player, the pixel behind a nearer part, so the line sits on the far side
        if (innerDepth > 0.0 && n > 0.0 && n == here && behind - depth(nm) > innerDepth) edge = true;
    }
    if (edge) { finalColor = vec4(outline, 1.0); return; }
    vec3 c = texture(texture0, uv).rgb;
    vec3 best = palette[0], next = palette[0]; float bd = 1e9, nd = 1e9;
    int n = here > 0.0 ? paletteSize : kitStart;
    for (int i = 0; i < n; i++) {
        vec3 e = c - palette[i]; float dd = dot(e, e);
        if (dd < bd) { nd = bd; next = best; bd = dd; best = palette[i]; }
        else if (dd < nd) { nd = dd; next = palette[i]; }
    }
    vec3 gap = next - best; float g = dot(gap, gap);
    if (ditherSpread > 0.0 && g > 0.0 && g < ditherGap * ditherGap) {
        // where c sits from best (0) to next (1); the nearer colour is best, so this is at most about 0.5
        float along = clamp(dot(c - best, gap) / g, 0.0, 1.0);
        float mix = clamp((along - 0.5) / (2.0 * ditherSpread) + 0.5, 0.0, 1.0);
        if (mix > bayer(uv / texel - ditherOrigin)) best = next;
    }
    finalColor = vec4(best, 1.0);
}
