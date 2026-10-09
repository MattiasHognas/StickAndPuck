#version 330
// Three flat tones times the kit texture, the same ramp as the Blender preview:
// shadow below 0.22, mid tone below 0.55, full above. No specular, no gradients.
// The tones are set by the lighting (W18); the defaults are the dusk look.
in vec2 fragTexCoord;
in vec3 fragNormal;
uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 lightDir;
uniform vec3 shadowTone = vec3(0.42, 0.40, 0.48);
uniform vec3 midTone = vec3(0.70, 0.68, 0.72);
uniform vec3 fullTone = vec3(1.0);
out vec4 finalColor;
void main() {
    vec4 t = texture(texture0, fragTexCoord) * colDiffuse;
    float d = max(dot(normalize(fragNormal), normalize(lightDir)), 0.0);
    vec3 tone = d < 0.22 ? shadowTone : (d < 0.55 ? midTone : fullTone);
    finalColor = vec4(min(t.rgb * tone, vec3(1.0)), 1.0);
}
