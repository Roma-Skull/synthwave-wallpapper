cbuffer SceneParams : register(b0) {
    float2 uResolution;
    float  uTime;
    float  uGridTime;
};

struct PSIn {
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

float ss(float edge0, float edge1, float x) {
    float t = saturate((x - edge0) / (edge1 - edge0));
    return t * t * (3.0 - 2.0 * t);
}

float hash11(float p) {
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float4 hash14(float2 p) {
    float4 p4 = frac(float4(p.xyxy) * float4(0.1031, 0.1030, 0.0973, 0.1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

float sdSegment(float2 pt, float2 a, float2 b) {
    float2 pa = pt - a;
    float2 ba = b - a;
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

float3 renderStarfield(float2 skyUv, float yAboveHor, float time) {
    float2 grid = skyUv * float2(38.0, 22.0);
    float2 cell = floor(grid);
    float2 f = frac(grid) - 0.5;
    float4 rnd = hash14(cell);
    if (rnd.x < 0.72) return float3(0.0, 0.0, 0.0);

    float2 offset = (rnd.yz - 0.5) * 0.68;
    float2 dVec = f - offset;
    float d = length(dVec);
    float size = lerp(0.022, 0.055, frac(rnd.w * 7.3));
    float core = ss(size, 0.0, d);

    // Brighter stars receive delicate 4-point synthwave cross sparkles
    float flare = 0.0;
    if (rnd.x > 0.92) {
        float crossH = exp(-abs(dVec.x) * 28.0) * exp(-abs(dVec.y) * 95.0);
        float crossV = exp(-abs(dVec.y) * 28.0) * exp(-abs(dVec.x) * 95.0);
        flare = (crossH + crossV) * 0.75;
    }

    float twinkle = 0.52 + 0.48 * sin(time * (1.1 + rnd.w * 2.5) + rnd.y * 6.2831853);
    float skyFade = ss(0.03, 0.24, yAboveHor) * ss(0.56, 0.40, yAboveHor);
    float3 tint = rnd.w < 0.35
        ? float3(0.45, 0.95, 1.0)
        : (rnd.w < 0.65 ? float3(1.0, 0.55, 0.88) : float3(0.92, 0.94, 1.0));
    return tint * (core + flare) * twinkle * skyFade * 1.15;
}

float3 renderMountains(float2 p, float2 sunCenter, float pxScale, float3 bgCol, float time) {
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

    float2 vL = float2(xL, hL);
    float2 vP = float2(xP, hP);
    float2 vR = float2(xR, hR);
    float2 vBase = float2((idx + 0.5) * cellW, 0.0);
    float2 pt = float2(xAbs, p.y);

    float ridgeY = pt.x < vP.x
        ? lerp(vL.y, vP.y, clamp((pt.x - vL.x) / max(1e-5, vP.x - vL.x), 0.0, 1.0))
        : lerp(vP.y, vR.y, clamp((pt.x - vP.x) / max(1e-5, vR.x - vP.x), 0.0, 1.0));

    float inside = 1.0 - ss(ridgeY - pxScale * 1.1, ridgeY + pxScale * 1.1, pt.y);

    float dRidge = min(sdSegment(pt, vL, vP), sdSegment(pt, vP, vR));
    float dMesh = min(
        sdSegment(pt, vP, vBase),
        min(sdSegment(pt, vL, vBase), sdSegment(pt, vR, vBase))
    );

    float wireRidge = 1.0 - ss(pxScale * 0.7, pxScale * 2.1, dRidge);
    float wireInner = (1.0 - ss(pxScale * 0.45, pxScale * 1.6, dMesh)) * inside;

    // Subtle low-poly facet tinting + cyan-to-magenta neon rim
    float facet = pt.x < vP.x ? 0.0 : 1.0;
    float3 darkFill = lerp(float3(0.032, 0.014, 0.072), float3(0.052, 0.020, 0.115), facet);
    float sunProx = exp(-length(p - sunCenter) * 2.4);
    darkFill += float3(0.14, 0.02, 0.12) * sunProx * 0.38;

    float pulse = 0.88 + 0.12 * sin(time * 1.5 - xAbs * 5.0);
    float3 cyanWire = float3(0.0, 0.94, 1.0);
    float3 magWire = float3(0.88, 0.22, 0.96);
    float3 wireCol = lerp(magWire, cyanWire, ss(0.22, 0.72, xAbs) * 0.65 + ss(0.01, 0.09, pt.y) * 0.35);

    float3 mtnCol = darkFill + wireCol * (wireRidge * 0.95 + wireInner * 0.55) * pulse;
    float3 res = lerp(bgCol, mtnCol, inside);
    res += cyanWire * wireRidge * 0.35 * (1.0 - inside) * pulse;
    return res;
}

float4 main(PSIn input) : SV_Target {
    // Convert D3D UV (0=top) to bottom-up coordinate space matching the original WebGL2 shader
    float2 glUv = float2(input.uv.x, 1.0 - input.uv.y);

    float aspect = uResolution.x / max(1.0, uResolution.y);
    float horY = 0.44;
    float viewH = max(1.0, 0.95 / max(0.35, aspect));
    float pxScale = viewH / min(uResolution.x, uResolution.y);
    float2 p = float2((glUv.x - 0.5) * aspect * viewH, (glUv.y - horY) * viewH);
    float2 sunCenter = float2(0.0, 0.062);

    // Precompute floor grid derivatives in uniform control flow
    float yFloor = max(0.0022, -p.y);
    float depth = 0.25 / yFloor;
    float worldX = p.x * depth * 1.15;
    float worldZ = depth - uGridTime * 1.45;
    float2 gCoord = float2(worldX, worldZ);
    float2 gDeriv = max(float2(0.0008, 0.0008), fwidth(gCoord));

    // 1. Deep Synthwave Twilight Sky & Horizon Nebula
    float3 skyTop = float3(0.0431, 0.0275, 0.0941);
    float3 skyMid = float3(0.088, 0.039, 0.195);
    float3 skyHor = float3(0.245, 0.055, 0.305);
    float tSky = clamp((glUv.y - horY) / (1.0 - horY), 0.0, 1.0);
    float3 col = lerp(skyHor, skyMid, ss(0.0, 0.36, tSky));
    col = lerp(col, skyTop, ss(0.26, 0.92, tSky));

    // Subtle atmospheric aurora bands
    float aurora = sin(p.x * 3.1 + uTime * 0.22) * cos(p.y * 7.5 - uTime * 0.16);
    col += float3(0.045, 0.012, 0.075) * max(0.0, aurora) * (1.0 - tSky);

    // 2. Retro Sliced Outrun Sun + Breathing Corona
    float2 pSun = p - sunCenter;
    float sunR = 0.188;
    float sunDist = length(pSun);
    float sunBreath = 1.0 + 0.045 * sin(uTime * 1.35);

    float corona = exp(-max(0.0, sunDist - sunR) * 9.5) * 0.65 * sunBreath;
    float wideGlow = exp(-length(pSun * float2(0.75, 1.45)) * 2.9) * 0.42;
    col += float3(1.0, 0.16, 0.52) * (corona + wideGlow);

    if (p.y >= -0.002) {
        float2 starCoord = float2(p.x, p.y + horY);
        float starFade = ss(0.03, 0.24, tSky) * ss(0.98, 0.72, tSky);
        col += renderStarfield(starCoord, 0.30, uTime) * (starFade / max(1e-4, ss(0.03, 0.24, 0.30) * ss(0.56, 0.40, 0.30)))
            * ss(sunR * 0.95, sunR * 1.45, sunDist);

        float discMask = (1.0 - ss(sunR - pxScale * 1.5, sunR + pxScale * 1.5, sunDist))
            * ss(-pxScale, pxScale * 1.5, p.y);
        if (discMask > 0.001) {
            float hNorm = clamp((pSun.y + sunR) / (2.0 * sunR), 0.0, 1.0);
            float3 sunTopCol = float3(1.0, 0.91, 0.37);
            float3 sunMidCol = float3(1.0, 0.42, 0.26);
            float3 sunBotCol = float3(1.0, 0.165, 0.52);
            float3 discCol = lerp(sunBotCol, sunMidCol, ss(0.08, 0.52, hNorm));
            discCol = lerp(discCol, sunTopCol, ss(0.45, 0.92, hNorm));

            // Smoothly drifting downward horizontal scan-slices across visible lower sun above horizon
            float visH = clamp((hNorm - 0.335) / 0.35, 0.0, 1.0);
            float slicePhase = frac(visH * 5.2 + uTime * 0.28);
            float sliceThickness = lerp(0.42, 0.04, visH);
            float sliceGap = ss(sliceThickness + 0.04, sliceThickness - 0.015, slicePhase)
                * step(visH, 0.96)
                * ss(0.96, 0.80, visH);
            col = lerp(col, discCol, discMask * (1.0 - sliceGap));
        }

        // 3. Low-Poly Wireframe Mountains
        col = renderMountains(p, sunCenter, pxScale, col, uTime);
    } else {
        // 4. Infinite Forward-Scrolling Perspective Neon Grid
        float yFloorRaw = -p.y;
        float2 gFrac = abs(frac(gCoord - 0.5) - 0.5) / gDeriv;
        float dLine = min(gFrac.x, gFrac.y);
        float lineCore = 1.0 - min(dLine, 1.0);
        float lineHalo = exp(-dLine * 0.68) * 0.28;
        float distFade = ss(0.004, 0.032, yFloorRaw);

        // Floor ambient gradient + animated sun shimmer trail across the center grid
        float3 floorNear = float3(0.039, 0.018, 0.088);
        float3 floorHor = float3(0.095, 0.026, 0.205);
        col = lerp(floorHor, floorNear, ss(0.0, 0.34 * viewH, yFloorRaw));

        float sunTrack = exp(-p.x * p.x * 16.0) * ss(0.42 * viewH, 0.0, yFloorRaw);
        float ripple = 0.85 + 0.15 * sin(worldZ * 2.2 + uGridTime * 2.6);
        col += float3(0.55, 0.12, 0.42) * sunTrack * 0.48 * ripple;

        float3 gridCyan = float3(0.0, 0.94, 1.0);
        float3 gridMag = float3(1.0, 0.18, 0.72);
        float3 gridCol = lerp(gridMag, gridCyan, exp(-yFloorRaw * 5.2 / viewH));
        col += gridCol * (lineCore * 0.95 + lineHalo) * distFade;
    }

    // 5. Electric Cyan Horizon Beam
    float horGlow = exp(-abs(p.y) * 140.0 / viewH);
    float horCore = exp(-abs(p.y) * 520.0 / viewH);
    col += float3(0.0, 0.94, 1.0) * (horGlow * 0.52 + horCore * 0.85);

    return float4(saturate(col), 1.0);
}
