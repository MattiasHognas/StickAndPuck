#version 330
// Pixel post pass on the 480x270 frame: snap every pixel to the palette, and draw a one-pixel
// outline just outside each player (where an empty mask pixel touches a player, or where two
// different players touch). Players are told apart by their whole mask colour, so any set of
// distinct ids works; Render/MaskIds.cs puts the id in the red channel.
in vec2 fragTexCoord;
uniform sampler2D texture0;
uniform sampler2D maskTex;
uniform vec3 palette[64];
uniform int paletteSize;
uniform int kitStart;       // palette[kitStart..] are kit colours, used only on player pixels
uniform vec3 outline;
uniform vec2 texel;
out vec4 finalColor;

// 0 for no player, otherwise a number unique to that player's mask colour
float id(vec4 m) {
    if (m.a < 0.5) return 0.0;
    vec3 c = floor(m.rgb * 255.0 + 0.5);
    return 1.0 + c.r + c.g * 256.0 + c.b * 65536.0;
}

void main() {
    vec2 uv = fragTexCoord;                      // both render textures share the flipped draw, so the same uv works
    float here = id(texture(maskTex, uv));
    bool edge = false;
    vec2 d[4] = vec2[4](vec2(texel.x, 0), vec2(-texel.x, 0), vec2(0, texel.y), vec2(0, -texel.y));
    for (int i = 0; i < 4; i++) {
        float n = id(texture(maskTex, uv + d[i]));
        // outside a player, or the lower id where two players meet, so a shared edge is one pixel
        if (n > 0.0 && (here == 0.0 || here < n)) edge = true;
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
