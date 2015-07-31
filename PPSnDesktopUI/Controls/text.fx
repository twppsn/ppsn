sampler2D implicitInput : register(s0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 color = tex2D(implicitInput, uv);
	float4 result; 

	float gray = color.r * 0.3 + color.g * 0.59 + color.b * 0.11;
	//float a;
	//if (uv.x < 0.5 && uv.y < 0.5)
	//{
	//	gray = 1.0;
	//	a = 0.0;
	//}
	//else
		//a = color.a;

	result = float4(gray, gray, gray, 1.0) * color.a * 0.3;

	return result;
}