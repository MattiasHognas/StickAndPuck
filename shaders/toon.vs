#version 330
// Raylib's standard vertex inputs, passing the world-space normal on for the toon ramp.
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
uniform mat4 mvp;
uniform mat4 matNormal;
out vec2 fragTexCoord;
out vec3 fragNormal;
void main() {
    fragTexCoord = vertexTexCoord;
    fragNormal = normalize(vec3(matNormal * vec4(vertexNormal, 1.0)));
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
