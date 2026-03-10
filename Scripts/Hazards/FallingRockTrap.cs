using Godot;

public partial class FallingRockTrap : Node2D
{
    [Export] public int Damage = 40;
    [Export] public float Gravity = 1200f;
    [Export] public float TriggerRange = 250f; // Khoảng cách nhìn xuống dưới để rớt
    [Export] public float RockRadius = 30f;

    private bool _isFalling = false;
    private bool _hasHit = false;
    private float _velocityY = 60f; // Có tốc độ ban đầu một chút
    private Area2D _hitArea;
    private Area2D _triggerArea;

    private CpuParticles2D _dustVfx;

    public override void _Ready()
    {
        // Vẽ đá rơi bằng hiệu ứng Shader hoặc chỉ cần cấu trúc _Draw hình tròn. Mình sẽ _Draw() bên dưới
        QueueRedraw();

        // Hit Area
        _hitArea = new Area2D();
        _hitArea.CollisionLayer = 0;
        _hitArea.CollisionMask = 1; // Player
        var hitShape = new CollisionShape2D();
        var circle = new CircleShape2D { Radius = RockRadius };
        hitShape.Shape = circle;
        _hitArea.AddChild(hitShape);
        _hitArea.BodyEntered += OnHitPlayer;
        AddChild(_hitArea);

        // Quét Player trúng vạch thì bắt đầu rớt
        _triggerArea = new Area2D();
        _triggerArea.CollisionLayer = 0;
        _triggerArea.CollisionMask = 1; // Player
        var trigShape = new CollisionShape2D();
        var rect = new RectangleShape2D { Size = new Vector2(RockRadius * 2, TriggerRange) };
        trigShape.Shape = rect;
        trigShape.Position = new Vector2(0, TriggerRange / 2);
        _triggerArea.AddChild(trigShape);
        _triggerArea.BodyEntered += OnTrigger;
        AddChild(_triggerArea);

        _dustVfx = new CpuParticles2D();
        _dustVfx.Emitting = false;
        _dustVfx.OneShot = true;
        _dustVfx.Amount = 25;
        _dustVfx.Lifetime = 0.6f;
        _dustVfx.Explosiveness = 0.9f;
        _dustVfx.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
        _dustVfx.EmissionSphereRadius = RockRadius;
        _dustVfx.Direction = new Vector2(0, -1);
        _dustVfx.Spread = 180f;
        _dustVfx.InitialVelocityMin = 50f;
        _dustVfx.InitialVelocityMax = 150f;
        _dustVfx.ScaleAmountMin = 2f;
        _dustVfx.ScaleAmountMax = 5f;
        _dustVfx.Color = new Color(0.6f, 0.55f, 0.45f, 0.8f);
        AddChild(_dustVfx);
    }

    public override void _Draw()
    {
        // 3D-like rock pixel art
        DrawCircle(Vector2.Zero, RockRadius, new Color(0.24f, 0.20f, 0.16f));
        DrawCircle(new Vector2(-5, -5), RockRadius * 0.8f, new Color(0.35f, 0.28f, 0.20f));
        DrawCircle(new Vector2(-10, -10), RockRadius * 0.4f, new Color(0.45f, 0.38f, 0.26f));
        
        // Móp méo
        DrawRect(new Rect2(-RockRadius * 0.5f, 0, RockRadius, RockRadius*0.8f), new Color(0.28f, 0.22f, 0.18f));
    }

    private void OnTrigger(Node2D body)
    {
        if (body is Player && !_isFalling)
        {
            _isFalling = true;
            _triggerArea.QueueFree(); // Trigger một lần
            
            // Xoay nhẹ để đá sinh động lúc rớt
            var tw = CreateTween().SetLoops();
            tw.TweenProperty(this, "rotation", Rotation + Mathf.Pi, 1.0f);
        }
    }

    private void OnHitPlayer(Node2D body)
    {
        if (body is Player player && _isFalling && !_hasHit)
        {
            _hasHit = true;
            player.TakeDamage(Damage);
            Shatter();
        }
    }

    private void Shatter()
    {
        _isFalling = false;
        
        var cam = GetViewport().GetCamera2D();
        if (cam != null && cam.HasMethod("Shake"))
        {
            cam.Call("Shake", 0.3f, 10f);
        }

        _hitArea.QueueFree();
        _dustVfx.Emitting = true;
        
        // Tàn biến nhanh
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0f, 0.2f);
        tw.Finished += () => QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isFalling && !_hasHit)
        {
            float dt = (float)delta;
            _velocityY += Gravity * dt;
            Position += new Vector2(0, _velocityY * dt);

            // Chạm đất tự vỡ 
            if (Position.Y > 580f) // Mặt đất trung bình Level 1
            {
                Shatter();
            }
        }
    }
}
