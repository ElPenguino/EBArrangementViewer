/// <description>Earthbound battle background effect.</description>

/// <summary>Time.</summary>
/// <defaultValue>0</defaultValue>
float timer: register(C0);

/// <summary>Horizontal Amplitude.</summary>
/// <defaultValue>0</defaultValue>
float hamplitude: register(C1);

/// <summary>Horizontal Period.</summary>
/// <defaultValue>0</defaultValue>
float hperiod: register(C2);

/// <summary>Vertical Amplitude.</summary>
/// <defaultValue>0</defaultValue>
float vamplitude: register(C3);

/// <summary>Vertical Period.</summary>
/// <defaultValue>0</defaultValue>
float vperiod: register(C4);

/// <summary>Time divisor.</summary>
/// <defaultValue>200</defaultValue>
float timerdivider: register(C5);

/// <summary>Horizontal Drift.</summary>
/// <defaultValue>0</defaultValue>
float hdrift: register(C6);

/// <summary>Vertical Drift.</summary>
/// <defaultValue>0</defaultValue>
float vdrift: register(C7);

/// <summary>The implicit input sampler passed into the pixel shader by WPF.</summary>
/// <samplingMode>Auto</samplingMode>
sampler2D Input : register(s0) = sampler_state { AddressU = wrap; AddressV = wrap; AddressW = wrap; };
float4 main( float2 Tex : TEXCOORD0 ) : COLOR0
{
    float4 Color;
	Tex.x = (1 + Tex.x + (sin((Tex.y + timer/timerdivider) * hperiod)*hamplitude) + (hdrift/256*timer)) % 1;
	Tex.y = (1 + Tex.y + (sin((Tex.x + timer/timerdivider) * vperiod)*vamplitude) + (vdrift/256*timer)) % 1;
	Color = tex2D( Input, Tex.xy);
    return Color;
}


technique PostProcess
{
    pass p1
    {
        VertexShader = null;
        PixelShader = compile ps_2_0 main();
    }

}