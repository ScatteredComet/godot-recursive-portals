shader_type spatial;

stencil_mode write_depth_fail, compare_always, 1;

void fragment()
{
	ALPHA = 0.;
}