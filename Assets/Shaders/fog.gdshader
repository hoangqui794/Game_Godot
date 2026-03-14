shader_type canvas_item;

uniform vec4 fog_color : source_color = vec4(0.2, 0.3, 0.25, 0.4);
uniform float fog_density = 0.5;
uniform float fog_speed = 0.2;

void fragment() {
    vec2 uv = UV;
    
    // Animated noise for fog
    float noise = sin(uv.x * 3.0 + TIME * fog_speed) * 0.5 + 0.5;
    noise *= sin(uv.y * 2.0 - TIME * fog_speed * 1.5) * 0.5 + 0.5;
    
    float mask = smoothstep(0.4, 1.0, UV.y);
    COLOR = vec4(fog_color.rgb, fog_color.a * noise * mask * fog_density);
}
