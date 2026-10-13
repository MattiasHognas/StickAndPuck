#version 330
// Pixel post pass on the scene frame (480x270, or 240x135 / 160x90 for close shots, W59): snap
// every pixel to the palette, and draw a one-pixel outline just outside each player (where an
// empty mask pixel touches a player, or where two different players touch). Players are told
// apart by the red channel (Render/MaskIds.cs); green and blue hold the depth (shaders/mask.fs),
// and on close shots a line is also drawn on a player's pixel just behind a part of the same
// player that is clearly nearer (an arm or stick across the body).
in vec2 fragTexCoord;
uniform sampler2D texture0;
uniform sampler2D maskTex;
uniform vec3 palette[64];
uniform int paletteSize;
uniform int kitStart;       // palette[kitStart..] are kit colours, used only on player pixels
uniform vec3 outline;
uniform vec2 texel;
uniform float innerDepth;   // metres; 0 turns the inner lines off
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
    vec3 best = palette[0]; float bd = 1e9;
    int n = here > 0.0 ? paletteSize : kitStart;
    for (int i = 0; i < n; i++) {
        vec3 e = c - palette[i]; float dd = dot(e, e);
        if (dd < bd) { bd = dd; best = palette[i]; }
    }
    finalColor = vec4(best, 1.0);
}
