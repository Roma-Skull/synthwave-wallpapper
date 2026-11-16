#version 300 es
precision highp float;
precision highp int;

uniform sampler2D sceneTex;
uniform sampler2D bloomTex;
uniform vec2 uResolution;
uniform float uTime;
uniform float uCurvature;
uniform float uScanlines;
uniform float uBloom;
uniform float uVhs;
uniform float uSweep;
uniform float uVignette;
uniform float uAnimate;
uniform float uScanPeriod;

in vec2 vUv;
out vec4 fragColor;

float ss(float edge0, float edge1, float x) {
    float t = clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

float hash12(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

vec3 sampleScene(vec2 uv) {
    return textureLod(sceneTex, clamp(uv, 0.001, 0.999), 0.0).rgb;
}

void main() {
    vec2 centered = vUv * 2.0 - 1.0;
    vec2 halfRes = uResolution * 0.5;
    float minDim = max(1.0, min(uResolution.x, uResolution.y));
    float rawAspect = uResolution.x / max(1.0, uResolution.y);
    float aspect = clamp(rawAspect, 0.20, 6.5);
    float sqAspect = sqrt(aspect);
    float wideScaleX = min(1.0, 1.7778 / max(1.0, rawAspect));

    // Aspect-balanced CRT barrel warp: edge midpoints stay anchored while corners curve cleanly
    float k = uCurvature * 0.048;
    float kx = k / sqAspect;
    float ky = k * sqAspect;
    vec2 barrel = uCurvature > 0.001
        ? centered * vec2(1.0 + kx * centered.y * centered.y, 1.0 + ky * centered.x * centered.x)
        : centered;
    vec2 crtUv = barrel * 0.5 + 0.5;

    // Signed distance to rounded CRT glass aperture in pixel coordinates (true circular corners)
    float pxScale = minDim / 1080.0;
    float padPx = max(1.5, (1.5 + 2.5 * uCurvature) * pxScale);
    float rCornerPx = min(minDim * 0.14, max(4.0, (4.0 + 24.0 * uCurvature) * pxScale));
    vec2 halfBoxPx = max(vec2(1.0, 1.0), halfRes - padPx - vec2(rCornerPx, rCornerPx));
    vec2 dBox = abs(barrel * halfRes) - halfBoxPx;
    float sdfPx = length(max(dBox, 0.0)) + min(max(dBox.x, dBox.y), 0.0) - rCornerPx;
    float aaPx = max(1.2, 1.5 * pxScale);
    float glassMask = uCurvature > 0.001 ? (1.0 - ss(-aaPx * 0.5, aaPx * 0.5, sdfPx)) : 1.0;

    vec3 bezelCol = vec3(0.0157, 0.0039, 0.0471);
    if (glassMask <= 0.001) {
        fragColor = vec4(bezelCol, 1.0);
        return;
    }

    // VHS horizontal sync flutter & traveling top-to-bottom scan sweep band
    vec2 sampleUv = crtUv;
    float t = uAnimate > 0.5 ? uTime : 0.0;
    float sweepMask = 0.0;
    float sweepSigned = 0.0;
    if (uSweep > 0.001 && uAnimate > 0.5) {
        float trackPos = fract(t * 0.12);
        sweepSigned = sampleUv.y - (1.0 - trackPos);
        float distTrack = abs(sweepSigned);
        float sweepScale = min(1.0, max(0.25, aspect));
        float sweepHalo = ss(0.085 * sweepScale, 0.0, distTrack);
        float sweepCore = ss(0.020 * sweepScale, 0.0, distTrack);
        sweepMask = (sweepHalo * 0.45 + sweepCore * 0.85) * uSweep;
    }

    if (uVhs > 0.001) {
        float glSampleY = sampleUv.y;
        float wave = sin(glSampleY * 42.0 + t * 5.2) * cos(glSampleY * 13.0 - t * 2.1);
        sampleUv.x += (wave * 0.0014 + sin(glSampleY * 160.0 + t * 28.0) * 0.0045 * sweepMask) * uVhs * wideScaleX;
    }

    // Chromatic RGB split along edges & VHS tape shift
    float chroma = uVhs * (0.0009 + 0.0038 * pow(max(1e-5, length(centered)), 1.35)) + sweepMask * uVhs * 0.0045;
    vec2 chromaVec = vec2(chroma * wideScaleX, chroma * 0.12 * min(1.0, rawAspect));
    vec3 baseCol;
    if (chroma > 0.0001) {
        baseCol.r = sampleScene(sampleUv + chromaVec).r;
        baseCol.g = sampleScene(sampleUv).g;
        baseCol.b = sampleScene(sampleUv - chromaVec).b;
    } else {
        baseCol = sampleScene(sampleUv);
    }

    // Phosphor bloom glow from pre-filtered 1/9-scene bloomTex (1 tap instead of 6 native taps)
    if (uBloom > 0.001) {
        vec3 glowAcc = textureLod(bloomTex, clamp(sampleUv, 0.001, 0.999), 0.0).rgb;
        vec3 bloomTint = glowAcc * vec3(1.06 + uVhs * 0.14, 1.0, 1.14 + uVhs * 0.16);
        baseCol = 1.0 - (1.0 - baseCol) * (1.0 - clamp(bloomTint * uBloom * 0.72, 0.0, 0.92));
    }

    if (sweepMask > 0.001) {
        float edgeSplit = ss(-0.04, 0.04, sweepSigned);
        vec3 sweepCol = mix(vec3(0.14, 0.85, 1.0), vec3(0.95, 0.22, 0.62), edgeSplit);
        baseCol += mix(sweepCol, vec3(0.45, 0.94, 1.0), 0.35) * (sweepMask * 0.34);
    }

    // Physical-pixel locked scanlines & aperture grille
    if (uScanlines > 0.001) {
        float phaseY = fract(gl_FragCoord.y / max(2.0, uScanPeriod));
        float wBell = abs(phaseY * 2.0 - 1.0);
        float waveY = wBell * wBell * (3.0 - 2.0 * wBell);
        float interlace = uAnimate > 0.5 ? (0.95 + 0.05 * sin(uTime * 52.0)) : 1.0;
        float scanMul = 1.0 - uScanlines * 0.36 * (1.0 - waveY) * interlace;
        baseCol *= scanMul;

        float sub = mod(floor(gl_FragCoord.x), 3.0);
        vec3 grille = sub < 0.5
            ? vec3(1.05, 0.96, 0.99)
            : (sub < 1.5 ? vec3(0.96, 1.05, 0.99) : vec3(0.98, 0.97, 1.06));
        baseCol *= mix(vec3(1.0, 1.0, 1.0), grille, uScanlines * 0.28);
    }

    if (uVhs > 0.001) {
        float frameSeed = uAnimate > 0.5 ? floor(uTime * 16.0) : 1.0;
        float n = hash12(floor(gl_FragCoord.xy * 0.75) + frameSeed * 19.7) - 0.5;
        baseCol += vec3(n * 1.06, n * 0.96, n * 1.12) * (0.075 * uVhs);
    }

    if (uVignette > 0.001) {
        float dist = length(centered * vec2(0.84, 0.94));
        float vig = 1.0 - uVignette * 0.78 * ss(0.55, 1.32, dist);
        baseCol *= max(0.08, vig);

        vec2 glCrtUv = crtUv;
        float sheenAspect = clamp(rawAspect / 1.7778, 0.70, 2.2);
        vec2 sheenV = (glCrtUv - vec2(0.18, 0.85)) * vec2(1.4 * sheenAspect, 2.2);
        float sheen = exp(-dot(sheenV, sheenV) * 3.5);
        baseCol += vec3(0.18, 0.28, 0.36) * sheen * (0.08 * uVignette + 0.03 * uCurvature);
    }

    if (uCurvature > 0.001) {
        float curvBlend = min(1.0, uCurvature * 4.0);
        float shadowPx = max(6.0, (9.0 + 11.0 * uCurvature) * pxScale);
        float innerShadow = ss(-shadowPx, 0.0, sdfPx);
        baseCol *= 1.0 - innerShadow * (0.28 + 0.34 * uCurvature) * curvBlend;
        float rimWidthPx = max(1.4, 1.8 * pxScale);
        float rimGlow = ss(rimWidthPx, 0.0, abs(sdfPx)) * (0.14 + 0.22 * uCurvature) * curvBlend;
        baseCol += vec3(0.71, 0.22, 0.95) * rimGlow;
    }

    fragColor = vec4(mix(bezelCol, clamp(baseCol, 0.0, 1.0), glassMask), 1.0);
}
