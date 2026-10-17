#version 100
precision highp float;
precision highp int;
// Match toon.vs precision so unlit surfaces and masks agree on position/depth.
// raylib's default ES2 vertex shader uses mediump, which can be 16-bit on hardware.
attribute vec3 vertexPosition;
attribute vec2 vertexTexCoord;
attribute vec4 vertexColor;
uniform mat4 mvp;
varying vec2 fragTexCoord;
varying vec4 fragColor;
void main() {
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
