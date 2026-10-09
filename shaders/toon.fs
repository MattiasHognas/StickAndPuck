#version 330
// Three flat tones times the kit texture, the same ramp as the Blender preview:
// shadow below 0.22, mid tone below 0.55, full above. No specular, no gradients.
in vec2 fragTexCoord;
in vec3 fragNormal;
uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 lightDir;
out vec4 finalColor;
void main() {
    vec4 t = texture(texture0, fragTexCoord) * colDiffuse;
    float d = max(dot(normalize(fragNormal), normalize(lightDir)), 0.0);
    vec3 tone = d < 0.22 ? vec3(0.42, 0.40, 0.48) : (d < 0.55 ? vec3(0.70, 0.68, 0.72) : vec3(1.0));
    finalColor = vec4(t.rgb * tone, 1.0);
}
