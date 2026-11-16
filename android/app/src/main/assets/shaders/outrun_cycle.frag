#version 300 es
precision highp float;
precision highp int;

uniform vec2 uResolution;
uniform float uTime;
uniform float uGridTime;

in vec2 vUv;
out vec4 fragColor;

float ss(float edge0, float edge1, float x) {
    float t = clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

float smin(float a, float b, float k) {
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return mix(b, a, h) - k * h * (1.0 - h);
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

float sdTaperedCapsule(vec2 pt, vec2 a, vec2 b, float rA, float rB) {
    vec2 pa = pt - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / max(1e-6, dot(ba, ba)), 0.0, 1.0);
    return length(pa - ba * h) - mix(rA, rB, h);
}

float sdRoundedBox(vec2 pt, vec2 halfSize, float radius) {
    vec2 q = abs(pt) - halfSize + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

float sdSegSq(vec2 pt, vec2 a, vec2 b) {
    vec2 pa = pt - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / max(1e-6, dot(ba, ba)), 0.0, 1.0);
    vec2 d = pa - ba * h;
    return dot(d, d);
}

float sdArc2Sq(vec2 pt, vec2 a, vec2 ctrl, vec2 b) {
    vec2 mid = 0.25 * a + 0.50 * ctrl + 0.25 * b;
    return min(sdSegSq(pt, a, mid), sdSegSq(pt, mid, b));
}

void evalCycleRibbonSq(vec2 b, out float dMainSq, out float dFineSq) {
    float dRearSq = min(
        sdArc2Sq(b, vec2(-0.530, 0.495), vec2(-0.350, 0.518), vec2(-0.295, 0.485)),
        sdArc2Sq(b, vec2(-0.295, 0.485), vec2(-0.375, 0.395), vec2(-0.285, 0.255))
    );
    float dFlankSq = min(
        sdArc2Sq(b, vec2(-0.145, 0.335), vec2(0.080, 0.355), vec2(0.255, 0.330)),
        sdArc2Sq(b, vec2(0.255, 0.330), vec2(0.330, 0.220), vec2(0.295, 0.078))
    );
    float dSpineSq = sdArc2Sq(b, vec2(-0.295, 0.485), vec2(-0.140, 0.412), vec2(0.145, 0.502));
    float dArmSq = sdArc2Sq(b, vec2(0.145, 0.502), vec2(0.245, 0.405), vec2(0.312, 0.285));
    dMainSq = min(dRearSq, min(dFlankSq, min(dSpineSq, dArmSq)));

    float dNoseSq = min(
        sdArc2Sq(b, vec2(0.775, 0.405), vec2(0.848, 0.245), vec2(0.812, 0.125)),
        sdArc2Sq(b, vec2(-0.790, 0.390), vec2(-0.822, 0.305), vec2(-0.810, 0.220))
    );
    float dLegSq = sdArc2Sq(b, vec2(-0.145, 0.335), vec2(-0.032, 0.286), vec2(-0.265, 0.214));
    float dVisorSq = sdSegSq(b, vec2(0.258, 0.544), vec2(0.324, 0.528));
    dFineSq = min(dNoseSq, min(dLegSq, dVisorSq));
}

vec3 renderStarfield(vec2 skyUv, float yNorm, float time) {
    vec2 grid = skyUv * vec2(38.0, 24.0);
    vec2 cell = floor(grid);
    vec2 f = fract(grid) - 0.5;
    vec4 rnd = hash14(cell);
    if (rnd.x < 0.72) return vec3(0.0, 0.0, 0.0);

    vec2 offset = (rnd.yz - 0.5) * 0.68;
    vec2 dVec = f - offset;
    float d = length(dVec);
    float size = mix(0.020, 0.052, fract(rnd.w * 7.3));
    float core = ss(size, 0.0, d);

    float flare = 0.0;
    if (rnd.x > 0.93) {
        float crossH = exp(-abs(dVec.x) * 30.0) * exp(-abs(dVec.y) * 95.0);
        float crossV = exp(-abs(dVec.y) * 30.0) * exp(-abs(dVec.x) * 95.0);
        flare = (crossH + crossV) * 0.65;
    }

    float twinkle = 0.58 + 0.42 * sin(time * (1.1 + rnd.w * 2.2) + rnd.y * 6.2831853);
    float skyFade = ss(0.18, 0.42, yNorm) * ss(0.99, 0.75, yNorm);
    vec3 tint = rnd.w < 0.35
        ? vec3(0.65, 0.94, 1.0)
        : vec3(0.96, 0.97, 1.0);
    return tint * (core + flare) * twinkle * skyFade * 1.15;
}

float mtnRidgeHeight(float idx) {
    float macro = pow(0.5 + 0.5 * cos(idx * 0.76), 1.6);
    float mid = hash11(idx * 11.73 + 3.1);
    float jagged = hash11(idx * 29.41 + 7.9);
    return mix(0.055, 0.225, clamp(macro * 0.62 + mid * 0.26 + jagged * 0.12, 0.0, 1.0));
}

vec2 mtnMeshVertex(vec2 ij) {
    vec4 rnd = hash14(ij * 1.37 + vec2(5.1, 13.7));
    float jx = (rnd.x - 0.5) * 0.44;
    float jy = (ij.y <= 0.5) ? 0.0 : (rnd.y - 0.5) * 0.48;
    return ij + vec2(jx, jy);
}

vec3 renderWireframeMountains(vec2 p, float pxScale, vec3 bgCol, float gridTime) {
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

    vec2 vL = vec2(xL, hL);
    vec2 vP = vec2(xP, hP);
    vec2 vR = vec2(xR, hR);
    vec2 pt = vec2(xScroll, p.y);

    float ridgeY = pt.x < vP.x
        ? mix(vL.y, vP.y, clamp((pt.x - vL.x) / max(1e-5, vP.x - vL.x), 0.0, 1.0))
        : mix(vP.y, vR.y, clamp((pt.x - vP.x) / max(1e-5, vR.x - vP.x), 0.0, 1.0));

    float inside = (1.0 - ss(ridgeY - pxScale * 1.1, ridgeY + pxScale * 1.1, pt.y)) * step(0.0, p.y);
    float dRidge = min(sdSegment(pt, vL, vP), sdSegment(pt, vP, vR));
    float ridgeWire = 1.0 - ss(pxScale * 0.85, pxScale * 2.2, dRidge);

    vec3 res = bgCol;
    if (inside > 0.001) {
        vec2 meshScale = vec2(25.0, 27.0);
        vec2 mp = pt * meshScale;
        vec2 mCell = floor(mp);

        vec2 p00 = mtnMeshVertex(mCell);
        vec2 p10 = mtnMeshVertex(mCell + vec2(1.0, 0.0));
        vec2 p01 = mtnMeshVertex(mCell + vec2(0.0, 1.0));
        vec2 p11 = mtnMeshVertex(mCell + vec2(1.0, 1.0));

        float dEdgesSq = min(
            min(sdSegSq(mp, p00, p10), sdSegSq(mp, p01, p11)),
            min(sdSegSq(mp, p00, p01), sdSegSq(mp, p10, p11))
        );
        float diagFlip = step(0.5, hash11(dot(mCell, vec2(17.1, 31.3))));
        vec2 dA = diagFlip > 0.5 ? p00 : p10;
        vec2 dB = diagFlip > 0.5 ? p11 : p01;
        float dDiagSq = sdSegSq(mp, dA, dB);
        float dMesh = sqrt(min(dEdgesSq, dDiagSq)) / 26.0;

        vec2 diagDir = dB - dA;
        vec2 relP = mp - dA;
        float sideTri = step(0.0, relP.x * diagDir.y - relP.y * diagDir.x);
        float facetRnd = hash11(dot(mCell, vec2(23.7, 41.9)) + sideTri * 17.3);

        vec3 mtnDark = mix(vec3(0.020, 0.042, 0.115), vec3(0.042, 0.095, 0.185), facetRnd * 0.75 + sideTri * 0.25);
        mtnDark = mix(mtnDark, vec3(0.24, 0.11, 0.34), exp(-max(0.0, p.y) * 18.0) * 0.65);

        float meshWire = 1.0 - ss(pxScale * 0.65, pxScale * 1.85, dMesh);
        float meshHalo = exp(-dMesh / max(1e-4, pxScale * 4.5)) * 0.22;
        vec3 iceCyan = vec3(0.45, 0.96, 1.0);

        vec3 mtnCol = mtnDark + iceCyan * (max(meshWire * 0.86, ridgeWire) + meshHalo);
        res = mix(res, mtnCol, inside);
    }

    res += vec3(0.45, 0.96, 1.0) * ridgeWire * 0.42 * (1.0 - inside);
    return res;
}

float sdSynthwaveRider(vec2 b, out float dHelmetOut) {
    vec2 pFoot = vec2(-0.275, 0.212);
    vec2 pKnee = vec2(-0.020, 0.276);
    vec2 pHip = vec2(-0.185, 0.365);
    vec2 pShoulder = vec2(0.148, 0.495);
    vec2 pHand = vec2(0.365, 0.305);

    float dThigh = sdTaperedCapsule(b, pHip, pKnee, 0.050, 0.036);
    float dShin = sdTaperedCapsule(b, pKnee, pFoot, 0.036, 0.022);
    float dLeg = min(dThigh, dShin);

    float dBack = sdTaperedCapsule(b, pHip, pShoulder, 0.054, 0.064);
    float dChest = sdTaperedCapsule(b, vec2(-0.04, 0.375), vec2(0.148, 0.435), 0.052, 0.048);
    float dTorso = smin(dBack, dChest, 0.014);

    float dArm = sdTaperedCapsule(b, pShoulder, pHand, 0.042, 0.024);
    dHelmetOut = sdTaperedCapsule(b, vec2(0.220, 0.550), vec2(0.286, 0.526), 0.054, 0.042);

    return smin(dTorso, smin(dArm, smin(dHelmetOut, dLeg, 0.010), 0.010), 0.012);
}

void evalLightCycleAndRider(
    vec2 p,
    vec2 groundAnchor,
    float bikeScale,
    float pxScale,
    float time,
    float gridTime,
    float isReflection,
    inout vec3 col)
{
    float invS = 1.0 / bikeScale;
    vec2 b = (p - groundAnchor) * invS;

    if (b.x < -1.15 || b.x > 1.45 || b.y < -0.18 || b.y > 0.90) {
        return;
    }

    float localPx = pxScale * invS;
    float reflAtten = isReflection > 0.5 ? 0.36 * exp(-max(0.0, b.y) * 2.4) * ss(0.84, 0.12, b.y) : 1.0;

    vec2 wRear = vec2(-0.56, 0.265);
    vec2 wFront = vec2(0.56, 0.260);
    float rRearOuter = 0.265;
    float rRearHub = 0.138;
    float rRearLight = 0.214;
    float rFrontOuter = 0.260;
    float rFrontHub = 0.134;
    float rFrontLight = 0.210;

    float dCenterRear = length(b - wRear);
    float dCenterFront = length(b - wFront);

    float dRearBackShell = sdTaperedCapsule(b, vec2(-0.825, 0.220), vec2(-0.740, 0.440), 0.044, 0.056);
    float dRearArch = sdTaperedCapsule(b, vec2(-0.740, 0.440), vec2(-0.530, 0.515), 0.056, 0.056);
    float dTailFin = sdTaperedCapsule(b, vec2(-0.540, 0.528), vec2(-0.785, 0.538), 0.018, 0.008);
    float dCanopyWing = sdTaperedCapsule(b, vec2(-0.530, 0.505), vec2(-0.295, 0.508), 0.052, 0.026);
    float dSeatBack = sdTaperedCapsule(b, vec2(-0.340, 0.425), vec2(-0.275, 0.275), 0.044, 0.054);

    float dBelly = sdTaperedCapsule(b, vec2(-0.360, 0.160), vec2(0.320, 0.140), 0.095, 0.088);
    float dWaistTank = sdTaperedCapsule(b, vec2(-0.140, 0.305), vec2(0.295, 0.395), 0.058, 0.074);
    float dFrontArch = sdTaperedCapsule(b, vec2(0.285, 0.395), vec2(0.560, 0.525), 0.074, 0.052);
    float dFrontNoseTop = sdTaperedCapsule(b, vec2(0.560, 0.525), vec2(0.790, 0.405), 0.052, 0.048);
    float dFrontNoseMid = sdTaperedCapsule(b, vec2(0.790, 0.405), vec2(0.848, 0.210), 0.048, 0.038);
    float dFrontNoseTip = sdTaperedCapsule(b, vec2(0.848, 0.210), vec2(0.795, 0.110), 0.036, 0.022);
    float dFrontSkirt = sdTaperedCapsule(b, vec2(0.300, 0.355), vec2(0.315, 0.100), 0.054, 0.036);

    float dRearBody = smin(dRearBackShell, dRearArch, 0.025);
    dRearBody = min(dRearBody, dTailFin);
    dRearBody = smin(dRearBody, smin(dCanopyWing, dSeatBack, 0.028), 0.025);

    float dMidBody = smin(dBelly, dWaistTank, 0.045);
    float dFrontBody = smin(dFrontArch, smin(dFrontNoseTop, smin(dFrontNoseMid, dFrontNoseTip, 0.025), 0.025), 0.030);
    dFrontBody = smin(dFrontBody, dFrontSkirt, 0.035);

    float dChassis = smin(dRearBody, smin(dMidBody, dFrontBody, 0.040), 0.035);
    dChassis = max(dChassis, -(dCenterRear - 0.244));
    dChassis = max(dChassis, -(dCenterFront - 0.240));

    float dBarMount = sdRoundedBox(b - vec2(0.355, 0.275), vec2(0.034, 0.028), 0.008);
    float dRearPeg = sdTaperedCapsule(b, vec2(-0.345, 0.210), vec2(-0.275, 0.205), 0.016, 0.012);
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

    float barrelMask = 1.0 - ss(-localPx, localPx, dBarrels);
    if (barrelMask > 0.001 && isReflection < 0.5) {
        float minB = min(dCenterRear - rRearHub, dCenterFront - rFrontHub);
        float depthRing = clamp(minB / 0.052, 0.0, 1.0);
        float innerBevel = sin(depthRing * 3.14159);
        float cyc = b.y > 0.26 ? 0.85 : 1.15;
        vec3 steelCol = vec3(0.042, 0.056, 0.095) * cyc + vec3(0.08, 0.22, 0.34) * innerBevel;
        steelCol += vec3(0.04, 0.58, 0.85) * smoothstep(0.45, 1.0, depthRing) * 0.42;
        col = mix(col, steelCol, barrelMask * 0.92);
    }

    float tireMask = 1.0 - ss(-localPx, localPx, dTires);
    if (tireMask > 0.001) {
        vec3 tireCol = vec3(0.012, 0.012, 0.028);
        float rimGlow = exp(-abs(min(dCenterRear - rRearOuter, dCenterFront - rFrontOuter)) * 45.0) * 0.12;
        tireCol += vec3(0.04, 0.45, 0.72) * rimGlow;
        col = isReflection > 0.5
            ? mix(col, col * 0.65 + tireCol * 0.35, tireMask * reflAtten * 0.75)
            : mix(col, tireCol, tireMask);
    }

    float bodyMask = 1.0 - ss(-localPx, localPx, dBodyFull);
    if (bodyMask > 0.001) {
        vec3 bodyCol = vec3(0.012, 0.018, 0.042);

        float crownRim = ss(0.022, 0.0, abs(dBodyFull)) * ss(0.28, 0.58, b.y);
        vec2 vDome = (b - vec2(0.56, 0.48)) * vec2(3.5, 9.0);
        vec2 vTail = (b - vec2(-0.56, 0.49)) * vec2(4.0, 12.0);
        vec2 vHelm = (b - vec2(0.24, 0.56)) * vec2(7.0, 14.0);
        float domeShine = exp(-dot(vDome, vDome) * 0.9) * 0.28;
        float tailShine = exp(-dot(vTail, vTail) * 0.9) * 0.24;
        float helmShine = exp(-dot(vHelm, vHelm) * 0.9) * 0.22;
        bodyCol += vec3(0.18, 0.28, 0.42) * (crownRim * 0.68 + domeShine + tailShine + helmShine);

        vec2 vSkirt = (b - vec2(-0.27, 0.25)) * vec2(7.0, 8.5);
        float skirtGlow = exp(-dot(vSkirt, vSkirt) * 1.1);
        float skirtRib = 0.70 + 0.30 * sin(b.x * 38.0 + b.y * 24.0);
        bodyCol += vec3(0.04, 0.68, 0.96) * skirtGlow * skirtRib * 0.34;
        bodyCol += vec3(0.42, 0.08, 0.34) * ss(-0.62, -0.82, b.x) * 0.20;

        col = mix(col, bodyCol, bodyMask);
    }

    vec2 turbCenter = vec2(0.015, 0.185);
    vec2 turbRel = b - turbCenter;
    float cAng = cos(-0.08), sAng = sin(-0.08);
    vec2 tb = vec2(turbRel.x * cAng - turbRel.y * sAng, turbRel.x * sAng + turbRel.y * cAng);
    float dTurb = sdRoundedBox(tb, vec2(0.128, 0.048), 0.026);
    float turbRim = ss(localPx * 3.0, 0.0, abs(dTurb));
    float turbMask = 1.0 - ss(-localPx, localPx, dTurb);
    if (turbMask > 0.001) {
        vec2 tp = tb / vec2(0.128, 0.048);
        vec3 bayBg = vec3(0.006, 0.016, 0.038);
        float conduitBars = step(0.15, sin(tp.y * 10.5)) * step(-0.78, tp.x) * step(tp.x, 0.78);
        float circuitGap = step(0.12, abs(fract(tp.x * 3.5 + tp.y * 0.8) - 0.5));
        float corePulse = exp(-dot(tp * vec2(0.9, 1.05), tp * vec2(0.9, 1.05)) * 1.15);
        float fins = conduitBars * circuitGap * (0.75 + 0.25 * sin(tp.x * 12.0 - gridTime * 7.5));
        vec3 finGlow = vec3(0.06, 0.82, 1.0) * fins * 0.92 + vec3(0.85, 0.99, 1.0) * fins * corePulse * 0.78;
        col = mix(col, bayBg, turbMask);
        col += finGlow * turbMask;
    }
    col += vec3(0.10, 0.45, 0.65) * turbRim * 0.45;
    col += vec3(0.0, 0.82, 1.0) * exp(-max(0.0, dTurb) * 18.0) * 0.44;

    vec2 qRear = b - wRear;
    vec2 qFront = b - wFront;
    float aRear = atan(qRear.y, qRear.x);
    float aFront = atan(qFront.y, qFront.x);
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

    vec3 neonCyan = vec3(0.06, 0.92, 1.0);
    vec3 coreWhite = vec3(0.88, 0.99, 1.0);

    col += neonCyan * (rearGlow + frontGlow) * 0.95;
    col += coreWhite * max(rearCore, frontCore) * 0.96;

    float mainCore = 1.0 - ss(0.0085, 0.0085 + localPx * 2.0, dMainRibbons);
    float mainGlow = exp(-dMainRibbons * 33.0);
    float fineCore = 1.0 - ss(0.0038, 0.0038 + localPx * 1.4, dFineRibbons);
    float fineGlow = exp(-dFineRibbons * 50.0);

    col += neonCyan * (mainGlow * 0.88 + fineGlow * 0.62);
    col += coreWhite * max(mainCore * 0.92, fineCore * 0.82);

    vec2 vHead = (b - vec2(0.86, 0.28)) * vec2(0.65, 1.8);
    float headBound = ss(1.42, 0.80, b.x) * ss(0.56, 0.25, abs(b.y - 0.26));
    col += vec3(0.12, 0.94, 1.0) * exp(-dot(vHead, vHead) * 6.8) * 0.36 * headBound;
}

void evalCycleReflection(
    vec2 p,
    vec2 groundAnchor,
    float bikeScale,
    float pxScale,
    float gridTime,
    inout vec3 col)
{
    float invS = 1.0 / bikeScale;
    vec2 b = (p - groundAnchor) * invS;
    if (b.x < -1.00 || b.x > 1.00 || b.y < 0.0 || b.y > 0.72) {
        return;
    }

    float localPx = pxScale * invS;
    float reflAtten = 0.36 * exp(-b.y * 2.4) * ss(0.72, 0.08, b.y);

    float dRearRing = abs(length(b - vec2(-0.56, 0.265)) - 0.214);
    float dFrontRing = abs(length(b - vec2(0.56, 0.260)) - 0.210);
    float rearGlow = exp(-dRearRing * 26.0);
    float frontGlow = exp(-dFrontRing * 26.0);
    float ringCore = max(1.0 - ss(0.015, 0.015 + localPx * 2.0, dRearRing), 1.0 - ss(0.015, 0.015 + localPx * 2.0, dFrontRing));

    float dMainSq, dFineSq;
    evalCycleRibbonSq(b, dMainSq, dFineSq);
    float dMain = sqrt(dMainSq);
    float dFine = sqrt(dFineSq);

    vec2 tb = (b - vec2(0.015, 0.185)) / vec2(0.135, 0.055);
    float turbGlow = exp(-dot(tb, tb) * 1.4) * (0.65 + 0.35 * sin(tb.y * 9.0));

    vec3 neonCyan = vec3(0.06, 0.92, 1.0);
    vec3 coreWhite = vec3(0.88, 0.99, 1.0);
    col += neonCyan * ((rearGlow + frontGlow) * 0.90 + exp(-dMain * 33.0) * 0.85 + exp(-dFine * 50.0) * 0.55 + turbGlow * 0.72) * reflAtten;
    col += coreWhite * max(ringCore * 0.90, 1.0 - ss(0.0085, 0.0085 + localPx * 2.0, dMain)) * reflAtten;
}

void main() {
    vec2 glUv = vUv;

    float aspect = uResolution.x / max(1.0, uResolution.y);
    float horY = 0.44;
    float viewH = max(1.0, 0.95 / max(0.15, aspect));
    float pxScale = viewH / max(1.0, uResolution.y);
    vec2 p = vec2((glUv.x - 0.5) * aspect * viewH, (glUv.y - horY) * viewH);
    vec2 sunCenter = vec2(0.0, 0.255);

    float yFloor = max(0.0022, -p.y);
    float depth = 0.24 / yFloor;
    float worldX = p.x * depth * 1.15 + uGridTime * 1.65;
    float worldZ = depth * 1.05;
    vec2 gCoord = vec2(worldX, worldZ);
    vec2 gDeriv = max(vec2(0.0008, 0.0008), fwidth(gCoord));

    // 1. Cosmic Night -> Royal Cobalt Blue -> Horizon Magenta Sky
    vec3 skyTop = vec3(0.015, 0.018, 0.068);
    vec3 skyMid = vec3(0.045, 0.135, 0.460);
    vec3 skyHor = vec3(0.560, 0.095, 0.540);
    float tSky = clamp((glUv.y - horY) / (1.0 - horY), 0.0, 1.0);
    vec3 col = mix(skyHor, skyMid, ss(0.02, 0.44, tSky));
    col = mix(col, skyTop, ss(0.55, 0.96, tSky));

    // 2. Crisp Golden Sliced Outrun Sun
    vec2 pSun = p - sunCenter;
    float sunR = 0.165;
    float sunDist = length(pSun);
    float sunBreath = 1.0 + 0.03 * sin(uTime * 1.25);

    float sunHalo = exp(-max(0.0, sunDist - sunR) * 11.0) * 0.45 * sunBreath;
    col += vec3(1.0, 0.48, 0.18) * sunHalo;

    if (p.y >= -0.002) {
        vec2 starCoord = vec2((glUv.x - 0.5) * aspect * viewH, glUv.y * viewH);
        col += renderStarfield(starCoord, tSky, uTime) * ss(sunR * 0.95, sunR * 1.45, sunDist);

        float discMask = 1.0 - ss(sunR - pxScale * 1.4, sunR + pxScale * 1.4, sunDist);
        if (discMask > 0.001) {
            float hNorm = clamp((pSun.y + sunR) / (2.0 * sunR), 0.0, 1.0);
            vec3 sunTopCol = vec3(1.0, 0.94, 0.24);
            vec3 sunBotCol = vec3(1.0, 0.68, 0.08);
            vec3 discCol = mix(sunBotCol, sunTopCol, ss(0.05, 0.90, hNorm));

            float bandH = clamp((hNorm - 0.04) / 0.58, 0.0, 1.0);
            float slicePhase = fract(bandH * 6.2 + uTime * 0.22);
            float sliceThickness = mix(0.42, 0.06, bandH);
            float sliceGap = ss(sliceThickness + 0.035, sliceThickness - 0.015, slicePhase)
                * step(bandH, 0.96)
                * ss(0.96, 0.80, bandH);
            col = mix(col, discCol, discMask * (1.0 - sliceGap));
        }

        // 3. Slow-Scrolling Triangulated Cyan Wireframe Mountains
        col = renderWireframeMountains(p, pxScale, col, uGridTime);
    } else {
        // 4. Reflective Hot-Pink Perspective Neon Grid Floor
        float yFloorRaw = -p.y;
        vec2 gFrac = abs(fract(gCoord - 0.5) - 0.5) / gDeriv;
        float dLine = min(gFrac.x, gFrac.y);
        float lineCore = 1.0 - min(dLine, 1.0);
        float lineHalo = exp(-dLine * 0.62) * 0.30;
        float distFade = ss(0.003, 0.028, yFloorRaw);

        vec3 floorHor = vec3(0.460, 0.085, 0.420);
        vec3 floorNear = vec3(0.095, 0.012, 0.125);
        col = mix(floorHor, floorNear, ss(0.0, 0.36 * viewH, yFloorRaw));

        vec3 pinkLine = vec3(1.0, 0.38, 0.95);
        vec3 pinkHaloCol = vec3(0.95, 0.16, 0.80);
        col += (pinkLine * lineCore * 0.95 + pinkHaloCol * lineHalo) * distFade;
    }

    // 5. Pastel Pink-White Horizon Glow Band
    float horMist = exp(-abs(p.y) * 95.0 / viewH);
    float horCore = exp(-abs(p.y) * 480.0 / viewH);
    col += vec3(1.0, 0.72, 0.95) * horMist * 0.62 + vec3(1.0, 0.92, 1.0) * horCore * 0.78;

    // 6. Unified Light Cycle + Merged Rider
    float bounce = sin(uGridTime * 11.0) * 0.0018 + sin(uGridTime * 23.0) * 0.0007;
    vec2 bikeGround = vec2(0.02 + sin(uTime * 0.55) * 0.026, -0.215 + bounce);
    float bikeScale = 0.34;

    vec2 wakeD = p - (bikeGround + vec2(-0.19, 0.012));
    if (p.x < bikeGround.x - 0.16 && abs(wakeD.y) < 0.14) {
        float backLen = clamp((bikeGround.x - 0.16 - p.x) / 0.85, 0.0, 1.0);
        float wakeFade = (1.0 - backLen) * (1.0 - backLen);
        float ribbonH = 0.024 * (1.0 - 0.45 * backLen);
        float ribbonCore = 1.0 - ss(ribbonH * 0.22, ribbonH, abs(wakeD.y));
        float ribbonGlow = exp(-abs(wakeD.y) * 52.0);
        float wavePulse = 0.82 + 0.18 * sin(p.x * 24.0 + uGridTime * 16.0);
        col += vec3(0.06, 0.92, 1.0) * (ribbonGlow * 0.48 + ribbonCore * 0.52) * wakeFade * wavePulse;
    }

    vec2 shD = (p - bikeGround) / vec2(0.28, 0.022);
    col *= 1.0 - exp(-dot(shD, shD) * 2.2) * 0.72;
    col += vec3(0.0, 0.88, 1.0) * exp(-length((p - bikeGround) * vec2(4.2, 34.0)) * 1.1) * 0.30;

    if (p.y < bikeGround.y) {
        float rippleOff = sin(p.y * 190.0 + uGridTime * 8.0) * 0.0022;
        vec2 pRefl = vec2(p.x + rippleOff, 2.0 * bikeGround.y - p.y);
        evalCycleReflection(pRefl, bikeGround, bikeScale, pxScale, uGridTime, col);
    }

    evalLightCycleAndRider(p, bikeGround, bikeScale, pxScale, uTime, uGridTime, 0.0, col);

    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
