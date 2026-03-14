shader_type canvas_item;

uniform float tilt_factor = 0.05;
uniform float lighting_intensity = 0.2;
uniform vec2 light_direction = vec2(1.0, 0.0);
uniform float shadow_height = 0.1;

void vertex() {
	// Global position approximation to tilt based on screen position
	float screen_pos = (CANVAS_MATRIX * MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).x;
	float tilt = (screen_pos / 1152.0 - 0.5) * tilt_factor;
	
	// Tilt the top of the sprite more than the bottom
	float tilt_mask = 1.0 - UV.y;
	VERTEX.x += tilt * tilt_mask * 100.0;
}

void fragment() {
	vec4 col = texture(TEXTURE, UV);
	
	// Fake volume lighting
	float light = dot(NORMAL, vec3(light_direction, 0.0).xyz) * lighting_intensity;
	// Since we don't have real normals, we simulate it based on horizontal UV
	float fake_normal = (UV.x - 0.5) * 2.0;
	float fake_light = fake_normal * light_direction.x * lighting_intensity;
	
	col.rgb += fake_light;
	
	COLOR = col;
}
