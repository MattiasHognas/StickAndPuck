#version 330
// Flat id colour per player (the tint), so the post pass can find silhouettes.
in vec2 fragTexCoord;
in vec4 fragColor;
uniform vec4 colDiffuse;
out vec4 finalColor;
void main() {
    finalColor = vec4(colDiffuse.rgb, 1.0);
}
