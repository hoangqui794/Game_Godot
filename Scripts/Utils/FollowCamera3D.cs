using Godot;

public partial class FollowCamera3D : Camera3D
{
    [Export] public float SmoothSpeed = 5.0f;
    // Offset tương đối so với nhân vật. Y và Z bằng nhau => góc nhìn ~45 độ.
    [Export] public Vector3 FollowOffset = new Vector3(0, 10, 10);
    [Export] public float MinX = 0;
    [Export] public float MaxX = 100;
    [Export] public float MinY = -10;
    [Export] public float MaxY = 20;

    private float _shakeIntensity = 0f;
    private float _shakeTimer = 0f;
    private Node3D _target;
    private Vector3 _originalOffset;

    public override void _Ready()
    {
        MakeCurrent();
        AddToGroup("MainCamera");
        _originalOffset = Position;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_target == null || !IsInstanceValid(_target))
        {
            var player = GetTree().GetFirstNodeInGroup("player");
            if (player is Node3D p) _target = p;
        }

        if (_target != null)
        {
            // Điểm nhân vật để camera nhìn vào
            Vector3 focus = _target.GlobalPosition;

            // Vị trí mong muốn của camera với offset (tạo góc nhìn chéo)
            Vector3 desiredPos = focus + FollowOffset;
            desiredPos.X = Mathf.Clamp(desiredPos.X, MinX, MaxX);
            desiredPos.Y = Mathf.Clamp(desiredPos.Y, MinY, MaxY);

            GlobalPosition = GlobalPosition.Lerp(desiredPos, SmoothSpeed * dt);

            // Luôn nhìn về nhân vật để giữ góc ~45 độ ổn định
            LookAt(focus, Vector3.Up);
        }

        if (_shakeTimer > 0)
        {
            _shakeTimer -= dt;
            Vector3 shakeOffset = new Vector3(
                (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                0
            );
            HOffset = shakeOffset.X;
            VOffset = shakeOffset.Y;

            if (_shakeTimer <= 0)
            {
                HOffset = 0;
                VOffset = 0;
            }
        }
    }

    public void Shake(float duration, float intensity)
    {
        _shakeTimer = duration;
        _shakeIntensity = intensity;
    }
}
