#version 330
// Flat id per player in red (the tint), so the post pass can find silhouettes, and the pixel's
// distance from the camera in green and blue (16 bits over 128 m), so it can find inner edges (W59).
in vec2 fragTexCoord;
in vec4 fragColor;
uniform vec4 colDiffuse;
uniform float nearPlane = 0.05;
uniform float farPlane = 4000.0;
out vec4 finalColor;
void main() {
    float z = gl_FragCoord.z * 2.0 - 1.0;
    float metres = 2.0 * nearPlane * farPlane / (farPlane + nearPlane - z * (farPlane - nearPlane));
    float v = floor(clamp(metres / 128.0, 0.0, 1.0) * 65535.0 + 0.5);
    float hi = floor(v / 256.0);
    finalColor = vec4(colDiffuse.r, hi / 255.0, (v - hi * 256.0) / 255.0, 1.0);
}
