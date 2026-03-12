using Godot;

/// <summary>
/// Đá lăn từ một phía sang phía kia, gây sát thương và knockback khi va chạm Player.
/// Tự huỷ khi ra ngoài màn hình.
/// </summary>
public partial class RollingRock : CharacterBody2D
{
    [Export] public float RollSpeed = 180.0f;       // Tốc độ lăn (px/s)
    [Export] public int Damage = 30;                // Sát thương gây ra
    [Export] public float KnockbackForce = 350.0f;  // Lực đẩy Player khi trúng
    [Export] public float Gravity = 900.0f;          // Trọng lực
    [Export] public float DestroyOffscreenX = 200f; // Huỷ khi ra ngoài màn này px

    private float _direction = -1f; // -1 = lăn trái, 1 = lăn phải
    private bool _hasHitPlayer = false;

    // Visual
    private Sprite2D _rockVisual;

    public override void _Ready()
    {
        // Đặt layer enemies để player không block nó
        CollisionLayer = 8; // Layer 4 (index 3)
        CollisionMask = 2;  // Chỉ va chạm với Ground (layer 2)

        AddToGroup("rolling_rocks");

        // Sử dụng ảnh đá "xịn" từ Level 1 để không bị phèn
        _rockVisual = new Sprite2D();
        _rockVisual.Texture = GD.Load<Texture2D>("res://Assets/Sprites/Environment/rock_pixel.png");
        _rockVisual.Scale = new Vector2(0.4f, 0.4f); // Chỉnh scale cho vừa kích cỡ 28px radius
        AddChild(_rockVisual);

        // Tạo Area2D để detect Player
        var hitArea = new Area2D();
        hitArea.CollisionLayer = 0;
        hitArea.CollisionMask = 1; // Player

        var hitShape = new CollisionShape2D();
        var circle = new CircleShape2D();
        circle.Radius = 28f;
        hitShape.Shape = circle;
        hitArea.AddChild(hitShape);
        hitArea.BodyEntered += OnHitAreaBodyEntered;
        AddChild(hitArea);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector2 vel = Velocity;

        // Áp dụng trọng lực
        if (!IsOnFloor())
        {
            vel.Y += Gravity * dt;
        }

        // Di chuyển theo hướng
        vel.X = _direction * RollSpeed;

        // Xoay visual để tạo cảm giác lăn
        if (_rockVisual != null)
        {
            _rockVisual.Rotation += _direction * 3.0f * dt;
        }

        Velocity = vel;
        MoveAndSlide();

        // Tự huỷ khi ra ngoài màn hình
        var camera = GetTree().GetFirstNodeInGroup("MainCamera") as Camera2D;
        if (camera != null)
        {
            float camLeft = camera.GlobalPosition.X - 700f;
            float camRight = camera.GlobalPosition.X + 700f;
            if (GlobalPosition.X < camLeft - DestroyOffscreenX ||
                GlobalPosition.X > camRight + DestroyOffscreenX ||
                GlobalPosition.Y > 1000f) // Rơi quá sâu
            {
                QueueFree();
            }
        }
        else
        {
            // Fallback: huỷ nếu quá xa gốc
            if (GlobalPosition.Y > 1500f) QueueFree();
        }
    }

    /// <summary>
    /// Đặt hướng lăn: -1 = lăn trái, 1 = lăn phải
    /// </summary>
    public void SetDirection(float dir)
    {
        _direction = Mathf.Sign(dir);
    }

    private void OnHitAreaBodyEntered(Node2D body)
    {
        if (_hasHitPlayer) return;
        if (body is Player player)
        {
            _hasHitPlayer = true;
            player.TakeDamage(Damage);

            // Knockback ra xa hướng đá lăn
            // Reset sau 0.5s để có thể hit lại nếu player quay lại
            var timer = GetTree().CreateTimer(0.5);
            timer.Timeout += () => { _hasHitPlayer = false; };
        }
    }
}

