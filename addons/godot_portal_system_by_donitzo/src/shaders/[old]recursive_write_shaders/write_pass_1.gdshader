shader_type spatial;

stencil_mode write, compare_always, 1;

uniform bool debug_visible = false;
uniform vec3 debug_color : source_color = vec3(1., 0., 0.);

void fragment()
{
	if (debug_visible)
	{
		ALBEDO = debug_color;
	}
	else
	{
		ALPHA = 0.;
	}
}