// Shader de cheveux du Salon : reflets anisotropes (Kajiya-Kay, double reflet),
// cheveux mouillés plus sombres et brillants, racines ombrées, léger balancement.
// Les lumières viennent de variables globales envoyées par SalonGame (_SalonLight...).
Shader "Salon/Hair"
{
    Properties
    {
        _SpecShift1 ("Décalage reflet principal", Range(-1, 1)) = 0.08
        _SpecShift2 ("Décalage reflet secondaire", Range(-1, 1)) = -0.15
        _SpecExp1 ("Netteté reflet principal", Float) = 160
        _SpecExp2 ("Netteté reflet secondaire", Float) = 36
        _SpecStrength1 ("Intensité reflet principal", Float) = 0.22
        _SpecStrength2 ("Intensité reflet secondaire", Float) = 0.3
        _Wrap ("Diffusion douce", Range(0, 1)) = 0.5
        _HairTex ("Carte de cheveux", 2D) = "white" {}
        _Tiling ("Répétition le long de la mèche", Float) = 7
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    float _SpecShift1, _SpecShift2, _SpecExp1, _SpecExp2, _SpecStrength1, _SpecStrength2, _Wrap, _Tiling;
    sampler2D _HairTex;
    float4 _HairWind;        // xyz = direction monde, w = amplitude
    float4 _HairWindParams;  // x = vitesse
    float4 _SalonLightDir;   // direction VERS la lumière principale
    float4 _SalonLightColor;
    float4 _SalonFillDir;
    float4 _SalonFillColor;
    float4 _SalonAmbient;

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float4 tangent : TANGENT;
        float4 color : COLOR;
        float2 uv0 : TEXCOORD0;   // x = côté du ruban, y = distance depuis la racine (m)
        float2 uv1 : TEXCOORD1;   // x = occlusion (couche), y = phase
        float2 uv2 : TEXCOORD2;   // x = proximité de la pointe (0..1)
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float3 nW : TEXCOORD0;
        float3 tW : TEXCOORD1;
        float3 wPos : TEXCOORD2;
        float4 data : TEXCOORD3;
        float tip : TEXCOORD4;
        float4 col : COLOR;
    };

    float3 HairWorldPos(appdata v)
    {
        float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
        float along = max(v.uv0.y, 0.0);
        float t = _Time.y * _HairWindParams.x;
        float sway = sin(t + v.uv1.y) + 0.5 * sin(t * 2.3 + v.uv1.y * 1.7);
        w += _HairWind.xyz * (_HairWind.w * sway * along * sqrt(along) * v.tangent.w);
        return w;
    }

    v2f vert(appdata v)
    {
        v2f o;
        float3 w = HairWorldPos(v);
        o.pos = mul(UNITY_MATRIX_VP, float4(w, 1.0));
        o.nW = UnityObjectToWorldNormal(v.normal);
        o.tW = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
        o.wPos = w;
        o.data = float4(v.uv0, v.uv1);
        o.col = v.color;
        o.tip = v.uv2.x;
        return o;
    }

    float StrandSpec(float3 T, float3 V, float3 L, float e)
    {
        float3 H = normalize(L + V);
        float dTH = dot(T, H);
        float sTH = sqrt(saturate(1.0 - dTH * dTH));
        float atten = smoothstep(-1.0, 0.0, dTH);
        return atten * pow(sTH, e);
    }

    // Alpha de la carte (cheveux fins) ; les pointes s'affinent.
    float CardAlpha(v2f i, out float shade)
    {
        float4 tx = tex2D(_HairTex, float2(i.data.x, i.data.y * _Tiling + i.data.w * 0.37));
        shade = tx.r;
        float th = lerp(0.5, 0.97, saturate(i.tip));
        return tx.a - th;
    }

    float4 fragHair(v2f i) : SV_Target
    {
        float shade;
        float ca = CardAlpha(i, shade);
        float a = ca / max(fwidth(ca), 1e-4) + 0.5;
        clip(a);
        float3 N = normalize(i.nW);
        float3 T = normalize(i.tW + 1e-5);
        float3 V = normalize(_WorldSpaceCameraPos.xyz - i.wPos);
        float wet = saturate(i.col.a);
        float along = i.data.y;
        float ao = i.data.z;

        float3 albedo = i.col.rgb * lerp(1.0, 0.55, wet) * lerp(0.8, 1.15, shade);
        float occl = ao * lerp(0.6, 1.0, saturate(along / 0.06));

        float3 L = normalize(_SalonLightDir.xyz + 1e-5);
        float3 L2 = normalize(_SalonFillDir.xyz + 1e-5);
        float diff = saturate((dot(N, L) + _Wrap) / (1.0 + _Wrap));
        float diff2 = saturate((dot(N, L2) + _Wrap) / (1.0 + _Wrap));

        float3 col = albedo * occl * (_SalonAmbient.rgb + _SalonLightColor.rgb * diff + _SalonFillColor.rgb * diff2);

        float e1 = lerp(_SpecExp1, _SpecExp1 * 2.5, wet);
        float s1 = lerp(_SpecStrength1, _SpecStrength1 * 2.2, wet);
        float3 T1 = normalize(T + N * _SpecShift1);
        float3 T2 = normalize(T + N * _SpecShift2);
        float spec1 = StrandSpec(T1, V, L, e1) * s1;
        float spec2 = StrandSpec(T2, V, L, _SpecExp2) * _SpecStrength2;
        float mask = saturate(diff * 1.2) * occl;
        col += _SalonLightColor.rgb * (spec1 + spec2 * albedo * 2.0) * mask;
        col += _SalonFillColor.rgb * StrandSpec(T1, V, L2, e1) * s1 * 0.5 * occl;

        // lumière qui traverse les mèches (contre-jour)
        float back = pow(saturate(dot(-V, L)), 4.0) * 0.3;
        col += albedo * _SalonLightColor.rgb * back;
        return float4(col, saturate(a));
    }

    float4 fragDepth(v2f i) : SV_Target
    {
        float shade;
        clip(CardAlpha(i, shade));
        return float4(i.pos.z, 0, 0, 0);
    }

    float4 fragDepthNormals(v2f i) : SV_Target
    {
        float shade;
        clip(CardAlpha(i, shade));
        return float4(normalize(i.nW), 0.0);
    }
    ENDCG

    // ---- URP ----
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "HairForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            AlphaToMask On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragHair
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepthNormals
            #pragma target 3.0
            ENDCG
        }
    }

    // ---- Pipeline intégré (secours) ----
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Off
            ZWrite On
            AlphaToMask On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragHair
            #pragma target 3.0
            ENDCG
        }
    }
}
