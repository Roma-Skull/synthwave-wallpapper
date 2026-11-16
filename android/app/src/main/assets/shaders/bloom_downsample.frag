#version 300 es
precision highp float;
precision highp int;

uniform sampler2D sceneTex;
uniform vec2 uResolution;

in vec2 vUv;
out vec4 fragColor;

vec3 sampleBright(vec2 uv) {
    vec3 s = textureLod(sceneTex, clamp(uv, 0.001, 0.999), 0.0).rgb;
    return max(vec3(0.0, 0.0, 0.0), s - 0.34);
}

void main() {
    // 9-tap tent downsample running at 1/9th scene pixels (~1/25th native pixels)
    // Hardware bilinear filtering + inner/outer offsets create a smooth phosphor glow
    vec2 invRes = 1.0 / max(vec2(32.0, 32.0), uResolution);
    vec2 d1 = invRes * 1.85;
    vec2 d2 = invRes * 4.20;

    vec3 acc = sampleBright(vUv) * 0.24;
    acc += (sampleBright(vUv + vec2(-d1.x, -d1.y))
          + sampleBright(vUv + vec2( d1.x, -d1.y))
          + sampleBright(vUv + vec2(-d1.x,  d1.y))
          + sampleBright(vUv + vec2( d1.x,  d1.y))) * 0.125;
    acc += (sampleBright(vUv + vec2(-d2.x,  0.0))
          + sampleBright(vUv + vec2( d2.x,  0.0))
          + sampleBright(vUv + vec2( 0.0,  -d2.y))
          + sampleBright(vUv + vec2( 0.0,   d2.y))) * 0.065;

    fragColor = vec4(acc, 1.0);
}
