#version 330
// Three flat tones times the kit texture, the same ramp as the Blender preview:
// shadow below 0.22, mid tone below 0.55, full above. No specular, no gradients.
// The tones are set by the lighting (W18); the defaults are the dusk look.
// W60: within bandWidth of each step the two tones are mixed with a 4x4 ordered Bayer pattern
// on the target's pixel grid, so a band edge turns into a short run of checker pixels instead of a
// stair-stepped line. The pattern starts at ditherOrigin (Render/DitherGrid.cs), the pixel a fixed
// spot on the ice lands on, so it steps with the rink when the camera does. Only surfaces inside the
// boards (players, goalies, nets) are dithered: the boards, glass and crowd sit at other depths,
// where a camera step is not a whole pixel, so a pattern there would crawl; they keep hard bands.
// bandWidth 0 turns it off.
in vec2 fragTexCoord;
in vec3 fragNormal;
in vec3 fragWorld;
uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 lightDir;
uniform vec3 shadowTone = vec3(0.42, 0.40, 0.48);
uniform vec3 midTone = vec3(0.70, 0.68, 0.72);
uniform vec3 fullTone = vec3(1.0);
uniform float bandWidth = 0.0;
uniform vec2 ditherOrigin;
uniform vec3 rink;          // half length, half width and corner radius of the boards, metres
out vec4 finalColor;

// the 4x4 Bayer threshold of this pixel, in (0, 1)
float bayer(vec2 p) {
    const float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
    ivec2 i = ivec2(mod(floor(p), 4.0));
    return (m[i.y * 4 + i.x] + 0.5) / 16.0;
}

// true where this pixel takes the brighter of the two tones meeting at the step
bool above(float d, float step, float threshold) {
    if (threshold < 0.0) return d >= step;
    return clamp((d - step) / (2.0 * bandWidth) + 0.5, 0.0, 1.0) > threshold;
}

// true inside the boards, a little in from them, on the ice plan (x along the rink, z across)
bool insideBoards(vec3 p) {
    vec2 q = abs(p.xz) - (rink.xy - rink.z);
    float outside = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - rink.z;
    return outside < -0.15;
}

void main() {
    vec4 t = texture(texture0, fragTexCoord) * colDiffuse;
    float d = max(dot(normalize(fragNormal), normalize(lightDir)), 0.0);
    float b = bandWidth > 0.0 && insideBoards(fragWorld) ? bayer(gl_FragCoord.xy - ditherOrigin) : -1.0;
    vec3 tone = above(d, 0.55, b) ? fullTone : (above(d, 0.22, b) ? midTone : shadowTone);
    finalColor = vec4(min(t.rgb * tone, vec3(1.0)), 1.0);
}
