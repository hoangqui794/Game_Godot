shader_type canvas_item;

uniform float height = 0.6;
uniform float scroll_speed = 0.1;
uniform vec4 horizon_color : source_color = vec4(0.08, 0.14, 0.1, 1.0);

void fragment() {
    vec2 uv = UV;
    
    // Perspective warp for 2.5D look
    float depth = 1.0 / (uv.y * height + (1.0 - height));
    uv.x = (uv.x - 0.5) * depth + 0.5;
    
    // Auto scroll based on time (for demo, in real case use camera pos)
    uv.x += TIME * scroll_speed;
    
    vec4 tex = texture(TEXTURE, uv);
    
    // Fade to horizon
    float fade = smoothstep(0.0, 0.2, UV.y);
    COLOR = mix(horizon_color, tex, fade);
}
