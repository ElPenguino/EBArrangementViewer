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

/// <summary>Horizontal Frequency</summary>
/// <defaultValue>1</defaultValue>
float hfrequency: register(C3);

/// <summary>Vertical Amplitude.</summary>
/// <defaultValue>0</defaultValue>
float vamplitude: register(C4);

/// <summary>Vertical Period.</summary>
/// <defaultValue>0</defaultValue>
float vperiod: register(C5);

/// <summary>Vertical Frequency.</summary>
/// <defaultValue>1</defaultValue>
float vfrequency: register(C6);

/// <summary>Horizontal Drift.</summary>
/// <defaultValue>0</defaultValue>
float hdrift: register(C7);

/// <summary>Vertical Drift.</summary>
/// <defaultValue>0</defaultValue>
float vdrift: register(C8);

/// <summary>The implicit input sampler passed into the pixel shader by WPF.</summary>
/// <samplingMode>Auto</samplingMode>
sampler2D Input : register(s0);
float4 main( float2 Tex : TEXCOORD0 ) : COLOR0
{
    float4 Color;
	Tex.y = (-1.0 * hdrift * (timer / 256.0)) + hamplitude * sin(2.0 * 3.1415 * hperiod * ((timer / 65536.0) + Tex.x));
	Tex.x = (-1.0 * vdrift * (timer / 256.0)) + vamplitude * sin(2.0 * 3.1415 * vperiod * ((timer / 65536.0) + Tex.y));
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