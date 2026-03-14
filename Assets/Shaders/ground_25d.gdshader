shader_type canvas_item;

uniform float top_ratio = 0.3; // How much of the height is the top surface
uniform float tilt = 0.15;     // Lateral perspective tilt
uniform float thickness_shading = 0.3; // Darkness of the front face
uniform vec4 top_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 side_color : source_color = vec4(0.6, 0.4, 0.3, 1.0);

void fragment() {
	vec2 uv = UV;
	
	if (uv.y < top_ratio) {
		// --- TOP SURFACE (Surface where player walks) ---
		float top_uv_y = uv.y / top_ratio;
		
		// Perspective warp
		float depth = 1.0 / (top_uv_y * 0.5 + 0.5);
		float p_uv_x = (uv.x - 0.5) * depth + 0.5;
		
		// Texture sampling for top
		vec2 top_uv = vec2(p_uv_x, top_uv_y);
		vec4 tex = texture(TEXTURE, top_uv);
		COLOR = tex * top_color;
		
		// Add a highlight to the front edge
		float edge = smoothstep(0.95, 1.0, top_uv_y);
		COLOR.rgb = mix(COLOR.rgb, COLOR.rgb * 1.5, edge);
		
	} else {
		// --- FRONT FACE (Thickness) ---
		float side_uv_y = (uv.y - top_ratio) / (1.0 - top_ratio);
		
		// Side texture (can be the same but stretched/offset)
		vec2 side_uv = vec2(uv.x, side_uv_y * 0.5);
		vec4 tex = texture(TEXTURE, side_uv);
		
		COLOR = tex * side_color;
		COLOR.rgb *= (1.0 - thickness_shading);
		
		// Ambience/Fog at the bottom
		COLOR.rgb = mix(COLOR.rgb, COLOR.rgb * 0.2, side_uv_y);
	}
}
