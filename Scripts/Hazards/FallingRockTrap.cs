using Godot;

public partial class FallingRockTrap : Node2D
{
    [Export] public int Damage = 40;
    [Export] public float Gravity = 1200f;
    [Export] public float TriggerRange = 250f; // Khoảng cách nhìn xuống dưới để rớt
    [Export] public float RockRadius = 30f;

    private bool _isFalling = false;
    private bool _hasHit = false;
    private float _velocityY = 60f; // Tốc độ rơi ban đầu
    private Sprite2D _rockVisual;
    private Area2D _hitArea;
    private Area2D _triggerArea;

    private CpuParticles2D _dustVfx;

    public override void _Ready()
    {
        // Visual
        _rockVisual = new Sprite2D();
        _rockVisual.Texture = GD.Load<Texture2D>("res://Assets/Sprites/Environment/rock_pixel.png");
        _rockVisual.Scale = new Vector2(0.3f, 0.3f);
        AddChild(_rockVisual);

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
        
        // Mở rộng bề ngang vùng kích hoạt một cách ngẫu nhiên nhưng VỪA PHẢI
        // Để người chơi chạy gần tới (hoặc vừa đạp mép) thì đá mới rớt, tạo độ gắt!
        float randomTriggerWidth = (float)GD.RandRange(60.0, 160.0);
        var rect = new RectangleShape2D { Size = new Vector2(randomTriggerWidth, TriggerRange) };
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

    private void OnTrigger(Node2D body)
    {
        if (body is Player && !_isFalling)
        {
            _isFalling = true;
            _triggerArea.QueueFree(); // Trigger một lần

            // Random tốc độ rơi ban đầu và trọng lực để người chơi không thể đoán trước timing!
            _velocityY = (float)GD.RandRange(80.0, 450.0);
            Gravity = (float)GD.RandRange(1200.0, 2000.0);

            // Xoay nhẹ để đá sinh động lúc rớt (hướng random)
            float randomRot = (float)GD.RandRange(-Mathf.Pi * 2, Mathf.Pi * 2);
            var tw = CreateTween().SetLoops();
            tw.TweenProperty(this, "rotation", Rotation + randomRot, 0.6f);
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

            // Chạm đất tự vỡ (Tạm thời tăng giới hạn Y để phù hợp với Màn 2 sâu hơn)
            if (GlobalPosition.Y > 2000f)
            {
                Shatter();
            }
        }
    }
}
