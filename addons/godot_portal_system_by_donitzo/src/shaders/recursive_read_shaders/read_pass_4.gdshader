shader_type spatial;
render_mode unshaded;

stencil_mode read, 4, compare_equal;

uniform sampler2D camera_texture : hint_default_black, source_color;

uniform bool debug_view_stencil_mask = false;

void fragment()
{
    ALBEDO = texture(camera_texture, SCREEN_UV).rgb;
	ALPHA = 1.;
	
	if (debug_view_stencil_mask)
	{
		ALBEDO = vec3(1., 0., 1.);
	}
}