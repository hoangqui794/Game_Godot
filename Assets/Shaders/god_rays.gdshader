shader_type canvas_item;

uniform vec4 ray_color : source_color = vec4(1.0, 0.9, 0.5, 0.5);
uniform float ray_angle = -0.5;
uniform float ray_density = 0.7;
uniform float ray_speed = 1.0;
uniform float ray_intensity = 1.5;

void fragment() {
    vec2 uv = UV;
    
    // Rotate UV for angle
    float s = sin(ray_angle);
    float c = cos(ray_angle);
    uv = vec2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
    
    // Sharper noise-like beams
    float rays = sin(uv.x * 25.0 + TIME * ray_speed) * 0.5 + 0.5;
    rays *= sin(uv.x * 15.0 - TIME * ray_speed * 0.8) * 0.5 + 0.5;
    
    // Sharpening the beams
    rays = pow(rays, 12.0 - ray_density * 8.0);
    
    // Fade at top and bottom
    rays *= smoothstep(0.0, 0.4, UV.y) * (1.0 - UV.y);
    
    COLOR = ray_color * rays * ray_intensity;
}
