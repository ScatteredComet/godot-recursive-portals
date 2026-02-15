/*
    Asset: Godot Simple Portal System
    File: portal.gdshader
    Description: Shader used by the simple portal system.
    Repository: https://github.com/Donitzo/godot-simple-portal-system
    License: CC0 License
*/

shader_type spatial;
render_mode unshaded, depth_prepass_alpha;

stencil_mode read, 1, compare_equal;

uniform sampler2D camera_texture : hint_default_black, source_color;

uniform float fade_out_distance_max = 10.0;
uniform float fade_out_distance_min = 8.0;
uniform vec4 fade_out_color = vec4(0.0);

void fragment()
{
    // The portal color is simply the screen-space color of the exit camera render target.
    // This is because the exit camera views the exit portal from the perspective of the player watching
    // the entrance portal, meaning the exit portal will occupy the same screen-space as the entrance portal.
	
    ALBEDO = texture(camera_texture, SCREEN_UV).rgb;
	//ALBEDO = vec3(1., 0., 0.);
	
	ALPHA = 1.; // have to write to alpha to prevent errors about reading without being part of the alpha pipeline
}