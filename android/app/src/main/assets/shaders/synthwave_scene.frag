#version 300 es
precision highp float;
precision highp int;

uniform sampler2D bgCacheTex;
uniform vec2 uResolution;
uniform float uTime;
uniform float uGridTime;
uniform float uLayerMode;

in vec2 vUv;
out vec4 fragColor;

float ss(float edge0, float edge1, float x) {
    float t = clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

float hash11(float p) {
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

vec4 hash14(vec2 p) {
    vec4 p4 = fract(vec4(p.xyxy) * vec4(0.1031, 0.1030, 0.0973, 0.1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return fract((p4.xxyz + p4.yzzw) * p4.zywx);
}

float sdSegment(vec2 pt, vec2 a, vec2 b) {
    vec2 pa = pt - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / max(1e-6, dot(ba, ba)), 0.0, 1.0);
    return length(pa - ba * h);
}

float mtnHeight(float idx, float side) {
    float x = abs(idx) * 0.105;
    float envelope = ss(0.20, 0.55, x) * (0.55 + 0.45 * hash11(idx * 3.17 + side * 19.7));
    float peak = (0.042 + 0.088 * hash11(idx * 7.41 + side * 11.3)) * envelope;
    return peak;
}

float mtnPeakOffset(float idx, float side) {
    return (hash11(idx * 5.23 + side * 31.1) - 0.5) * 0.042;
}

vec3 renderStarfield(vec2 skyUv, float yAboveHor, float time) {
    vec2 grid = skyUv * vec2(38.0, 22.0);
    vec2 cell = floor(grid);
    vec2 f = fract(grid) - 0.5;
    vec4 rnd = hash14(cell);
    if (rnd.x < 0.72) return vec3(0.0, 0.0, 0.0);

    vec2 offset = (rnd.yz - 0.5) * 0.68;
    vec2 dVec = f - offset;
    float d = length(dVec);
    float size = mix(0.022, 0.055, fract(rnd.w * 7.3));
    float core = ss(size, 0.0, d);

    float flare = 0.0;
    if (rnd.x > 0.92) {
        float crossH = exp(-abs(dVec.x) * 28.0) * exp(-abs(dVec.y) * 95.0);
        float crossV = exp(-abs(dVec.y) * 28.0) * exp(-abs(dVec.x) * 95.0);
        flare = (crossH + crossV) * 0.75;
    }

    float twinkle = 0.52 + 0.48 * sin(time * (1.1 + rnd.w * 2.5) + rnd.y * 6.2831853);
    float skyFade = ss(0.03, 0.24, yAboveHor) * ss(0.56, 0.40, yAboveHor);
    vec3 tint = rnd.w < 0.35
        ? vec3(0.45, 0.95, 1.0)
        : (rnd.w < 0.65 ? vec3(1.0, 0.55, 0.88) : vec3(0.92, 0.94, 1.0));
    return tint * (core + flare) * twinkle * skyFade * 1.15;
}

vec3 renderMountains(vec2 p, vec2 sunCenter, float pxScale, vec3 bgCol, float time) {
    if (p.y < 0.0 || abs(p.x) < 0.19) return bgCol;

    float side = p.x >= 0.0 ? 1.0 : -1.0;
    float xAbs = abs(p.x);
    float cellW = 0.105;
    float idx = floor(xAbs / cellW);

    float xL = idx * cellW;
    float xR = (idx + 1.0) * cellW;
    float hL = mtnHeight(idx, side);
    float hR = mtnHeight(idx + 1.0, side);
    float xP = (idx + 0.5) * cellW + mtnPeakOffset(idx, side);
    float hP = mtnHeight(idx + 0.5, side) * 1.24;

    vec2 vL = vec2(xL, hL);
    vec2 vP = vec2(xP, hP);
    vec2 vR = vec2(xR, hR);
    vec2 vBase = vec2((idx + 0.5) * cellW, 0.0);
    vec2 pt = vec2(xAbs, p.y);

    float ridgeY = pt.x < vP.x
        ? mix(vL.y, vP.y, clamp((pt.x - vL.x) / max(1e-5, vP.x - vL.x), 0.0, 1.0))
        : mix(vP.y, vR.y, clamp((pt.x - vP.x) / max(1e-5, vR.x - vP.x), 0.0, 1.0));

    float inside = 1.0 - ss(ridgeY - pxScale * 1.1, ridgeY + pxScale * 1.1, pt.y);

    float dRidge = min(sdSegment(pt, vL, vP), sdSegment(pt, vP, vR));
    float dMesh = min(
        sdSegment(pt, vP, vBase),
        min(sdSegment(pt, vL, vBase), sdSegment(pt, vR, vBase))
    );

    float wireRidge = 1.0 - ss(pxScale * 0.7, pxScale * 2.1, dRidge);
    float wireInner = (1.0 - ss(pxScale * 0.45, pxScale * 1.6, dMesh)) * inside;

    float facet = pt.x < vP.x ? 0.0 : 1.0;
    vec3 darkFill = mix(vec3(0.032, 0.014, 0.072), vec3(0.052, 0.020, 0.115), facet);
    float sunProx = exp(-length(p - sunCenter) * 2.4);
    darkFill += vec3(0.14, 0.02, 0.12) * sunProx * 0.38;

    float pulse = 0.88 + 0.12 * sin(time * 1.5 - xAbs * 5.0);
    vec3 cyanWire = vec3(0.0, 0.94, 1.0);
    vec3 magWire = vec3(0.88, 0.22, 0.96);
    vec3 wireCol = mix(magWire, cyanWire, ss(0.22, 0.72, xAbs) * 0.65 + ss(0.01, 0.09, pt.y) * 0.35);

    vec3 mtnCol = darkFill + wireCol * (wireRidge * 0.95 + wireInner * 0.55) * pulse;
    vec3 res = mix(bgCol, mtnCol, inside);
    res += cyanWire * wireRidge * 0.35 * (1.0 - inside) * pulse;
    return res;
}

void main() {
    vec2 glUv = vUv;

    float aspect = uResolution.x / max(1.0, uResolution.y);
    float horY = 0.44;
    float viewH = max(1.0, 0.95 / max(0.15, aspect));
    float pxScale = viewH / max(1.0, uResolution.y);
    vec2 p = vec2((glUv.x - 0.5) * aspect * viewH, (glUv.y - horY) * viewH);
    vec2 sunCenter = vec2(0.0, 0.062);

    if (uLayerMode > 0.5 && uLayerMode < 1.5) {
        if (p.y < -0.006) {
            fragColor = vec4(0.0, 0.0, 0.0, 0.0);
            return;
        }

        vec3 skyTop = vec3(0.0431, 0.0275, 0.0941);
        vec3 skyMid = vec3(0.088, 0.039, 0.195);
        vec3 skyHor = vec3(0.245, 0.055, 0.305);
        float tSky = clamp((glUv.y - horY) / (1.0 - horY), 0.0, 1.0);
        vec3 col = mix(skyHor, skyMid, ss(0.0, 0.36, tSky));
        col = mix(col, skyTop, ss(0.26, 0.92, tSky));

        float aurora = sin(p.x * 3.1 + uTime * 0.22) * cos(p.y * 7.5 - uTime * 0.16);
        col += vec3(0.045, 0.012, 0.075) * max(0.0, aurora) * (1.0 - tSky);

        vec2 pSun = p - sunCenter;
        float sunR = 0.188;
        float sunDist = length(pSun);
        float sunBreath = 1.0 + 0.045 * sin(uTime * 1.35);

        float corona = exp(-max(0.0, sunDist - sunR) * 9.5) * 0.65 * sunBreath;
        float wideGlow = exp(-length(pSun * vec2(0.75, 1.45)) * 2.9) * 0.42;
        col += vec3(1.0, 0.16, 0.52) * (corona + wideGlow);

        vec2 starCoord = vec2(p.x, p.y + horY);
        float starFade = ss(0.03, 0.24, tSky) * ss(0.98, 0.72, tSky);
        col += renderStarfield(starCoord, 0.30, uTime) * (starFade / max(1e-4, ss(0.03, 0.24, 0.30) * ss(0.56, 0.40, 0.30)))
            * ss(sunR * 0.95, sunR * 1.45, sunDist);

        float discMask = (1.0 - ss(sunR - pxScale * 1.5, sunR + pxScale * 1.5, sunDist))
            * ss(-pxScale, pxScale * 1.5, p.y);
        if (discMask > 0.001) {
            float hNorm = clamp((pSun.y + sunR) / (2.0 * sunR), 0.0, 1.0);
            vec3 sunTopCol = vec3(1.0, 0.91, 0.37);
            vec3 sunMidCol = vec3(1.0, 0.42, 0.26);
            vec3 sunBotCol = vec3(1.0, 0.165, 0.52);
            vec3 discCol = mix(sunBotCol, sunMidCol, ss(0.08, 0.52, hNorm));
            discCol = mix(discCol, sunTopCol, ss(0.45, 0.92, hNorm));

            float visH = clamp((hNorm - 0.335) / 0.35, 0.0, 1.0);
            float slicePhase = fract(visH * 5.2 + uTime * 0.28);
            float sliceThickness = mix(0.42, 0.04, visH);
            float sliceGap = ss(sliceThickness + 0.04, sliceThickness - 0.015, slicePhase)
                * step(visH, 0.96)
                * ss(0.96, 0.80, visH);
            col = mix(col, discCol, discMask * (1.0 - sliceGap));
        }

        col = renderMountains(p, sunCenter, pxScale, col, uTime);
        fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
        return;
    }

    // Precompute floor grid derivatives in uniform control flow
    float yFloor = max(0.0022, -p.y);
    float depth = 0.25 / yFloor;
    float worldX = p.x * depth * 1.15;
    float worldZ = depth - uGridTime * 1.45;
    vec2 gCoord = vec2(worldX, worldZ);
    vec2 gDeriv = max(vec2(0.0008, 0.0008), fwidth(gCoord));

    vec3 col = vec3(0.0, 0.0, 0.0);

    if (p.y >= -0.002) {
        if (uLayerMode >= 1.5) {
            col = textureLod(bgCacheTex, vUv, 0.0).rgb;
        } else {
            // 1. Deep Synthwave Twilight Sky & Horizon Nebula
            vec3 skyTop = vec3(0.0431, 0.0275, 0.0941);
            vec3 skyMid = vec3(0.088, 0.039, 0.195);
            vec3 skyHor = vec3(0.245, 0.055, 0.305);
            float tSky = clamp((glUv.y - horY) / (1.0 - horY), 0.0, 1.0);
            col = mix(skyHor, skyMid, ss(0.0, 0.36, tSky));
            col = mix(col, skyTop, ss(0.26, 0.92, tSky));

            float aurora = sin(p.x * 3.1 + uTime * 0.22) * cos(p.y * 7.5 - uTime * 0.16);
            col += vec3(0.045, 0.012, 0.075) * max(0.0, aurora) * (1.0 - tSky);

            // 2. Retro Sliced Outrun Sun + Breathing Corona
            vec2 pSun = p - sunCenter;
            float sunR = 0.188;
            float sunDist = length(pSun);
            float sunBreath = 1.0 + 0.045 * sin(uTime * 1.35);

            float corona = exp(-max(0.0, sunDist - sunR) * 9.5) * 0.65 * sunBreath;
            float wideGlow = exp(-length(pSun * vec2(0.75, 1.45)) * 2.9) * 0.42;
            col += vec3(1.0, 0.16, 0.52) * (corona + wideGlow);

            vec2 starCoord = vec2(p.x, p.y + horY);
            float starFade = ss(0.03, 0.24, tSky) * ss(0.98, 0.72, tSky);
            col += renderStarfield(starCoord, 0.30, uTime) * (starFade / max(1e-4, ss(0.03, 0.24, 0.30) * ss(0.56, 0.40, 0.30)))
                * ss(sunR * 0.95, sunR * 1.45, sunDist);

            float discMask = (1.0 - ss(sunR - pxScale * 1.5, sunR + pxScale * 1.5, sunDist))
                * ss(-pxScale, pxScale * 1.5, p.y);
            if (discMask > 0.001) {
                float hNorm = clamp((pSun.y + sunR) / (2.0 * sunR), 0.0, 1.0);
                vec3 sunTopCol = vec3(1.0, 0.91, 0.37);
                vec3 sunMidCol = vec3(1.0, 0.42, 0.26);
                vec3 sunBotCol = vec3(1.0, 0.165, 0.52);
                vec3 discCol = mix(sunBotCol, sunMidCol, ss(0.08, 0.52, hNorm));
                discCol = mix(discCol, sunTopCol, ss(0.45, 0.92, hNorm));

                float visH = clamp((hNorm - 0.335) / 0.35, 0.0, 1.0);
                float slicePhase = fract(visH * 5.2 + uTime * 0.28);
                float sliceThickness = mix(0.42, 0.04, visH);
                float sliceGap = ss(sliceThickness + 0.04, sliceThickness - 0.015, slicePhase)
                    * step(visH, 0.96)
                    * ss(0.96, 0.80, visH);
                col = mix(col, discCol, discMask * (1.0 - sliceGap));
            }

            // 3. Low-Poly Wireframe Mountains
            col = renderMountains(p, sunCenter, pxScale, col, uTime);
        }
    } else {
        // 4. Infinite Forward-Scrolling Perspective Neon Grid
        float yFloorRaw = -p.y;
        vec2 gFrac = abs(fract(gCoord - 0.5) - 0.5) / gDeriv;
        float dLine = min(gFrac.x, gFrac.y);
        float lineCore = 1.0 - min(dLine, 1.0);
        float lineHalo = exp(-dLine * 0.68) * 0.28;
        float distFade = ss(0.004, 0.032, yFloorRaw);

        vec3 floorNear = vec3(0.039, 0.018, 0.088);
        vec3 floorHor = vec3(0.095, 0.026, 0.205);
        col = mix(floorHor, floorNear, ss(0.0, 0.34 * viewH, yFloorRaw));

        float sunTrack = exp(-p.x * p.x * 16.0) * ss(0.42 * viewH, 0.0, yFloorRaw);
        float ripple = 0.85 + 0.15 * sin(worldZ * 2.2 + uGridTime * 2.6);
        col += vec3(0.55, 0.12, 0.42) * sunTrack * 0.48 * ripple;

        vec3 gridCyan = vec3(0.0, 0.94, 1.0);
        vec3 gridMag = vec3(1.0, 0.18, 0.72);
        vec3 gridCol = mix(gridMag, gridCyan, exp(-yFloorRaw * 5.2 / viewH));
        col += gridCol * (lineCore * 0.95 + lineHalo) * distFade;
    }

    // 5. Electric Cyan Horizon Beam
    float horGlow = exp(-abs(p.y) * 140.0 / viewH);
    float horCore = exp(-abs(p.y) * 520.0 / viewH);
    col += vec3(0.0, 0.94, 1.0) * (horGlow * 0.52 + horCore * 0.85);

    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
