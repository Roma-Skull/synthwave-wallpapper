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

float sdBox(float2 pt, float2 halfSize) {
    float2 d = abs(pt) - halfSize;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

float sdRoundedBox(float2 pt, float2 halfSize, float r) {
    return sdBox(pt, halfSize - float2(r, r)) - r;
}

float sdTrapezoidY(float2 pt, float wBot, float wTop, float h) {
    float2 k1 = float2(wTop, h);
    float2 k2 = float2(wTop - wBot, 2.0 * h);
    pt.x = abs(pt.x);
    float2 ca = float2(pt.x - min(pt.x, (pt.y < 0.0) ? wBot : wTop), abs(pt.y) - h);
    float2 cb = pt - k1 + k2 * clamp(dot(k1 - pt, k2) / max(1e-6, dot(k2, k2)), 0.0, 1.0);
    float s = (cb.x < 0.0 && ca.y < 0.0) ? -1.0 : 1.0;
    return s * sqrt(min(dot(ca, ca), dot(cb, cb)));
}

float3 renderStarfield(float2 skyUv, float yAboveHor, float time) {
    float2 grid = skyUv * float2(40.0, 22.0);
    float2 cell = floor(grid);
    float2 f = frac(grid) - 0.5;
    float4 rnd = hash14(cell);
    if (rnd.x < 0.74) return float3(0.0, 0.0, 0.0);

    float2 offset = (rnd.yz - 0.5) * 0.66;
    float2 dVec = f - offset;
    float d = length(dVec);
    float size = lerp(0.020, 0.052, frac(rnd.w * 7.3));
    float core = ss(size, 0.0, d);

    float flare = 0.0;
    if (rnd.x > 0.93) {
        float crossH = exp(-abs(dVec.x) * 30.0) * exp(-abs(dVec.y) * 95.0);
        float crossV = exp(-abs(dVec.y) * 30.0) * exp(-abs(dVec.x) * 95.0);
        flare = (crossH + crossV) * 0.7;
    }

    float twinkle = 0.55 + 0.45 * sin(time * (1.1 + rnd.w * 2.4) + rnd.y * 6.2831853);
    float skyFade = ss(0.10, 0.30, yAboveHor) * ss(0.57, 0.42, yAboveHor);
    float3 tint = rnd.w < 0.35
        ? float3(0.55, 0.95, 1.0)
        : (rnd.w < 0.65 ? float3(1.0, 0.62, 0.90) : float3(0.95, 0.94, 1.0));
    return tint * (core + flare) * twinkle * skyFade;
}

float3 renderMiamiSkyline(float2 p, float pxScale, float3 bgCol, float time) {
    if (p.y < 0.0 || p.y > 0.28 || abs(p.x) < 0.13) return bgCol;

    float side = p.x >= 0.0 ? 1.0 : -1.0;
    float xAbs = abs(p.x);
    float3 res = bgCol;

    [unroll]
    for (int layer = 0; layer < 2; layer++) {
        float cellW = layer == 0 ? 0.068 : 0.054;
        float xShift = layer == 0 ? 0.0 : 0.029;
        float xCoord = xAbs + xShift;
        float idx = floor(xCoord / cellW);
        float localX = frac(xCoord / cellW) - 0.5;

        float hRnd = hash11(idx * 13.37 + side * 29.1 + float(layer) * 71.3);
        float bGap = step(0.08, 0.5 - abs(localX));
        float corridor = ss(0.13, 0.32, xAbs);
        float maxH = layer == 0 ? 0.165 : 0.115;
        float minH = layer == 0 ? 0.042 : 0.028;
        float bHeight = lerp(minH, maxH, hRnd * hRnd) * corridor;

        float tierW = 0.24;
        float isCrown = step(abs(localX), tierW) * step(0.55, hRnd);
        float totalH = bHeight + isCrown * 0.022 * corridor;

        float hasSpire = step(0.76, hash11(idx * 5.71 + side * 41.3 + float(layer) * 19.0));
        float spireH = totalH + hasSpire * 0.028 * corridor;
        float dSpire = abs(localX * cellW);
        float spireMask = (1.0 - ss(pxScale * 0.8, pxScale * 2.0, dSpire))
            * step(totalH, p.y) * (1.0 - ss(spireH - pxScale, spireH + pxScale, p.y));

        float inBldg = bGap * (1.0 - ss(totalH - pxScale * 1.2, totalH + pxScale * 1.2, p.y));
        if (inBldg > 0.001 || spireMask > 0.001) {
            float3 bldgCol = layer == 0
                ? float3(0.036, 0.015, 0.092)
                : float3(0.020, 0.009, 0.058);

            float sunSide = saturate((0.5 - localX * sign(p.x)) * 0.5) * exp(-xAbs * 2.4);
            bldgCol += float3(0.14, 0.03, 0.11) * sunSide;

            float2 wGrid = float2(localX * cellW * 155.0, p.y * 160.0);
            float2 wCell = floor(wGrid);
            float2 wF = abs(frac(wGrid) - 0.5);
            float wRnd = hash11(dot(wCell, float2(17.3, 41.7)) + idx * 9.1 + float(layer) * 33.0);
            float winBox = step(wF.x, 0.24) * step(wF.y, 0.26);
            float winOn = step(0.68, wRnd) * step(0.014, p.y) * step(p.y, totalH - 0.012);
            float3 winTint = wRnd > 0.90
                ? float3(0.0, 0.94, 1.0)
                : (wRnd > 0.80 ? float3(1.0, 0.25, 0.75) : float3(1.0, 0.78, 0.42));
            bldgCol += winTint * winBox * winOn * (layer == 0 ? 0.55 : 0.85);

            float roofRim = ss(pxScale * 3.0, 0.0, abs(p.y - totalH)) * bGap;
            float stripeOn = step(0.60, hash11(idx * 19.3 + float(layer) * 11.0));
            float vertNeon = ss(pxScale * 2.2, 0.0, abs(localX * cellW)) * stripeOn * step(0.015, p.y);
            float3 neonCol = hash11(idx * 3.1 + float(layer) * 7.7) > 0.5
                ? float3(0.0, 0.94, 1.0)
                : float3(1.0, 0.18, 0.68);
            float pulse = 0.88 + 0.12 * sin(time * 2.0 + idx);
            bldgCol += neonCol * (roofRim * 0.75 + vertNeon * 0.42) * pulse;

            res = lerp(res, bldgCol, inBldg);
            res += float3(1.0, 0.30, 0.78) * spireMask * 0.65;
        }
    }

    return res;
}

void evalPalmAndLamp(
    float2 p,
    float anchorX,
    float anchorY,
    float invZ,
    float side,
    float seed,
    float pxScale,
    float time,
    inout float3 col)
{
    float treeH = 0.48 * invZ;
    float crownR = 0.22 * invZ;
    float sway = sin(time * 1.4 + seed * 3.1) * 0.012 * invZ;

    if (p.x < anchorX - crownR * 1.45 || p.x > anchorX + crownR * 1.45 ||
        p.y < anchorY - 0.02 * invZ || p.y > anchorY + treeH + crownR * 1.15)
    {
        return;
    }

    float lean = side * 0.055 * invZ + sway;
    float2 pBase = float2(anchorX, anchorY);
    float2 pMid = float2(anchorX + lean * 0.35, anchorY + treeH * 0.52);
    float2 pCrown = float2(anchorX + lean, anchorY + treeH);

    float dTrunk = min(sdSegment(p, pBase, pMid), sdSegment(p, pMid, pCrown));
    float tNorm = saturate((p.y - anchorY) / max(1e-4, treeH));
    float trunkHalfW = lerp(0.0105, 0.0042, tNorm) * invZ;
    float trunkMask = 1.0 - ss(trunkHalfW - pxScale * 0.8, trunkHalfW + pxScale * 0.8, dTrunk);

    float2 dp = p - pCrown;
    float r = length(dp);
    float frondMask = 0.0;

    if (r < crownR * 1.35) {
        [loop]
        for (int f = 0; f < 7; f++) {
            float ang = -2.25 + float(f) * 0.75 + (hash11(seed * 7.1 + float(f)) - 0.5) * 0.14;
            float2 dir = float2(sin(ang), cos(ang));
            float fLen = crownR * lerp(0.82, 1.15, hash11(seed * 3.3 + float(f)));

            float proj = clamp(dot(dp, dir) / max(1e-4, fLen), 0.0, 1.0);
            float2 frondPt = dir * (fLen * proj) + float2(0.0, -0.58 * proj * proj * crownR + 0.26 * proj * crownR);
            float dArc = length(dp - frondPt);

            float leaflet = 0.0075 * invZ * abs(sin(proj * 74.0)) * ss(0.06, 0.22, proj) * (1.0 - proj * 0.92);
            float bladeW = (0.0135 * invZ * (1.0 - proj * 0.92)) + leaflet;
            float fHit = 1.0 - ss(bladeW - pxScale * 0.8, bladeW + pxScale * 0.8, dArc);
            frondMask = max(frondMask, fHit);
        }
        float hub = 1.0 - ss(0.014 * invZ, 0.014 * invZ + pxScale, r);
        frondMask = max(frondMask, hub);
    }

    float treeMask = max(trunkMask, frondMask);
    if (treeMask > 0.001) {
        float distFog = ss(0.04, 0.32, invZ);
        float3 silDark = lerp(float3(0.085, 0.022, 0.14), float3(0.016, 0.006, 0.042), distFog);
        float rim = ss(0.35, 0.95, 1.0 - abs(p.x - anchorX) / max(1e-4, crownR));
        float3 rimCol = float3(1.0, 0.22, 0.58) * exp(-abs(anchorX) * 2.0) * 0.22 * rim;
        col = lerp(col, silDark + rimCol, treeMask);
    }

    float lampX = anchorX - side * 0.036 * invZ;
    float lampH = 0.23 * invZ;
    float2 lBot = float2(lampX, anchorY);
    float2 lTop = float2(lampX, anchorY + lampH);
    float2 lArm = float2(lampX - side * 0.028 * invZ, anchorY + lampH + 0.008 * invZ);
    float dPole = min(sdSegment(p, lBot, lTop), sdSegment(p, lTop, lArm));
    float poleMask = 1.0 - ss(0.0024 * invZ, 0.0024 * invZ + pxScale, dPole);
    float lampBulb = length(p - lArm);
    float bulbCore = 1.0 - ss(0.006 * invZ, 0.006 * invZ + pxScale, lampBulb);
    float bulbGlow = exp(-lampBulb / max(1e-4, 0.030 * invZ)) * ss(0.04, 0.35, invZ);
    float3 lampCol = (frac(seed * 0.5) < 0.5) ? float3(0.0, 0.94, 1.0) : float3(1.0, 0.22, 0.74);
    col = lerp(col, float3(0.035, 0.015, 0.08), poleMask);
    col += lampCol * (bulbGlow * 0.85 + bulbCore * 0.95);
}

// Evaluates an oncoming car or 80s semi-truck driving toward the camera in the left lanes
void evalOncomingVehicle(
    float2 p,
    float slotIdx,
    float zVeh,
    float pxScale,
    float time,
    inout float3 col)
{
    float4 rnd = hash14(float2(slotIdx * 1.37, 29.7));

    // Variable traffic intensity over time/distance (clusters and empty stretches)
    float clusterWave = 0.42 + 0.28 * sin(slotIdx * 0.47 + 0.8) + 0.14 * cos(slotIdx * 1.13);
    if (rnd.x > clusterWave) return;

    // Select oncoming lane (left outer or left inner lane) + slight natural offset
    float isTruck = step(0.66, rnd.z); // ~34% semi-trucks, ~66% cars
    float laneChoice = (isTruck > 0.5) ? step(0.72, rnd.y) : step(0.48, rnd.y);
    float laneWorldX = laneChoice < 0.5 ? -0.315 : -0.110;
    laneWorldX += (rnd.w - 0.5) * 0.022;

    float invZ = 1.0 / max(0.035, zVeh);
    float bounce = sin(time * 8.5 + slotIdx * 4.3) * 0.0012 * invZ;
    float2 anchor = float2(laneWorldX * invZ, -0.26 * invZ + bounce);

    float halfW = (isTruck > 0.5 ? 0.092 : 0.076) * invZ;
    float maxH = (isTruck > 0.5 ? 0.215 : 0.095) * invZ;

    // Quick bounding-box reject (wide enough that shadow & headlight glow fade completely to 0 inside AABB)
    if (p.x < anchor.x - halfW * 3.0 || p.x > anchor.x + halfW * 3.0 ||
        p.y < anchor.y - 0.14 * invZ || p.y > anchor.y + maxH * 1.2)
    {
        return;
    }

    // Transform pixel into normalized vehicle-local coordinates (1.0 unit = 1 world unit at z=1)
    float2 q = (p - anchor) / invZ;
    float localPx = pxScale / invZ;
    float distFade = ss(0.04, 0.22, invZ);

    // 1. Contact shadow & oncoming headlight cone wash on wet asphalt
    float shadowD = length((q - float2(0.0, 0.0)) * float2(1.0 / (isTruck > 0.5 ? 0.105 : 0.085), 6.5));
    col *= 1.0 - exp(-shadowD * shadowD * 8.0) * 0.72;

    float beamWash = exp(-length((q - float2(0.0, -0.015)) * float2(12.0, 26.0)) * 1.35);
    float3 headHue = (isTruck > 0.5 || rnd.w > 0.45) ? float3(0.85, 0.96, 1.0) : float3(1.0, 0.88, 0.65);
    col += headHue * beamWash * 0.30 * distFade;

    if (isTruck > 0.5) {
        // --- 80s Synthwave Semi-Truck (Фура) ---
        // Tall trailer box behind the cab (top kept below camera eye height so it clears portrait viewport cleanly)
        float dTrailer = sdRoundedBox(q - float2(0.0, 0.108), float2(0.088, 0.084), 0.008);
        float trailerMask = 1.0 - ss(-localPx, localPx, dTrailer);
        if (trailerMask > 0.001) {
            float3 trCol = float3(0.022, 0.010, 0.062);
            float trEdge = ss(localPx * 3.0, 0.0, abs(dTrailer));
            trCol += float3(1.0, 0.18, 0.65) * trEdge * 0.45;
            // Top trailer marker lights (left, center, right)
            float dTopMarks = min(length(q - float2(-0.072, 0.180)), min(length(q - float2(0.0, 0.180)), length(q - float2(0.072, 0.180))));
            trCol += float3(1.0, 0.55, 0.18) * exp(-dTopMarks * 180.0) * 0.9;
            col = lerp(col, trCol, trailerMask);
        }

        // Front tires
        float dTires = min(
            sdRoundedBox(q - float2(-0.074, 0.014), float2(0.017, 0.018), 0.004),
            sdRoundedBox(q - float2( 0.074, 0.014), float2(0.017, 0.018), 0.004)
        );
        col = lerp(col, float3(0.018, 0.010, 0.036), 1.0 - ss(-localPx, localPx, dTires));

        // Tall cab body
        float dCab = sdRoundedBox(q - float2(0.0, 0.086), float2(0.080, 0.068), 0.010);
        float cabMask = 1.0 - ss(-localPx, localPx, dCab);
        if (cabMask > 0.001) {
            float3 cabCol = float3(0.040, 0.018, 0.100);
            // Windshield with sunset reflection
            float dWind = sdRoundedBox(q - float2(0.0, 0.118), float2(0.064, 0.021), 0.005);
            float windMask = 1.0 - ss(-localPx, localPx, dWind);
            float3 windCol = lerp(float3(0.08, 0.02, 0.16), float3(0.95, 0.35, 0.68), saturate((q.y - 0.10) * 22.0) * 0.55);
            cabCol = lerp(cabCol, windCol, windMask);

            // Chrome grille
            float dGrille = sdRoundedBox(q - float2(0.0, 0.056), float2(0.038, 0.026), 0.004);
            float grilleMask = 1.0 - ss(-localPx, localPx, dGrille);
            float bars = 0.5 + 0.5 * sin(q.y * 520.0);
            cabCol = lerp(cabCol, float3(0.14, 0.10, 0.24) + float3(0.0, 0.45, 0.55) * bars * 0.35, grilleMask);

            // Cab cyan trim
            float cabEdge = ss(localPx * 3.0, 0.0, abs(dCab));
            cabCol += float3(0.0, 0.90, 1.0) * cabEdge * 0.42;
            col = lerp(col, cabCol, cabMask);
        }

        // Heavy front bumper
        float dBumper = sdRoundedBox(q - float2(0.0, 0.022), float2(0.085, 0.011), 0.003);
        col = lerp(col, float3(0.065, 0.035, 0.14), 1.0 - ss(-localPx, localPx, dBumper));

        // Cab roof amber clearance lights (5-light cluster)
        float roofLights = 0.0;
        [unroll]
        for (int c = -2; c <= 2; c++) {
            float dLed = length(q - float2(float(c) * 0.018, 0.149));
            roofLights += exp(-dLed * 240.0);
        }
        col += float3(1.0, 0.62, 0.15) * roofLights * 0.95 * distFade;

        // Twin bright truck headlights
        float dHL = min(
            sdRoundedBox(q - float2(-0.056, 0.044), float2(0.014, 0.008), 0.002),
            sdRoundedBox(q - float2( 0.056, 0.044), float2(0.014, 0.008), 0.002)
        );
        float hlCore = 1.0 - ss(-localPx, localPx, dHL);
        float hlGlow = exp(-max(0.0, dHL) * 75.0);
        col += headHue * hlGlow * 0.95 * distFade;
        col = lerp(col, float3(1.0, 0.98, 1.0), hlCore);
    } else {
        // --- 80s Retro Sedan / Sports Car (Легковушка) ---
        float dTires = min(
            sdRoundedBox(q - float2(-0.058, 0.012), float2(0.015, 0.014), 0.004),
            sdRoundedBox(q - float2( 0.058, 0.012), float2(0.015, 0.014), 0.004)
        );
        col = lerp(col, float3(0.018, 0.010, 0.036), 1.0 - ss(-localPx, localPx, dTires));

        // Cabin & windshield
        float dCab = sdTrapezoidY(q - float2(0.0, 0.062), 0.058, 0.040, 0.017);
        float cabMask = 1.0 - ss(-localPx, localPx, dCab);
        if (cabMask > 0.001) {
            float dWind = sdTrapezoidY(q - float2(0.0, 0.062), 0.048, 0.033, 0.013);
            float windMask = 1.0 - ss(-localPx, localPx, dWind);
            float3 cabCol = float3(0.035, 0.016, 0.088);
            float3 windCol = lerp(float3(0.06, 0.02, 0.14), float3(0.92, 0.32, 0.65), saturate((q.y - 0.05) * 35.0) * 0.5);
            cabCol = lerp(cabCol, windCol, windMask);
            cabCol += float3(1.0, 0.40, 0.78) * ss(localPx * 3.0, 0.0, abs(dCab)) * 0.45;
            col = lerp(col, cabCol, cabMask);
        }

        // Front hood / fascia
        float dBody = sdTrapezoidY(q - float2(0.0, 0.030), 0.072, 0.064, 0.018);
        float bodyMask = 1.0 - ss(-localPx, localPx, dBody);
        if (bodyMask > 0.001) {
            float3 bCol = lerp(float3(0.028, 0.012, 0.070), float3(0.085, 0.028, 0.18), saturate((q.y - 0.012) / 0.036));
            bCol += float3(0.0, 0.88, 1.0) * ss(localPx * 3.0, 0.0, abs(dBody)) * 0.40;
            // Subtle front grille
            float dGr = sdRoundedBox(q - float2(0.0, 0.026), float2(0.026, 0.006), 0.002);
            bCol = lerp(bCol, float3(0.012, 0.006, 0.030), 1.0 - ss(-localPx, localPx, dGr));
            col = lerp(col, bCol, bodyMask);
        }

        // Twin bright headlights + glow
        float dHL = min(
            sdRoundedBox(q - float2(-0.046, 0.030), float2(0.014, 0.0055), 0.002),
            sdRoundedBox(q - float2( 0.046, 0.030), float2(0.014, 0.0055), 0.002)
        );
        float hlCore = 1.0 - ss(-localPx, localPx, dHL);
        float hlGlow = exp(-max(0.0, dHL) * 85.0);
        col += headHue * hlGlow * 0.95 * distFade;
        col = lerp(col, float3(1.0, 0.98, 1.0), hlCore);
    }
}

float3 renderHeroCar(float2 p, float pxScale, float3 bgCol, float time) {
    // Hero car drives in its own right-hand inner lane (worldX ~ +0.108)
    // Projection at y = -0.252 => yFloor = 0.252, z = 0.26 / 0.252 = 1.032
    float bounce = sin(time * 7.2) * 0.0014 + sin(time * 13.7) * 0.0006;
    float laneWorldX = 0.108 + sin(time * 0.72) * 0.009;
    float roll = cos(time * 0.72) * 0.014;

    float carScale = 0.50; // Properly scaled so car fits cleanly inside a single lane (~17% screen width)
    float invS = 1.0 / carScale;
    float2 carCenter = float2(laneWorldX / 1.032, -0.252 + bounce);
    float2 q = (p - carCenter) * invS;

    float cr = cos(roll);
    float sr = sin(roll);
    q = float2(q.x * cr - q.y * sr, q.x * sr + q.y * cr);

    if (abs(q.x) > 0.44 || q.y < -0.20 || q.y > 0.20) {
        return bgCol;
    }

    float localPx = pxScale * invS;
    float3 col = bgCol;

    // 1. Ground contact shadow + cyan underglow + red taillight reflection
    float shadowDist = length((q - float2(0.0, -0.056)) * float2(1.0, 3.8));
    float contactShadow = exp(-shadowDist * shadowDist * 32.0);
    col *= 1.0 - contactShadow * 0.82;

    float underGlow = exp(-length((q - float2(0.0, -0.056)) * float2(1.0, 4.5)) * 12.0);
    col += float3(0.0, 0.88, 1.0) * underGlow * 0.34;

    float2 tailRefL = (q - float2(-0.105, -0.072)) * float2(1.4, 2.2);
    float2 tailRefR = (q - float2( 0.105, -0.072)) * float2(1.4, 2.2);
    float tailSmear = exp(-length(tailRefL) * 14.0) + exp(-length(tailRefR) * 14.0);
    col += float3(1.0, 0.12, 0.42) * tailSmear * 0.45;

    // 2. Wide rear tires
    float2 wheelPosL = q - float2(-0.148, -0.036);
    float2 wheelPosR = q - float2( 0.148, -0.036);
    float dTireL = sdRoundedBox(wheelPosL, float2(0.034, 0.030), 0.008);
    float dTireR = sdRoundedBox(wheelPosR, float2(0.034, 0.030), 0.008);
    float tireMask = 1.0 - ss(-localPx, localPx, min(dTireL, dTireR));
    if (tireMask > 0.001) {
        float3 tireCol = float3(0.025, 0.015, 0.045);
        float rimFlash = 0.5 + 0.5 * sin(time * 28.0);
        float dRim = min(abs(wheelPosL.x), abs(wheelPosR.x));
        tireCol += float3(0.18, 0.08, 0.28) * ss(0.024, 0.010, dRim) * rimFlash * 0.4;
        col = lerp(col, tireCol, tireMask);
    }

    // 3. Lower rear diffuser & twin exhausts
    float dDiffuser = sdTrapezoidY(q - float2(0.0, -0.032), 0.150, 0.165, 0.016);
    float diffMask = 1.0 - ss(-localPx, localPx, dDiffuser);
    if (diffMask > 0.001) {
        float3 diffCol = float3(0.035, 0.018, 0.068);
        float fin = ss(0.003, 0.0, abs(frac((q.x + 0.02) * 26.0) - 0.5) - 0.15) * step(abs(q.x), 0.075);
        diffCol += float3(0.05, 0.03, 0.11) * fin;
        col = lerp(col, diffCol, diffMask);
    }

    float2 exL = q - float2(-0.088, -0.036);
    float2 exR = q - float2( 0.088, -0.036);
    float dEx = min(length(exL), length(exR));
    float exPipe = 1.0 - ss(0.009 - localPx, 0.009 + localPx, dEx);
    float exFlicker = 0.78 + 0.22 * sin(time * 42.0 + sin(time * 19.0));
    float exFlame = exp(-dEx * 130.0) * exFlicker;
    col = lerp(col, float3(0.08, 0.04, 0.14), exPipe);
    col += float3(1.0, 0.35, 0.78) * exFlame * 0.9;
    col += float3(0.25, 0.92, 1.0) * exp(-dEx * 220.0) * exFlicker;

    // 4. Muscular rear haunches & rear fascia
    float dBody = sdTrapezoidY(q - float2(0.0, 0.006), 0.172, 0.156, 0.028);
    float bodyMask = 1.0 - ss(-localPx, localPx, dBody);

    // 5. Rear fastback cabin greenhouse
    float dCabin = sdTrapezoidY(q - float2(0.0, 0.064), 0.126, 0.086, 0.028);
    float cabinMask = 1.0 - ss(-localPx, localPx, dCabin);

    // 6. Wide rear wing spoiler
    float dStruts = min(
        sdBox(q - float2(-0.095, 0.034), float2(0.006, 0.011)),
        sdBox(q - float2( 0.095, 0.034), float2(0.006, 0.011))
    );
    float dWing = sdTrapezoidY(q - float2(0.0, 0.045), 0.166, 0.158, 0.0065);
    float wingMask = 1.0 - ss(-localPx, localPx, min(dStruts, dWing));

    if (cabinMask > 0.001) {
        float3 cabCol = float3(0.045, 0.022, 0.105);
        float dGlass = sdTrapezoidY(q - float2(0.0, 0.065), 0.102, 0.070, 0.020);
        float glassMask = 1.0 - ss(-localPx, localPx, dGlass);
        float3 glassCol = lerp(
            float3(0.08, 0.02, 0.18),
            float3(0.95, 0.36, 0.65),
            ss(-0.015, 0.022, q.y - 0.058) * 0.55
        );
        float louverPhase = frac((q.y - 0.045) * 145.0);
        float louver = ss(0.35, 0.55, louverPhase);
        glassCol = lerp(float3(0.025, 0.012, 0.060), glassCol, louver * 0.85);
        cabCol = lerp(cabCol, glassCol, glassMask);

        float roofRim = ss(localPx * 3.0, 0.0, abs(dCabin)) * step(0.084, q.y);
        cabCol += float3(1.0, 0.52, 0.78) * roofRim * 0.85;
        col = lerp(col, cabCol, cabinMask);
    }

    if (bodyMask > 0.001) {
        float yNorm = saturate((q.y + 0.022) / 0.056);
        float3 paint = lerp(float3(0.030, 0.014, 0.075), float3(0.11, 0.035, 0.22), yNorm);

        float chromeBand = exp(-pow((yNorm - 0.82) * 7.0, 2.0));
        paint += float3(1.0, 0.42, 0.72) * chromeBand * 0.42;
        paint += float3(0.0, 0.85, 1.0) * exp(-pow((yNorm - 0.94) * 14.0, 2.0)) * 0.35;

        float dPanel = sdRoundedBox(q - float2(0.0, 0.005), float2(0.150, 0.016), 0.004);
        float panelMask = 1.0 - ss(-localPx, localPx, dPanel);
        paint = lerp(paint, float3(0.015, 0.006, 0.036), panelMask);

        float strakeArea = step(0.068, abs(q.x)) * step(abs(q.x), 0.146) * step(abs(q.y - 0.005), 0.013);
        float strakeLines = ss(0.30, 0.50, abs(frac((q.y + 0.005) * 170.0) - 0.5));
        paint += float3(0.16, 0.05, 0.28) * strakeArea * strakeLines * 0.5;

        float fenderRim = ss(localPx * 3.0, 0.0, abs(dBody)) * step(0.0, q.y);
        paint += float3(0.0, 0.92, 1.0) * fenderRim * 0.45;

        float dPlate = sdRoundedBox(q - float2(0.0, -0.002), float2(0.032, 0.011), 0.002);
        float plateMask = 1.0 - ss(-localPx, localPx, dPlate);
        float plateEdge = ss(localPx * 2.5, 0.0, abs(dPlate));
        paint = lerp(paint, float3(0.03, 0.08, 0.16), plateMask);
        paint += float3(0.0, 0.94, 1.0) * plateEdge * 0.9;
        float plateText = (1.0 - ss(-localPx, localPx, sdBox(q - float2(0.0, -0.002), float2(0.018, 0.0025))));
        paint += float3(1.0, 0.35, 0.80) * plateText * 0.85;

        col = lerp(col, paint, bodyMask);
    }

    if (wingMask > 0.001) {
        float3 wingCol = float3(0.045, 0.020, 0.11);
        float wingTopEdge = ss(localPx * 2.5, 0.0, abs(q.y - 0.0515)) * step(abs(q.x), 0.158);
        float wingBotEdge = ss(localPx * 2.2, 0.0, abs(q.y - 0.0385)) * step(abs(q.x), 0.165);
        wingCol += float3(0.0, 0.94, 1.0) * wingTopEdge * 0.85;
        wingCol += float3(1.0, 0.20, 0.72) * wingBotEdge * 0.55;
        col = lerp(col, wingCol, wingMask);
    }

    float2 tailL = q - float2(-0.104, 0.006);
    float2 tailR = q - float2( 0.104, 0.006);
    float dTailBox = min(
        sdRoundedBox(tailL, float2(0.040, 0.0085), 0.0025),
        sdRoundedBox(tailR, float2(0.040, 0.0085), 0.0025)
    );
    float seg = ss(0.0018, 0.0032, abs(frac((abs(q.x) - 0.064) * 38.0) - 0.5));
    float tailCore = (1.0 - ss(-localPx, localPx, dTailBox)) * seg;
    float tailGlow = exp(-max(0.0, dTailBox) * 65.0);
    float tailWide = exp(-length((abs(q) - float2(0.104, 0.006)) * float2(0.8, 1.6)) * 22.0);

    col += float3(1.0, 0.12, 0.38) * (tailGlow * 0.95 + tailWide * 0.35);
    col = lerp(col, float3(1.0, 0.82, 0.88), tailCore);

    return col;
}

float4 main(PSIn input) : SV_Target {
    float2 glUv = float2(input.uv.x, 1.0 - input.uv.y);

    float aspect = uResolution.x / max(1.0, uResolution.y);
    float horY = 0.43;
    // Isotropic camera: on landscape (aspect >= 0.95) viewH = 1.0 (identical framing);
    // on portrait monitors (aspect < 0.95) viewH scales vertically to keep 1:1 pixel proportions without clipping road/car.
    float viewH = max(1.0, 0.95 / max(0.15, aspect));
    float pxScale = viewH / max(1.0, uResolution.y);
    float2 p = float2((glUv.x - 0.5) * aspect * viewH, (glUv.y - horY) * viewH);
    float2 sunCenter = float2(0.0, 0.088);

    // Precompute perspective road coordinates in uniform control flow
    float yFloor = max(0.0022, -p.y);
    float depth = 0.26 / yFloor;
    float worldX = p.x * depth;
    float driveSpeed = 3.1;
    float worldZ = depth + uGridTime * driveSpeed;
    float2 roadCoord = float2(worldX * 3.4, worldZ * 0.65);
    float2 roadDeriv = max(float2(0.0008, 0.0008), fwidth(roadCoord));
    float dEdgeScale = max(0.001, fwidth(worldX));

    // 1. Warm Miami Twilight Sky & Sunset Haze
    float3 skyTop = float3(0.039, 0.020, 0.114);
    float3 skyMid = float3(0.145, 0.039, 0.260);
    float3 skyHor = float3(0.410, 0.078, 0.310);
    float tSky = clamp((glUv.y - horY) / (1.0 - horY), 0.0, 1.0);
    float3 col = lerp(skyHor, skyMid, ss(0.0, 0.38, tSky));
    col = lerp(col, skyTop, ss(0.30, 0.92, tSky));

    float horizonWarm = exp(-abs(p.x) * 2.1) * exp(-max(0.0, p.y) * 5.5);
    col += float3(0.35, 0.08, 0.16) * horizonWarm;

    // 2. Retro Sliced Outrun Sunset
    float2 pSun = p - sunCenter;
    float sunR = 0.192;
    float sunDist = length(pSun);
    float sunBreath = 1.0 + 0.04 * sin(uTime * 1.25);

    float corona = exp(-max(0.0, sunDist - sunR) * 8.5) * 0.68 * sunBreath;
    float wideGlow = exp(-length(pSun * float2(0.7, 1.35)) * 2.6) * 0.45;
    col += float3(1.0, 0.18, 0.50) * (corona + wideGlow);

    if (p.y >= -0.002) {
        col += renderStarfield(float2((glUv.x - 0.5) * aspect * viewH, glUv.y * viewH), tSky * (1.0 - horY), uTime)
            * ss(sunR * 0.95, sunR * 1.45, sunDist);

        float discMask = (1.0 - ss(sunR - pxScale * 1.5, sunR + pxScale * 1.5, sunDist))
            * ss(-pxScale, pxScale * 1.5, p.y);
        if (discMask > 0.001) {
            float hNorm = clamp((pSun.y + sunR) / (2.0 * sunR), 0.0, 1.0);
            float3 sunTopCol = float3(1.0, 0.94, 0.40);
            float3 sunMidCol = float3(1.0, 0.43, 0.22);
            float3 sunBotCol = float3(1.0, 0.14, 0.50);
            float3 discCol = lerp(sunBotCol, sunMidCol, ss(0.08, 0.52, hNorm));
            discCol = lerp(discCol, sunTopCol, ss(0.45, 0.92, hNorm));

            float visH = clamp((hNorm - 0.28) / 0.40, 0.0, 1.0);
            float slicePhase = frac(visH * 5.6 + uTime * 0.25);
            float sliceThickness = lerp(0.40, 0.04, visH);
            float sliceGap = ss(sliceThickness + 0.04, sliceThickness - 0.015, slicePhase)
                * step(visH, 0.96)
                * ss(0.96, 0.80, visH);
            col = lerp(col, discCol, discMask * (1.0 - sliceGap));
        }

        // 3. Miami Art Deco Neon Skyline
        col = renderMiamiSkyline(p, pxScale, col, uTime);
    } else {
        // 4. Widened 4-Lane Miami Boulevard Street (Road + Curbs + Side Grid)
        float yFloorRaw = -p.y;
        float roadHalfW = 0.420;
        float sidewalkW = 0.550;
        float absX = abs(worldX);

        float3 floorNear = float3(0.032, 0.014, 0.078);
        float3 floorHor = float3(0.115, 0.028, 0.215);
        col = lerp(floorHor, floorNear, ss(0.0, 0.35 * viewH, yFloorRaw));

        float distFade = ss(0.004, 0.028, yFloorRaw);

        if (absX < roadHalfW) {
            float roadWave = sin(worldZ * 3.8 + sin(worldX * 14.0)) * 0.5 + 0.5;
            float asphaltSpec = exp(-absX * 4.2) * (0.65 + 0.35 * roadWave);
            col += float3(0.55, 0.12, 0.36) * asphaltSpec * ss(0.42 * viewH, 0.0, yFloorRaw) * 0.62;

            float sheenBand = 0.5 + 0.5 * sin(worldZ * 1.9);
            col += float3(0.08, 0.02, 0.14) * sheenBand * 0.22 * distFade;

            // Center double yellow/amber divider separating oncoming (left) vs forward (right) lanes
            float dashMask = step(0.42, frac(worldZ * 0.72));
            float centerLine = 1.0 - ss(0.0048, 0.0048 + dEdgeScale, absX);
            float centerHalo = exp(-absX / 0.018) * 0.35;
            col += float3(1.0, 0.78, 0.32) * (centerLine * 0.95 + centerHalo) * dashMask * distFade;

            // Lane dividers at +/- 0.21 (2 lanes on each side)
            float laneDist = abs(absX - 0.210);
            float laneLine = 1.0 - ss(0.0032, 0.0032 + dEdgeScale, laneDist);
            float laneDash = step(0.52, frac(worldZ * 0.72 + 0.25));
            col += float3(1.0, 0.22, 0.74) * laneLine * laneDash * 0.65 * distFade;
        } else if (absX < sidewalkW) {
            col = lerp(col, float3(0.055, 0.022, 0.125), 0.75);
            float tileZ = abs(frac(worldZ * 0.85) - 0.5) / max(0.001, roadDeriv.y);
            float tileLine = (1.0 - min(tileZ, 1.0)) * 0.35 * distFade;
            col += float3(0.75, 0.18, 0.95) * tileLine;
        } else {
            float2 gFrac = abs(frac(roadCoord - 0.5) - 0.5) / roadDeriv;
            float dLine = min(gFrac.x, gFrac.y);
            float lineCore = 1.0 - min(dLine, 1.0);
            float lineHalo = exp(-dLine * 0.75) * 0.22;
            float3 sideGridCol = lerp(float3(0.95, 0.18, 0.72), float3(0.0, 0.92, 1.0), exp(-yFloorRaw * 5.0 / viewH));
            col += sideGridCol * (lineCore * 0.78 + lineHalo) * distFade;
        }

        // Electric cyan glowing street curbs along both road edges
        float curbDist = abs(absX - roadHalfW);
        float curbCore = 1.0 - ss(0.006, 0.006 + dEdgeScale, curbDist);
        float curbGlow = exp(-curbDist / 0.026);
        col += float3(0.0, 0.94, 1.0) * (curbCore * 0.95 + curbGlow * 0.42) * distFade;
    }

    // 5. Electric Cyan Horizon Beam
    float horGlow = exp(-abs(p.y) * 130.0 / viewH);
    float horCore = exp(-abs(p.y) * 480.0 / viewH);
    col += float3(0.15, 0.94, 1.0) * (horGlow * 0.46 + horCore * 0.82);

    // 6. Back-to-Front Palm Trees, Streetlights & Oncoming Traffic (exact depth interleaving)
    // Palms extend down to zMin = 0.07 and vehicles down to zMin = 0.05 so both exit completely off-screen on arbitrary aspect ratios (from 9:21 portrait to 48:9 super-ultrawide) before wrapping.
    float stepZ = 1.45;
    float scrollCycles = (uGridTime * driveSpeed) / stepZ;
    float zFrac = frac(scrollCycles);
    float basePalmIdx = floor(scrollCycles);
    float zMinPalm = 0.07;

    float trafficSpeed = 6.4;
    float trafficStep = 3.1;
    float trafficCycles = (uGridTime * trafficSpeed) / trafficStep;
    float tFrac = frac(trafficCycles);
    float baseVehIdx = floor(trafficCycles);
    float zMinVeh = 0.05;

    float prevZ = 1e6;
    [loop]
    for (int k = 12; k >= 0; k--) {
        float zPalm = zMinPalm + (float(k) + (1.0 - zFrac)) * stepZ;

        // Draw any oncoming vehicles between prevZ and zPalm (back-to-front)
        [loop]
        for (int v = 8; v >= 0; v--) {
            float zVeh = zMinVeh + (float(v) + (1.0 - tFrac)) * trafficStep;
            if (zVeh < prevZ && zVeh >= zPalm) {
                evalOncomingVehicle(p, baseVehIdx + float(v), zVeh, pxScale, uTime, col);
            }
        }

        float invZ = 1.0 / max(0.06, zPalm);
        float anchorY = -0.26 * invZ;
        float palmOffset = 0.485;
        float seed = basePalmIdx + float(k);

        evalPalmAndLamp(p, -palmOffset * invZ, anchorY, invZ, -1.0, seed * 1.13 + 3.7, pxScale, uTime, col);
        evalPalmAndLamp(p,  palmOffset * invZ, anchorY, invZ,  1.0, seed * 1.71 + 9.2, pxScale, uTime, col);

        prevZ = zPalm;
    }

    // Draw any oncoming vehicle closer than the nearest palm
    [loop]
    for (int v2 = 8; v2 >= 0; v2--) {
        float zVeh2 = zMinVeh + (float(v2) + (1.0 - tFrac)) * trafficStep;
        if (zVeh2 < prevZ) {
            evalOncomingVehicle(p, baseVehIdx + float(v2), zVeh2, pxScale, uTime, col);
        }
    }

    // 7. Hero Synthwave Supercar in right-hand lane
    col = renderHeroCar(p, pxScale, col, uTime);

    return float4(saturate(col), 1.0);
}
