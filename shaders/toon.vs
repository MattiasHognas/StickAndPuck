#version 330
// Raylib's standard vertex inputs, passing the world-space normal on for the toon ramp and the
// world position for where the band dither applies (W60).
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
uniform mat4 mvp;
uniform mat4 matNormal;
uniform mat4 matModel;
out vec2 fragTexCoord;
out vec3 fragNormal;
out vec3 fragWorld;
void main() {
    fragTexCoord = vertexTexCoord;
    fragWorld = vec3(matModel * vec4(vertexPosition, 1.0));
    fragNormal = normalize(vec3(matNormal * vec4(vertexNormal, 1.0)));
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
