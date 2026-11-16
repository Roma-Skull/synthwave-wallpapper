Texture2D sceneTex : register(t0);
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

float3 sampleBright(float2 uv) {
    float3 s = sceneTex.SampleLevel(linearClamp, clamp(uv, 0.001, 0.999), 0).rgb;
    return max(float3(0.0, 0.0, 0.0), s - 0.34);
}

float4 main(PSIn input) : SV_Target {
    // 9-tap tent downsample running at 1/9th scene pixels (~1/25th native pixels)
    // Hardware bilinear filtering + inner/outer offsets create a smooth phosphor glow
    float2 invRes = 1.0 / max(float2(32.0, 32.0), uResolution);
    float2 d1 = invRes * 1.85;
    float2 d2 = invRes * 4.20;

    float3 acc = sampleBright(input.uv) * 0.24;
    acc += (sampleBright(input.uv + float2(-d1.x, -d1.y))
          + sampleBright(input.uv + float2( d1.x, -d1.y))
          + sampleBright(input.uv + float2(-d1.x,  d1.y))
          + sampleBright(input.uv + float2( d1.x,  d1.y))) * 0.125;
    acc += (sampleBright(input.uv + float2(-d2.x,  0.0))
          + sampleBright(input.uv + float2( d2.x,  0.0))
          + sampleBright(input.uv + float2( 0.0,  -d2.y))
          + sampleBright(input.uv + float2( 0.0,   d2.y))) * 0.065;

    return float4(acc, 1.0);
}
