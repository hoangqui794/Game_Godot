shader_type canvas_item;

uniform vec4 shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.4);
uniform float shadow_skew = 0.5;
uniform float shadow_scale_y = 0.3;

void vertex() {
	// Flatten and skew the sprite to the ground
	if (VERTEX.y < 0.0) {
		VERTEX.y *= shadow_scale_y;
		VERTEX.x += VERTEX.y * shadow_skew;
	}
}

void fragment() {
	vec4 col = texture(TEXTURE, UV);
	if (col.a > 0.1) {
		COLOR = shadow_color;
		// Fade shadow towards the top (which is the far end of the ground)
		COLOR.a *= UV.y;
	} else {
		discard;
	}
}
