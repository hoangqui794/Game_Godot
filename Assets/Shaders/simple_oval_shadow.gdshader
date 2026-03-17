shader_type canvas_item;

uniform vec4 shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.4);

void fragment() {
	vec2 center = vec2(0.5, 0.5);
	float dist = length((UV - center) * vec2(1.0, 2.5)); // Stretch into an oval
	float alpha = smoothstep(0.5, 0.4, dist);
	COLOR = vec4(shadow_color.rgb, shadow_color.a * alpha);
}
