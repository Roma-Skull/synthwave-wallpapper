Texture2D sceneTex : register(t0);
SamplerState linearClamp : register(s0);

cbuffer CrtParams : register(b0) {
    float2 uResolution;
    float  uTime;
    float  uCurvature;
    float  uScanlines;
    float  uBloom;
    float  uVhs;
    float  uSweep;
    float  uVignette;
    float  uAnimate;
    float  uScanPeriod;
    float  _pad0;
};

struct PSIn {
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

float ss(float edge0, float edge1, float x) {
    float t = saturate((x - edge0) / (edge1 - edge0));
    return t * t * (3.0 - 2.0 * t);
}

float hash12(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float3 sampleScene(float2 uv) {
    return sceneTex.SampleLevel(linearClamp, clamp(uv, 0.001, 0.999), 0).rgb;
}

float4 main(PSIn input) : SV_Target {
    float2 vUv = input.uv;
    float2 centered = vUv * 2.0 - 1.0;
    float2 halfRes = uResolution * 0.5;
    float minDim = max(1.0, min(uResolution.x, uResolution.y));
    float rawAspect = uResolution.x / max(1.0, uResolution.y);
    float aspect = clamp(rawAspect, 0.20, 6.5);
    float sqAspect = sqrt(aspect);
    float wideScaleX = min(1.0, 1.7778 / max(1.0, rawAspect));

    // Aspect-balanced CRT barrel warp: edge midpoints stay anchored while corners curve cleanly
    float k = uCurvature * 0.048;
    float kx = k / sqAspect;
    float ky = k * sqAspect;
    float2 barrel = uCurvature > 0.001
        ? centered * float2(1.0 + kx * centered.y * centered.y, 1.0 + ky * centered.x * centered.x)
        : centered;
    float2 crtUv = barrel * 0.5 + 0.5;

    // Signed distance to rounded CRT glass aperture in pixel coordinates (true circular corners)
    float pxScale = minDim / 1080.0;
    float padPx = max(1.5, (1.5 + 2.5 * uCurvature) * pxScale);
    float rCornerPx = min(minDim * 0.14, max(4.0, (4.0 + 24.0 * uCurvature) * pxScale));
    float2 halfBoxPx = max(float2(1.0, 1.0), halfRes - padPx - float2(rCornerPx, rCornerPx));
    float2 dBox = abs(barrel * halfRes) - halfBoxPx;
    float sdfPx = length(max(dBox, 0.0)) + min(max(dBox.x, dBox.y), 0.0) - rCornerPx;
    float aaPx = max(1.2, 1.5 * pxScale);
    float glassMask = uCurvature > 0.001 ? (1.0 - ss(-aaPx * 0.5, aaPx * 0.5, sdfPx)) : 1.0;

    float3 bezelCol = float3(0.0157, 0.0039, 0.0471);
    if (glassMask <= 0.001) {
        return float4(bezelCol, 1.0);
    }

    // VHS horizontal sync flutter & traveling top-to-bottom scan sweep band
    float2 sampleUv = crtUv;
    float t = uAnimate > 0.5 ? uTime : 0.0;
    float sweepMask = 0.0;
    float sweepSigned = 0.0;
    if (uSweep > 0.001 && uAnimate > 0.5) {
        float trackPos = frac(t * 0.12);
        sweepSigned = (1.0 - sampleUv.y) - (1.0 - trackPos);
        float distTrack = abs(sweepSigned);
        float sweepScale = min(1.0, max(0.25, aspect));
        float sweepHalo = ss(0.085 * sweepScale, 0.0, distTrack);
        float sweepCore = ss(0.020 * sweepScale, 0.0, distTrack);
        sweepMask = (sweepHalo * 0.45 + sweepCore * 0.85) * uSweep;
    }

    if (uVhs > 0.001) {
        float glSampleY = 1.0 - sampleUv.y;
        float wave = sin(glSampleY * 42.0 + t * 5.2) * cos(glSampleY * 13.0 - t * 2.1);
        sampleUv.x += (wave * 0.0014 + sin(glSampleY * 160.0 + t * 28.0) * 0.0045 * sweepMask) * uVhs * wideScaleX;
    }

    // Chromatic RGB split along edges & VHS tape shift
    float chroma = uVhs * (0.0009 + 0.0038 * pow(max(1e-5, length(centered)), 1.35)) + sweepMask * uVhs * 0.0045;
    float2 chromaVec = float2(chroma * wideScaleX, -chroma * 0.12 * min(1.0, rawAspect));
    float3 baseCol;
    if (chroma > 0.0001) {
        baseCol.r = sampleScene(sampleUv + chromaVec).r;
        baseCol.g = sampleScene(sampleUv).g;
        baseCol.b = sampleScene(sampleUv - chromaVec).b;
    } else {
        baseCol = sampleScene(sampleUv);
    }

    // Phosphor bloom glow from bright highlights (6 staggered taps: 3 inner + 3 outer = ~62% fewer texture fetches)
    if (uBloom > 0.001) {
        float2 texel = (2.5 + uBloom * 8.5) / uResolution;
        static const float2 bloomDirs[6] = {
            float2( 1.000,  0.000) * 1.15,
            float2(-0.500,  0.866) * 1.15,
            float2(-0.500, -0.866) * 1.15,
            float2( 0.500,  0.866) * 2.35,
            float2(-1.000,  0.000) * 2.35,
            float2( 0.500, -0.866) * 2.35
        };
        float3 glowAcc = float3(0.0, 0.0, 0.0);
        [unroll]
        for (int i = 0; i < 6; i++) {
            float w = (i < 3) ? 0.65 : 0.35;
            float3 s = sampleScene(sampleUv + bloomDirs[i] * texel);
            glowAcc += max(float3(0.0, 0.0, 0.0), s - 0.34) * w;
        }
        glowAcc /= 3.0;
        float3 bloomTint = glowAcc * float3(1.06 + uVhs * 0.14, 1.0, 1.14 + uVhs * 0.16);
        baseCol = 1.0 - (1.0 - baseCol) * (1.0 - clamp(bloomTint * uBloom * 0.72, 0.0, 0.92));
    }

    if (sweepMask > 0.001) {
        float edgeSplit = ss(-0.04, 0.04, sweepSigned);
        float3 sweepCol = lerp(float3(0.14, 0.85, 1.0), float3(0.95, 0.22, 0.62), edgeSplit);
        baseCol += lerp(sweepCol, float3(0.45, 0.94, 1.0), 0.35) * (sweepMask * 0.34);
    }

    // Physical-pixel locked scanlines (zero moire on any DPI; trig-free smooth bell curve) & aperture grille
    if (uScanlines > 0.001) {
        float phaseY = frac(input.pos.y / max(2.0, uScanPeriod));
        float wBell = abs(phaseY * 2.0 - 1.0);
        float waveY = wBell * wBell * (3.0 - 2.0 * wBell);
        float interlace = uAnimate > 0.5 ? (0.95 + 0.05 * sin(uTime * 52.0)) : 1.0;
        float scanMul = 1.0 - uScanlines * 0.36 * (1.0 - waveY) * interlace;
        baseCol *= scanMul;

        float sub = fmod(floor(input.pos.x), 3.0);
        float3 grille = sub < 0.5
            ? float3(1.05, 0.96, 0.99)
            : (sub < 1.5 ? float3(0.96, 1.05, 0.99) : float3(0.98, 0.97, 1.06));
        baseCol *= lerp(float3(1.0, 1.0, 1.0), grille, uScanlines * 0.28);
    }

    if (uVhs > 0.001) {
        float frameSeed = uAnimate > 0.5 ? floor(uTime * 16.0) : 1.0;
        float n = hash12(floor(input.pos.xy * 0.75) + frameSeed * 19.7) - 0.5;
        baseCol += float3(n * 1.06, n * 0.96, n * 1.12) * (0.075 * uVhs);
    }

    if (uVignette > 0.001) {
        float dist = length(centered * float2(0.84, 0.94));
        float vig = 1.0 - uVignette * 0.78 * ss(0.55, 1.32, dist);
        baseCol *= max(0.08, vig);

        float2 glCrtUv = float2(crtUv.x, 1.0 - crtUv.y);
        float sheenAspect = clamp(rawAspect / 1.7778, 0.70, 2.2);
        float2 sheenV = (glCrtUv - float2(0.18, 0.85)) * float2(1.4 * sheenAspect, 2.2);
        float sheen = exp(-dot(sheenV, sheenV) * 3.5);
        baseCol += float3(0.18, 0.28, 0.36) * sheen * (0.08 * uVignette + 0.03 * uCurvature);
    }

    if (uCurvature > 0.001) {
        float curvBlend = min(1.0, uCurvature * 4.0);
        float shadowPx = max(6.0, (9.0 + 11.0 * uCurvature) * pxScale);
        float innerShadow = ss(-shadowPx, 0.0, sdfPx);
        baseCol *= 1.0 - innerShadow * (0.28 + 0.34 * uCurvature) * curvBlend;
        float rimWidthPx = max(1.4, 1.8 * pxScale);
        float rimGlow = ss(rimWidthPx, 0.0, abs(sdfPx)) * (0.14 + 0.22 * uCurvature) * curvBlend;
        baseCol += float3(0.71, 0.22, 0.95) * rimGlow;
    }

    return float4(lerp(bezelCol, saturate(baseCol), glassMask), 1.0);
}
