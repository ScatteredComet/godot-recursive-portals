shader_type spatial;

stencil_mode write_depth_fail, compare_always, 2;

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