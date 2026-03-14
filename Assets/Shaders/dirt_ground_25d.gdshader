shader_type canvas_item;

varying vec2 world_pos;

uniform float top_ratio = 0.25;
uniform float thickness_shading = 0.4;
uniform float cell_size = 24.0;

float hash2(vec2 p) {
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 74.1);
    return fract(p.x * p.y);
}

void vertex() {
    // Get global world position for seamless tiling across segments
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

vec3 get_dirt_color(vec2 p_uv) {
    vec2 uv = p_uv / cell_size;
    float px = 1.0 / 6.0;
    uv = floor(uv / px) * px;
    
    vec2 g = floor(uv);
    vec2 f = fract(uv);
    float min_d = 10.0;
    float cell_val = 0.0;

    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            vec2 n = vec2(float(x), float(y));
            vec2 rnd = vec2(hash2(g + n), hash2(g + n + vec2(7.3, 3.7)));
            rnd = 0.5 + 0.4 * sin(rnd * 6.28318);
            vec2 d = n + rnd - f;
            float dist = length(d);
            if (dist < min_d) {
                min_d = dist;
                cell_val = hash2(g + n + vec2(13.1, 19.7));
            }
        }
    }

    vec3 base  = vec3(0.66, 0.43, 0.22);
    vec3 light = vec3(0.80, 0.57, 0.32);
    vec3 dark  = vec3(0.50, 0.31, 0.14);
    vec3 edge  = vec3(0.26, 0.14, 0.05);

    vec3 col = mix(dark, mix(base, light, cell_val), cell_val * 0.7 + 0.3);
    float border = smoothstep(0.05, 0.16, min_d);
    return mix(edge, col, border);
}

void fragment() {
    vec2 uv = UV;
    vec3 col;
    
    if (uv.y < top_ratio) {
        // TOP FACE
        float top_uv_y = uv.y / top_ratio;
        // Perspective depth: 1.0 at the front (uv.y=top_ratio), higher at the back
        float depth = 1.0 / (mix(0.6, 1.0, top_uv_y));
        
        // Use global world_pos.x but apply perspective towards the horizontal center of the screen
        // Approximation: warp based on world X relative to some reference
        vec2 p_pos = vec2(world_pos.x * depth, world_pos.y);
        col = get_dirt_color(p_pos);
        
        // Highlight edge
        float edge = smoothstep(0.85, 1.0, top_uv_y);
        col = mix(col, col * 1.25, edge);
    } else {
        // FRONT FACE
        float front_uv_y = (uv.y - top_ratio) / (1.0 - top_ratio);
        // Front face uses standard world_pos for seamless vertical/horizontal tiling
        col = get_dirt_color(world_pos);
        col *= (1.0 - thickness_shading);
        
        // Ambient occlusion / depth darkening
        col *= mix(1.0, 0.5, front_uv_y);
    }
    
    COLOR = vec4(col, 1.0);
}
