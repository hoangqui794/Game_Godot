using Godot;

/// <summary>
/// Spike Hazard 3D - Gai nhọn trồi lên từ mặt đất trong không gian 3D.
/// Sử dụng MeshInstance3D thay vì _Draw() để tạo hình gai.
/// </summary>
public partial class SpikeHazard3D : Node3D
{
    [Export] public int Damage = 25;
    [Export] public float DamageCooldown = 0.8f;

    [Export] public float UpDuration = 2.0f;
    [Export] public float DownDuration = 1.5f;
    [Export] public float StartDelay = 0.0f;

    [Export] public int SpikeCount = 4;
    [Export] public float SpikeSpacing = 0.5f;
    [Export] public float SpikeHeight = 1.0f;
    [Export] public float RiseSpeed = 4.0f;
    [Export] public float FallSpeed = 6.0f;

    private bool _isUp = false;
    private float _timer = 0f;
    private float _currentHeight = 0f;
    private bool _wantUp = false;

    private Area3D _hitArea;
    private CollisionShape3D _hitCollision;
    private Node3D _spikeVisual;
    private bool _canDamage = true;
    private Timer _damageCooldownTimer;
    private Node _playerOnSpike = null;

    private float _warnTimer = 0f;
    private bool _inWarnPhase = false;
    private const float WarnDuration = 0.35f;

    public override void _Ready()
    {
        BuildVisuals();
        BuildCollision();
        BuildDamageTimer();

        if (StartDelay > 0) _timer = -StartDelay;
        _currentHeight = 0f;
        _wantUp = false;
        _isUp = false;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _timer += dt;

        if (!_isUp)
        {
            if (_inWarnPhase)
            {
                _warnTimer += dt;
                // Cảnh báo nhấp nháy
                if (_spikeVisual != null)
                {
                    float blink = (_warnTimer % 0.12f < 0.06f) ? 1.5f : 1.0f;
                    // No direct modulate on Node3D, we handle via position jitter
                    Position = new Vector3(Position.X + (float)GD.RandRange(-0.02, 0.02), Position.Y, Position.Z);
                }

                if (_warnTimer >= WarnDuration)
                {
                    _inWarnPhase = false;
                    _isUp = true;
                    _wantUp = true;
                    _hitCollision?.SetDeferred("disabled", false);
                }
            }
            else if (_timer >= DownDuration)
            {
                _timer = 0f;
                _warnTimer = 0f;
                _inWarnPhase = true;
            }
        }
        else
        {
            if (_timer >= UpDuration)
            {
                _timer = 0f;
                _isUp = false;
                _wantUp = false;
                _hitCollision?.SetDeferred("disabled", true);
            }

            if (_playerOnSpike != null && _canDamage && _playerOnSpike.HasMethod("TakeDamage"))
            {
                _playerOnSpike.Call("TakeDamage", Damage);
                _canDamage = false;
                _damageCooldownTimer.Start();
            }
        }

        float target = _wantUp ? SpikeHeight : 0f;
        float speed = _wantUp ? RiseSpeed : FallSpeed;
        _currentHeight = Mathf.MoveToward(_currentHeight, target, speed * dt);

        if (_spikeVisual != null)
        {
            _spikeVisual.Scale = new Vector3(1, Mathf.Max(0.01f, _currentHeight / SpikeHeight), 1);
            _spikeVisual.Position = new Vector3(0, _currentHeight * 0.5f, 0);
        }
    }

    private void BuildVisuals()
    {
        _spikeVisual = new Node3D();
        _spikeVisual.Scale = new Vector3(1, 0.01f, 1);
        AddChild(_spikeVisual);

        float totalW = (SpikeCount - 1) * SpikeSpacing;
        float startX = -totalW / 2f;

        for (int i = 0; i < SpikeCount; i++)
        {
            float cx = startX + i * SpikeSpacing;

            var mesh = new MeshInstance3D();
            var prism = new PrismMesh();
            prism.LeftToRight = 0.5f;
            prism.Size = new Vector3(0.3f, SpikeHeight, 0.3f);
            mesh.Mesh = prism;
            mesh.Position = new Vector3(cx, 0, 0);

            // Material nâu đất cho gai
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.56f, 0.38f, 0.20f);
            mat.Metallic = 0.3f;
            mat.Roughness = 0.7f;
            mesh.MaterialOverride = mat;

            _spikeVisual.AddChild(mesh);
        }
    }

    private void BuildCollision()
    {
        _hitArea = new Area3D();
        _hitArea.CollisionLayer = 0;
        _hitArea.CollisionMask = 2; // Player layer

        _hitCollision = new CollisionShape3D();
        var shape = new BoxShape3D();
        float totalW = (SpikeCount - 1) * SpikeSpacing + 0.5f;
        shape.Size = new Vector3(totalW, SpikeHeight, 0.5f);
        _hitCollision.Shape = shape;
        _hitCollision.Position = new Vector3(0, SpikeHeight / 2f, 0);
        _hitCollision.Disabled = true;

        _hitArea.AddChild(_hitCollision);
        _hitArea.BodyEntered += OnBodyEntered;
        _hitArea.BodyExited += OnBodyExited;
        AddChild(_hitArea);
    }

    private void BuildDamageTimer()
    {
        _damageCooldownTimer = new Timer();
        _damageCooldownTimer.WaitTime = DamageCooldown;
        _damageCooldownTimer.OneShot = true;
        _damageCooldownTimer.Timeout += () => { _canDamage = true; };
        AddChild(_damageCooldownTimer);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("player"))
        {
            _playerOnSpike = body;
            if (_canDamage && _isUp && body.HasMethod("TakeDamage"))
            {
                body.Call("TakeDamage", Damage);
                _canDamage = false;
                _damageCooldownTimer.Start();
            }
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body.IsInGroup("player")) _playerOnSpike = null;
    }
}
