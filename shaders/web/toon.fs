#version 100
precision highp float;
precision highp int;
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
varying vec2 fragTexCoord;
varying vec3 fragNormal;
varying vec3 fragWorld;
uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 lightDir;
uniform vec3 shadowTone;
uniform vec3 midTone;
uniform vec3 fullTone;
uniform float bandWidth;
uniform vec2 ditherOrigin;
uniform vec3 rink;          // half length, half width and corner radius of the boards, metres


// the 4x4 Bayer threshold of this pixel, in (0, 1)
float bayer(vec2 p) {
    vec2 i = mod(floor(p), 4.0);
    vec2 low = mod(i, 2.0), high = floor(i / 2.0);
    float a = 2.0 * low.x + 3.0 * low.y - 4.0 * low.x * low.y;
    float b = 2.0 * high.x + 3.0 * high.y - 4.0 * high.x * high.y;
    return (4.0 * a + b + 0.5) / 16.0;
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
    vec4 t = texture2D(texture0, fragTexCoord) * colDiffuse;
    float d = max(dot(normalize(fragNormal), normalize(lightDir)), 0.0);
    float b = bandWidth > 0.0 && insideBoards(fragWorld) ? bayer(gl_FragCoord.xy - ditherOrigin) : -1.0;
    vec3 tone = above(d, 0.55, b) ? fullTone : (above(d, 0.22, b) ? midTone : shadowTone);
    gl_FragColor = vec4(min(t.rgb * tone, vec3(1.0)), 1.0);
}
