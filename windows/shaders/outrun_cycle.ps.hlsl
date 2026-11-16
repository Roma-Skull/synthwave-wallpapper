Texture2D bgCacheTex : register(t0);
SamplerState linearClamp : register(s0);

cbuffer SceneParams : register(b0) {
    float2 uResolution;
    float  uTime;
    float  uGridTime;
    float  uLayerMode;
    float3 _pad;
};

struct PSIn {
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

float ss(float edge0, float edge1, float x) {
    float t = saturate((x - edge0) / (edge1 - edge0));
    return t * t * (3.0 - 2.0 * t);
}

float smin(float a, float b, float k) {
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return lerp(b, a, h) - k * h * (1.0 - h);
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

float sdTaperedCapsule(float2 pt, float2 a, float2 b, float rA, float rB) {
    float2 pa = pt - a;
    float2 ba = b - a;
    float h = clamp(dot(pa, ba) / max(1e-6, dot(ba, ba)), 0.0, 1.0);
    return length(pa - ba * h) - lerp(rA, rB, h);
}

float sdRoundedBox(float2 pt, float2 halfSize, float radius) {
    float2 q = abs(pt) - halfSize + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

float sdSegSq(float2 pt, float2 a, float2 b) {
    float2 pa = pt - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(1e-6, dot(ba, ba)));
    float2 d = pa - ba * h;
    return dot(d, d);
}

float sdArc2Sq(float2 pt, float2 a, float2 ctrl, float2 b) {
    float2 mid = 0.25 * a + 0.50 * ctrl + 0.25 * b;
    return min(sdSegSq(pt, a, mid), sdSegSq(pt, mid, b));
}

void evalCycleRibbonSq(float2 b, out float dMainSq, out float dFineSq) {
    float dRearSq = min(
        sdArc2Sq(b, float2(-0.530, 0.495), float2(-0.350, 0.518), float2(-0.295, 0.485)),
        sdArc2Sq(b, float2(-0.295, 0.485), float2(-0.375, 0.395), float2(-0.285, 0.255))
    );
    float dFlankSq = min(
        sdArc2Sq(b, float2(-0.145, 0.335), float2(0.080, 0.355), float2(0.255, 0.330)),
        sdArc2Sq(b, float2(0.255, 0.330), float2(0.330, 0.220), float2(0.295, 0.078))
    );
    float dSpineSq = sdArc2Sq(b, float2(-0.295, 0.485), float2(-0.140, 0.412), float2(0.145, 0.502));
    float dArmSq = sdArc2Sq(b, float2(0.145, 0.502), float2(0.245, 0.405), float2(0.312, 0.285));
    dMainSq = min(dRearSq, min(dFlankSq, min(dSpineSq, dArmSq)));

    float dNoseSq = min(
        sdArc2Sq(b, float2(0.775, 0.405), float2(0.848, 0.245), float2(0.812, 0.125)),
        sdArc2Sq(b, float2(-0.790, 0.390), float2(-0.822, 0.305), float2(-0.810, 0.220))
    );
    float dLegSq = sdArc2Sq(b, float2(-0.145, 0.335), float2(-0.032, 0.286), float2(-0.265, 0.214));
    float dVisorSq = sdSegSq(b, float2(0.258, 0.544), float2(0.324, 0.528));
    dFineSq = min(dNoseSq, min(dLegSq, dVisorSq));
}

float3 renderStarfield(float2 skyUv, float yNorm, float time) {
    float2 grid = skyUv * float2(38.0, 24.0);
    float2 cell = floor(grid);
    float2 f = frac(grid) - 0.5;
    float4 rnd = hash14(cell);
    if (rnd.x < 0.72) return float3(0.0, 0.0, 0.0);

    float2 offset = (rnd.yz - 0.5) * 0.68;
    float2 dVec = f - offset;
    float d = length(dVec);
    float size = lerp(0.020, 0.052, frac(rnd.w * 7.3));
    float core = ss(size, 0.0, d);

    float flare = 0.0;
    if (rnd.x > 0.93) {
        float crossH = exp(-abs(dVec.x) * 30.0) * exp(-abs(dVec.y) * 95.0);
        float crossV = exp(-abs(dVec.y) * 30.0) * exp(-abs(dVec.x) * 95.0);
        flare = (crossH + crossV) * 0.65;
    }

    float twinkle = 0.58 + 0.42 * sin(time * (1.1 + rnd.w * 2.2) + rnd.y * 6.2831853);
    float skyFade = ss(0.18, 0.42, yNorm) * ss(0.99, 0.75, yNorm);
    float3 tint = rnd.w < 0.35
        ? float3(0.65, 0.94, 1.0)
        : float3(0.96, 0.97, 1.0);
    return tint * (core + flare) * twinkle * skyFade * 1.15;
}

float mtnRidgeHeight(float idx) {
    float macro = pow(0.5 + 0.5 * cos(idx * 0.76), 1.6);
    float mid = hash11(idx * 11.73 + 3.1);
    float jagged = hash11(idx * 29.41 + 7.9);
    return lerp(0.055, 0.225, saturate(macro * 0.62 + mid * 0.26 + jagged * 0.12));
}

float2 mtnMeshVertex(float2 ij) {
    float4 rnd = hash14(ij * 1.37 + float2(5.1, 13.7));
    float jx = (rnd.x - 0.5) * 0.44;
    float jy = (ij.y <= 0.5) ? 0.0 : (rnd.y - 0.5) * 0.48;
    return ij + float2(jx, jy);
}

// Low-poly faceted Alpine cyan wireframe mountains scrolling slowly left across the pink-blue horizon
float3 renderWireframeMountains(float2 p, float pxScale, float3 bgCol, float gridTime) {
    if (p.y < -0.002 || p.y > 0.32) return bgCol;

    float xScroll = p.x + gridTime * 0.048;
    float cellW = 0.115;
    float idx = floor(xScroll / cellW);
    float xL = idx * cellW;
    float xR = (idx + 1.0) * cellW;
    float hL = mtnRidgeHeight(idx);
    float hR = mtnRidgeHeight(idx + 1.0);
    float xP = (idx + 0.5) * cellW + (hash11(idx * 5.23 + 19.7) - 0.5) * 0.036;
    float hP = mtnRidgeHeight(idx + 0.5) * 1.18 + 0.02;

    float2 vL = float2(xL, hL);
    float2 vP = float2(xP, hP);
    float2 vR = float2(xR, hR);
    float2 pt = float2(xScroll, p.y);

    float ridgeY = pt.x < vP.x
        ? lerp(vL.y, vP.y, clamp((pt.x - vL.x) / max(1e-5, vP.x - vL.x), 0.0, 1.0))
        : lerp(vP.y, vR.y, clamp((pt.x - vP.x) / max(1e-5, vR.x - vP.x), 0.0, 1.0));

    float inside = (1.0 - ss(ridgeY - pxScale * 1.1, ridgeY + pxScale * 1.1, pt.y)) * step(0.0, p.y);
    float dRidge = min(sdSegment(pt, vL, vP), sdSegment(pt, vP, vR));
    float ridgeWire = 1.0 - ss(pxScale * 0.85, pxScale * 2.2, dRidge);

    float3 res = bgCol;
    if (inside > 0.001) {
        // Jittered 3D triangulated wireframe lattice across every mountain face
        float2 meshScale = float2(25.0, 27.0);
        float2 mp = pt * meshScale;
        float2 mCell = floor(mp);

        float2 p00 = mtnMeshVertex(mCell);
        float2 p10 = mtnMeshVertex(mCell + float2(1.0, 0.0));
        float2 p01 = mtnMeshVertex(mCell + float2(0.0, 1.0));
        float2 p11 = mtnMeshVertex(mCell + float2(1.0, 1.0));

        float dEdgesSq = min(
            min(sdSegSq(mp, p00, p10), sdSegSq(mp, p01, p11)),
            min(sdSegSq(mp, p00, p01), sdSegSq(mp, p10, p11))
        );
        float diagFlip = step(0.5, hash11(dot(mCell, float2(17.1, 31.3))));
        float2 dA = diagFlip > 0.5 ? p00 : p10;
        float2 dB = diagFlip > 0.5 ? p11 : p01;
        float dDiagSq = sdSegSq(mp, dA, dB);
        float dMesh = sqrt(min(dEdgesSq, dDiagSq)) / 26.0;

        // Determine per-triangle facet contrast for crisp low-poly 3D depth
        float2 diagDir = dB - dA;
        float2 relP = mp - dA;
        float sideTri = step(0.0, relP.x * diagDir.y - relP.y * diagDir.x);
        float facetRnd = hash11(dot(mCell, float2(23.7, 41.9)) + sideTri * 17.3);

        float3 mtnDark = lerp(float3(0.020, 0.042, 0.115), float3(0.042, 0.095, 0.185), facetRnd * 0.75 + sideTri * 0.25);
        // Subtle pink-violet atmospheric haze toward horizon foot of the mountains
        mtnDark = lerp(mtnDark, float3(0.24, 0.11, 0.34), exp(-max(0.0, p.y) * 18.0) * 0.65);

        float meshWire = 1.0 - ss(pxScale * 0.65, pxScale * 1.85, dMesh);
        float meshHalo = exp(-dMesh / max(1e-4, pxScale * 4.5)) * 0.22;
        float3 iceCyan = float3(0.45, 0.96, 1.0);

        float3 mtnCol = mtnDark + iceCyan * (max(meshWire * 0.86, ridgeWire) + meshHalo);
        res = lerp(res, mtnCol, inside);
    }

    res += float3(0.45, 0.96, 1.0) * ridgeWire * 0.42 * (1.0 - inside);
    return res;
}

// Clean angular Synthwave Outrun-style rider silhouette tucked neatly into the sculpted cockpit (facing RIGHT)
float sdSynthwaveRider(float2 b, out float dHelmetOut) {
    float2 pFoot = float2(-0.275, 0.212);
    float2 pKnee = float2(-0.020, 0.276);
    float2 pHip = float2(-0.185, 0.365);
    float2 pShoulder = float2(0.148, 0.495);
    float2 pHand = float2(0.365, 0.305);

    float dThigh = sdTaperedCapsule(b, pHip, pKnee, 0.050, 0.036);
    float dShin = sdTaperedCapsule(b, pKnee, pFoot, 0.036, 0.022);
    float dLeg = min(dThigh, dShin);

    float dBack = sdTaperedCapsule(b, pHip, pShoulder, 0.054, 0.064);
    float dChest = sdTaperedCapsule(b, float2(-0.04, 0.375), float2(0.148, 0.435), 0.052, 0.048);
    float dTorso = smin(dBack, dChest, 0.014);

    float dArm = sdTaperedCapsule(b, pShoulder, pHand, 0.042, 0.024);
    dHelmetOut = sdTaperedCapsule(b, float2(0.220, 0.550), float2(0.286, 0.526), 0.054, 0.042);

    return smin(dTorso, smin(dArm, smin(dHelmetOut, dLeg, 0.010), 0.010), 0.012);
}

// Unified Light Cycle + visually fused Rider (shared obsidian material & integrated cyan neon flow)
void evalLightCycleAndRider(
    float2 p,
    float2 groundAnchor,
    float bikeScale,
    float pxScale,
    float time,
    float gridTime,
    float isReflection,
    inout float3 col)
{
    float invS = 1.0 / bikeScale;
    float2 b = (p - groundAnchor) * invS;

    if (b.x < -1.15 || b.x > 1.45 || b.y < -0.18 || b.y > 0.90) {
        return;
    }

    float localPx = pxScale * invS;
    float reflAtten = isReflection > 0.5 ? 0.36 * exp(-max(0.0, b.y) * 2.4) * ss(0.84, 0.12, b.y) : 1.0;

    float2 wRear = float2(-0.56, 0.265);
    float2 wFront = float2(0.56, 0.260);
    float rRearOuter = 0.265;
    float rRearHub = 0.138;
    float rRearLight = 0.214;
    float rFrontOuter = 0.260;
    float rFrontHub = 0.134;
    float rFrontLight = 0.210;

    float dCenterRear = length(b - wRear);
    float dCenterFront = length(b - wFront);

    float dRearBackShell = sdTaperedCapsule(b, float2(-0.825, 0.220), float2(-0.740, 0.440), 0.044, 0.056);
    float dRearArch = sdTaperedCapsule(b, float2(-0.740, 0.440), float2(-0.530, 0.515), 0.056, 0.056);
    float dTailFin = sdTaperedCapsule(b, float2(-0.540, 0.528), float2(-0.785, 0.538), 0.018, 0.008);
    float dCanopyWing = sdTaperedCapsule(b, float2(-0.530, 0.505), float2(-0.295, 0.508), 0.052, 0.026);
    float dSeatBack = sdTaperedCapsule(b, float2(-0.340, 0.425), float2(-0.275, 0.275), 0.044, 0.054);

    float dBelly = sdTaperedCapsule(b, float2(-0.360, 0.160), float2(0.320, 0.140), 0.095, 0.088);
    float dWaistTank = sdTaperedCapsule(b, float2(-0.140, 0.305), float2(0.295, 0.395), 0.058, 0.074);
    float dFrontArch = sdTaperedCapsule(b, float2(0.285, 0.395), float2(0.560, 0.525), 0.074, 0.052);
    float dFrontNoseTop = sdTaperedCapsule(b, float2(0.560, 0.525), float2(0.790, 0.405), 0.052, 0.048);
    float dFrontNoseMid = sdTaperedCapsule(b, float2(0.790, 0.405), float2(0.848, 0.210), 0.048, 0.038);
    float dFrontNoseTip = sdTaperedCapsule(b, float2(0.848, 0.210), float2(0.795, 0.110), 0.036, 0.022);
    float dFrontSkirt = sdTaperedCapsule(b, float2(0.300, 0.355), float2(0.315, 0.100), 0.054, 0.036);

    float dRearBody = smin(dRearBackShell, dRearArch, 0.025);
    dRearBody = min(dRearBody, dTailFin);
    dRearBody = smin(dRearBody, smin(dCanopyWing, dSeatBack, 0.028), 0.025);

    float dMidBody = smin(dBelly, dWaistTank, 0.045);
    float dFrontBody = smin(dFrontArch, smin(dFrontNoseTop, smin(dFrontNoseMid, dFrontNoseTip, 0.025), 0.025), 0.030);
    dFrontBody = smin(dFrontBody, dFrontSkirt, 0.035);

    float dChassis = smin(dRearBody, smin(dMidBody, dFrontBody, 0.040), 0.035);
    dChassis = max(dChassis, -(dCenterRear - 0.244));
    dChassis = max(dChassis, -(dCenterFront - 0.240));

    float dBarMount = sdRoundedBox(b - float2(0.355, 0.275), float2(0.034, 0.028), 0.008);
    float dRearPeg = sdTaperedCapsule(b, float2(-0.345, 0.210), float2(-0.275, 0.205), 0.016, 0.012);
    dChassis = min(dChassis, min(dBarMount, dRearPeg));

    float dHelmet = 1.0;
    float dRider = sdSynthwaveRider(b, dHelmet);
    float dBodyFull = smin(dChassis, dRider, 0.014);

    float dRearTire = max(dCenterRear - rRearOuter, -(dCenterRear - 0.190));
    float dFrontTire = max(dCenterFront - rFrontOuter, -(dCenterFront - 0.186));
    float dTires = min(dRearTire, dFrontTire);

    float dRearBarrel = max(dCenterRear - 0.192, -(dCenterRear - rRearHub));
    float dFrontBarrel = max(dCenterFront - 0.188, -(dCenterFront - rFrontHub));
    float dBarrels = min(dRearBarrel, dFrontBarrel);

    // 1) Recessed 3D Metallic Inner Wheel Barrels
    float barrelMask = 1.0 - ss(-localPx, localPx, dBarrels);
    if (barrelMask > 0.001 && isReflection < 0.5) {
        float minB = min(dCenterRear - rRearHub, dCenterFront - rFrontHub);
        float depthRing = clamp(minB / 0.052, 0.0, 1.0);
        float innerBevel = sin(depthRing * 3.14159);
        float cyc = b.y > 0.26 ? 0.85 : 1.15;
        float3 steelCol = float3(0.042, 0.056, 0.095) * cyc + float3(0.08, 0.22, 0.34) * innerBevel;
        steelCol += float3(0.04, 0.58, 0.85) * smoothstep(0.45, 1.0, depthRing) * 0.42;
        col = lerp(col, steelCol, barrelMask * 0.92);
    }

    // 2) Dark Tire Sides & Unified Obsidian Body+Rider Silhouette
    float tireMask = 1.0 - ss(-localPx, localPx, dTires);
    if (tireMask > 0.001) {
        float3 tireCol = float3(0.012, 0.012, 0.028);
        float rimGlow = exp(-abs(min(dCenterRear - rRearOuter, dCenterFront - rFrontOuter)) * 45.0) * 0.12;
        tireCol += float3(0.04, 0.45, 0.72) * rimGlow;
        col = isReflection > 0.5
            ? lerp(col, col * 0.65 + tireCol * 0.35, tireMask * reflAtten * 0.75)
            : lerp(col, tireCol, tireMask);
    }

    float bodyMask = 1.0 - ss(-localPx, localPx, dBodyFull);
    if (bodyMask > 0.001) {
        float3 bodyCol = float3(0.012, 0.018, 0.042);

        float crownRim = ss(0.022, 0.0, abs(dBodyFull)) * ss(0.28, 0.58, b.y);
        float2 vDome = (b - float2(0.56, 0.48)) * float2(3.5, 9.0);
        float2 vTail = (b - float2(-0.56, 0.49)) * float2(4.0, 12.0);
        float2 vHelm = (b - float2(0.24, 0.56)) * float2(7.0, 14.0);
        float domeShine = exp(-dot(vDome, vDome) * 0.9) * 0.28;
        float tailShine = exp(-dot(vTail, vTail) * 0.9) * 0.24;
        float helmShine = exp(-dot(vHelm, vHelm) * 0.9) * 0.22;
        bodyCol += float3(0.18, 0.28, 0.42) * (crownRim * 0.68 + domeShine + tailShine + helmShine);

        float2 vSkirt = (b - float2(-0.27, 0.25)) * float2(7.0, 8.5);
        float skirtGlow = exp(-dot(vSkirt, vSkirt) * 1.1);
        float skirtRib = 0.70 + 0.30 * sin(b.x * 38.0 + b.y * 24.0);
        bodyCol += float3(0.04, 0.68, 0.96) * skirtGlow * skirtRib * 0.34;
        bodyCol += float3(0.42, 0.08, 0.34) * ss(-0.62, -0.82, b.x) * 0.20;

        col = lerp(col, bodyCol, bodyMask);
    }

    // 3) Angled Recessed Core Engine / Circuit Bay
    float2 turbCenter = float2(0.015, 0.185);
    float2 turbRel = b - turbCenter;
    float cAng = cos(-0.08), sAng = sin(-0.08);
    float2 tb = float2(turbRel.x * cAng - turbRel.y * sAng, turbRel.x * sAng + turbRel.y * cAng);
    float dTurb = sdRoundedBox(tb, float2(0.128, 0.048), 0.026);
    float turbRim = ss(localPx * 3.0, 0.0, abs(dTurb));
    float turbMask = 1.0 - ss(-localPx, localPx, dTurb);
    if (turbMask > 0.001) {
        float2 tp = tb / float2(0.128, 0.048);
        float3 bayBg = float3(0.006, 0.016, 0.038);
        float conduitBars = step(0.15, sin(tp.y * 10.5)) * step(-0.78, tp.x) * step(tp.x, 0.78);
        float circuitGap = step(0.12, abs(frac(tp.x * 3.5 + tp.y * 0.8) - 0.5));
        float corePulse = exp(-dot(tp * float2(0.9, 1.05), tp * float2(0.9, 1.05)) * 1.15);
        float fins = conduitBars * circuitGap * (0.75 + 0.25 * sin(tp.x * 12.0 - gridTime * 7.5));
        float3 finGlow = float3(0.06, 0.82, 1.0) * fins * 0.92 + float3(0.85, 0.99, 1.0) * fins * corePulse * 0.78;
        col = lerp(col, bayBg, turbMask);
        col += finGlow * turbMask;
    }
    col += float3(0.10, 0.45, 0.65) * turbRim * 0.45;
    col += float3(0.0, 0.82, 1.0) * exp(-max(0.0, dTurb) * 18.0) * 0.44;

    // 4) Unified Neon Light Ribbons — Bike Fairing + Integrated Rider Circuit Flow + Wheel Rings
    float2 qRear = b - wRear;
    float2 qFront = b - wFront;
    float aRear = atan2(qRear.y, qRear.x);
    float aFront = atan2(qFront.y, qFront.x);
    float spinRear = 0.90 + 0.10 * sin(aRear * 3.0 + gridTime * 18.0);
    float spinFront = 0.90 + 0.10 * sin(aFront * 3.0 + gridTime * 18.5);

    float dRearRing = abs(dCenterRear - rRearLight);
    float dFrontRing = abs(dCenterFront - rFrontLight);
    float rearCore = (1.0 - ss(0.015, 0.015 + localPx * 2.0, dRearRing)) * spinRear;
    float rearGlow = exp(-dRearRing * 26.0) * spinRear;
    float frontCore = (1.0 - ss(0.015, 0.015 + localPx * 2.0, dFrontRing)) * spinFront;
    float frontGlow = exp(-dFrontRing * 26.0) * spinFront;

    float dMainSq, dFineSq;
    evalCycleRibbonSq(b, dMainSq, dFineSq);
    float dMainRibbons = sqrt(dMainSq);
    float dFineRibbons = sqrt(dFineSq);

    float3 neonCyan = float3(0.06, 0.92, 1.0);
    float3 coreWhite = float3(0.88, 0.99, 1.0);

    col += neonCyan * (rearGlow + frontGlow) * 0.95;
    col += coreWhite * max(rearCore, frontCore) * 0.96;

    float mainCore = 1.0 - ss(0.0085, 0.0085 + localPx * 2.0, dMainRibbons);
    float mainGlow = exp(-dMainRibbons * 33.0);
    float fineCore = 1.0 - ss(0.0038, 0.0038 + localPx * 1.4, dFineRibbons);
    float fineGlow = exp(-dFineRibbons * 50.0);

    col += neonCyan * (mainGlow * 0.88 + fineGlow * 0.62);
    col += coreWhite * max(mainCore * 0.92, fineCore * 0.82);

    float2 vHead = (b - float2(0.86, 0.28)) * float2(0.65, 1.8);
    float headBound = ss(1.42, 0.80, b.x) * ss(0.56, 0.25, abs(b.y - 0.26));
    col += float3(0.12, 0.94, 1.0) * exp(-dot(vHead, vHead) * 6.8) * 0.36 * headBound;
}

// Lean ground reflection pass (emissive wheel rings, turbine glow & integrated neon ribbons only)
void evalCycleReflection(
    float2 p,
    float2 groundAnchor,
    float bikeScale,
    float pxScale,
    float gridTime,
    inout float3 col)
{
    float invS = 1.0 / bikeScale;
    float2 b = (p - groundAnchor) * invS;
    if (b.x < -1.00 || b.x > 1.00 || b.y < 0.0 || b.y > 0.72) {
        return;
    }

    float localPx = pxScale * invS;
    float reflAtten = 0.36 * exp(-b.y * 2.4) * ss(0.72, 0.08, b.y);

    float dRearRing = abs(length(b - float2(-0.56, 0.265)) - 0.214);
    float dFrontRing = abs(length(b - float2(0.56, 0.260)) - 0.210);
    float rearGlow = exp(-dRearRing * 26.0);
    float frontGlow = exp(-dFrontRing * 26.0);
    float ringCore = max(1.0 - ss(0.015, 0.015 + localPx * 2.0, dRearRing), 1.0 - ss(0.015, 0.015 + localPx * 2.0, dFrontRing));

    float dMainSq, dFineSq;
    evalCycleRibbonSq(b, dMainSq, dFineSq);
    float dMain = sqrt(dMainSq);
    float dFine = sqrt(dFineSq);

    float2 tb = (b - float2(0.015, 0.185)) / float2(0.135, 0.055);
    float turbGlow = exp(-dot(tb, tb) * 1.4) * (0.65 + 0.35 * sin(tb.y * 9.0));

    float3 neonCyan = float3(0.06, 0.92, 1.0);
    float3 coreWhite = float3(0.88, 0.99, 1.0);
    col += neonCyan * ((rearGlow + frontGlow) * 0.90 + exp(-dMain * 33.0) * 0.85 + exp(-dFine * 50.0) * 0.55 + turbGlow * 0.72) * reflAtten;
    col += coreWhite * max(ringCore * 0.90, 1.0 - ss(0.0085, 0.0085 + localPx * 2.0, dMain)) * reflAtten;
}

float4 main(PSIn input) : SV_Target {
    float2 glUv = float2(input.uv.x, 1.0 - input.uv.y);

    float aspect = uResolution.x / max(1.0, uResolution.y);
    float horY = 0.44;
    float viewH = max(1.0, 0.95 / max(0.15, aspect));
    float pxScale = viewH / max(1.0, uResolution.y);
    float2 p = float2((glUv.x - 0.5) * aspect * viewH, (glUv.y - horY) * viewH);
    float2 sunCenter = float2(0.0, 0.255);

    if (uLayerMode > 0.5 && uLayerMode < 1.5) {
        if (p.y < -0.006) return float4(0.0, 0.0, 0.0, 0.0);

        float3 skyTop = float3(0.015, 0.018, 0.068);
        float3 skyMid = float3(0.045, 0.135, 0.460);
        float3 skyHor = float3(0.560, 0.095, 0.540);
        float tSky = clamp((glUv.y - horY) / (1.0 - horY), 0.0, 1.0);
        float3 col = lerp(skyHor, skyMid, ss(0.02, 0.44, tSky));
        col = lerp(col, skyTop, ss(0.55, 0.96, tSky));

        float2 pSun = p - sunCenter;
        float sunR = 0.165;
        float sunDist = length(pSun);
        float sunBreath = 1.0 + 0.03 * sin(uTime * 1.25);

        float sunHalo = exp(-max(0.0, sunDist - sunR) * 11.0) * 0.45 * sunBreath;
        col += float3(1.0, 0.48, 0.18) * sunHalo;

        float2 starCoord = float2((glUv.x - 0.5) * aspect * viewH, glUv.y * viewH);
        col += renderStarfield(starCoord, tSky, uTime) * ss(sunR * 0.95, sunR * 1.45, sunDist);

        float discMask = 1.0 - ss(sunR - pxScale * 1.4, sunR + pxScale * 1.4, sunDist);
        if (discMask > 0.001) {
            float hNorm = clamp((pSun.y + sunR) / (2.0 * sunR), 0.0, 1.0);
            float3 sunTopCol = float3(1.0, 0.94, 0.24);
            float3 sunBotCol = float3(1.0, 0.68, 0.08);
            float3 discCol = lerp(sunBotCol, sunTopCol, ss(0.05, 0.90, hNorm));

            float bandH = clamp((hNorm - 0.04) / 0.58, 0.0, 1.0);
            float slicePhase = frac(bandH * 6.2 + uTime * 0.22);
            float sliceThickness = lerp(0.42, 0.06, bandH);
            float sliceGap = ss(sliceThickness + 0.035, sliceThickness - 0.015, slicePhase)
                * step(bandH, 0.96)
                * ss(0.96, 0.80, bandH);
            col = lerp(col, discCol, discMask * (1.0 - sliceGap));
        }

        col = renderWireframeMountains(p, pxScale, col, uGridTime);
        return float4(saturate(col), 1.0);
    }

    // Precompute perspective pink floor grid derivatives in uniform control flow
    // Grid lines radiate from horizon vanishing point while scrolling fast LEFT under the riding cycle
    float yFloor = max(0.0022, -p.y);
    float depth = 0.24 / yFloor;
    float worldX = p.x * depth * 1.15 + uGridTime * 1.65;
    float worldZ = depth * 1.05;
    float2 gCoord = float2(worldX, worldZ);
    float2 gDeriv = max(float2(0.0008, 0.0008), fwidth(gCoord));

    float3 col = float3(0.0, 0.0, 0.0);

    if (p.y >= -0.002) {
        if (uLayerMode >= 1.5) {
            col = bgCacheTex.SampleLevel(linearClamp, input.uv, 0).rgb;
        } else {
            // 1. Cosmic Night -> Royal Cobalt Blue -> Horizon Magenta Sky
            float3 skyTop = float3(0.015, 0.018, 0.068);
            float3 skyMid = float3(0.045, 0.135, 0.460);
            float3 skyHor = float3(0.560, 0.095, 0.540);
            float tSky = clamp((glUv.y - horY) / (1.0 - horY), 0.0, 1.0);
            col = lerp(skyHor, skyMid, ss(0.02, 0.44, tSky));
            col = lerp(col, skyTop, ss(0.55, 0.96, tSky));

            // 2. Crisp Golden Sliced Outrun Sun High Behind the Central Wireframe Peak
            float2 pSun = p - sunCenter;
            float sunR = 0.165;
            float sunDist = length(pSun);
            float sunBreath = 1.0 + 0.03 * sin(uTime * 1.25);

            float sunHalo = exp(-max(0.0, sunDist - sunR) * 11.0) * 0.45 * sunBreath;
            col += float3(1.0, 0.48, 0.18) * sunHalo;

            float2 starCoord = float2((glUv.x - 0.5) * aspect * viewH, glUv.y * viewH);
            col += renderStarfield(starCoord, tSky, uTime) * ss(sunR * 0.95, sunR * 1.45, sunDist);

            float discMask = 1.0 - ss(sunR - pxScale * 1.4, sunR + pxScale * 1.4, sunDist);
            if (discMask > 0.001) {
                float hNorm = clamp((pSun.y + sunR) / (2.0 * sunR), 0.0, 1.0);
                float3 sunTopCol = float3(1.0, 0.94, 0.24);
                float3 sunBotCol = float3(1.0, 0.68, 0.08);
                float3 discCol = lerp(sunBotCol, sunTopCol, ss(0.05, 0.90, hNorm));

                // Horizontal sky gaps: thin slits near center widening toward the bottom of the golden sun
                float bandH = clamp((hNorm - 0.04) / 0.58, 0.0, 1.0);
                float slicePhase = frac(bandH * 6.2 + uTime * 0.22);
                float sliceThickness = lerp(0.42, 0.06, bandH);
                float sliceGap = ss(sliceThickness + 0.035, sliceThickness - 0.015, slicePhase)
                    * step(bandH, 0.96)
                    * ss(0.96, 0.80, bandH);
                col = lerp(col, discCol, discMask * (1.0 - sliceGap));
            }

            // 3. Slow-Scrolling Triangulated Cyan Wireframe Mountains
            col = renderWireframeMountains(p, pxScale, col, uGridTime);
        }
    } else {
        // 4. Reflective Hot-Pink Perspective Neon Grid Floor
        float yFloorRaw = -p.y;
        float2 gFrac = abs(frac(gCoord - 0.5) - 0.5) / gDeriv;
        float dLine = min(gFrac.x, gFrac.y);
        float lineCore = 1.0 - min(dLine, 1.0);
        float lineHalo = exp(-dLine * 0.62) * 0.30;
        float distFade = ss(0.003, 0.028, yFloorRaw);

        float3 floorHor = float3(0.460, 0.085, 0.420);
        float3 floorNear = float3(0.095, 0.012, 0.125);
        col = lerp(floorHor, floorNear, ss(0.0, 0.36 * viewH, yFloorRaw));

        float3 pinkLine = float3(1.0, 0.38, 0.95);
        float3 pinkHaloCol = float3(0.95, 0.16, 0.80);
        col += (pinkLine * lineCore * 0.95 + pinkHaloCol * lineHalo) * distFade;
    }

    // 5. Pastel Pink-White Horizon Glow Band along mountain-grid seam
    float horMist = exp(-abs(p.y) * 95.0 / viewH);
    float horCore = exp(-abs(p.y) * 480.0 / viewH);
    col += float3(1.0, 0.72, 0.95) * horMist * 0.62 + float3(1.0, 0.92, 1.0) * horCore * 0.78;

    // 6. Unified Light Cycle + Merged Rider cruising Left-to-Right over the pink grid
    float bounce = sin(uGridTime * 11.0) * 0.0018 + sin(uGridTime * 23.0) * 0.0007;
    float2 bikeGround = float2(0.02 + sin(uTime * 0.55) * 0.026, -0.215 + bounce);
    float bikeScale = 0.34;

    float2 wakeD = p - (bikeGround + float2(-0.19, 0.012));
    if (p.x < bikeGround.x - 0.16 && abs(wakeD.y) < 0.14) {
        float backLen = clamp((bikeGround.x - 0.16 - p.x) / 0.85, 0.0, 1.0);
        float wakeFade = (1.0 - backLen) * (1.0 - backLen);
        float ribbonH = 0.024 * (1.0 - 0.45 * backLen);
        float ribbonCore = 1.0 - ss(ribbonH * 0.22, ribbonH, abs(wakeD.y));
        float ribbonGlow = exp(-abs(wakeD.y) * 52.0);
        float wavePulse = 0.82 + 0.18 * sin(p.x * 24.0 + uGridTime * 16.0);
        col += float3(0.06, 0.92, 1.0) * (ribbonGlow * 0.48 + ribbonCore * 0.52) * wakeFade * wavePulse;
    }

    float2 shD = (p - bikeGround) / float2(0.28, 0.022);
    col *= 1.0 - exp(-dot(shD, shD) * 2.2) * 0.72;
    col += float3(0.0, 0.88, 1.0) * exp(-length((p - bikeGround) * float2(4.2, 34.0)) * 1.1) * 0.30;

    if (p.y < bikeGround.y) {
        float rippleOff = sin(p.y * 190.0 + uGridTime * 8.0) * 0.0022;
        float2 pRefl = float2(p.x + rippleOff, 2.0 * bikeGround.y - p.y);
        evalCycleReflection(pRefl, bikeGround, bikeScale, pxScale, uGridTime, col);
    }

    evalLightCycleAndRider(p, bikeGround, bikeScale, pxScale, uTime, uGridTime, 0.0, col);

    return float4(saturate(col), 1.0);
}
