/*
    Asset: Godot Simple Portal System
    File: portal.gdshader
    Description: Shader used by the simple portal system.
    Repository: https://github.com/Donitzo/godot-simple-portal-system
    License: CC0 License
	
	Modified by ScatteredComet
*/

shader_type spatial;
render_mode unshaded, cull_disabled;

//stencil_mode read, 0, compare_less_or_equal;

uniform sampler2D camera_texture : hint_default_black, source_color;

uniform bool debug_view_stencil_mask = false;

void fragment()
{
    ALBEDO = texture(camera_texture, SCREEN_UV).rgb;
	ALPHA = 1.; // have to write to alpha to prevent errors about reading without being part of the alpha pipeline
	
	if (debug_view_stencil_mask)
	{
		ALBEDO = vec3(1., 0., 0.);
	}
}