using Godot;

/// <summary>
/// Cánh cửa đá cổ kính ở cuối Ải 1.
/// Khi Player đến chạm vào → FadeOut → chuyển sang Ải 2.
/// </summary>
public partial class CaveDoor : Area2D
{
    [Export] public string NextScenePath = "res://Scenes/Levels/Level2.tscn";
    [Export] public float TransitionDelay = 0.5f; // Đợi chút trước khi fade

    private bool _isTriggered = false;
    private Label _hintLabel;
    private AnimatedSprite2D _doorGlow;
    public bool IsLocked = false;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1; // Detect Player
        AddToGroup("cave_door");

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        // Tạo label gợi ý
        _hintLabel = new Label();
        _hintLabel.Text = "✦ Cửa Hang ✦\nTiến vào...";
        _hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _hintLabel.VerticalAlignment = VerticalAlignment.Center;
        _hintLabel.Position = new Vector2(-80, -95);
        _hintLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f)); // Vàng ánh
        _hintLabel.AddThemeFontSizeOverride("font_size", 15);
        _hintLabel.Visible = false;
        AddChild(_hintLabel);

        // Hiệu ứng glow xung quanh cửa
        StartDoorGlowEffect();
    }

    public override void _Process(double delta)
    {
        // Animate label nhấp nháy nhẹ
        if (_hintLabel != null && _hintLabel.Visible)
        {
            float alpha = 0.7f + 0.3f * Mathf.Sin((float)Time.GetTicksMsec() * 0.003f);
            _hintLabel.Modulate = new Color(1, 1, 1, alpha);
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_isTriggered) return;
        if (body is not Player player) return;

        if (IsLocked)
        {
            _hintLabel.Text = "✦ Cửa Hang ✦\nCần tìm rương báu để mở cửa!";
            _hintLabel.Visible = true;
            return;
        }

        _isTriggered = true;
        _hintLabel.Visible = false;

        // Ép Player chuyển sang Cutscene Mode: Khóa phím, tiếp tục chạy bộ vào cửa phía bên phải
        player.WalkIntoCave(1f);

        // Delay 1 giây đợi nhân vật kịp lặn vào bóng tối của hang rồi mới tải Level 2
        var timer = GetTree().CreateTimer(1.0f);
        timer.Timeout += () => DoTransition();
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player && !_isTriggered)
        {
            _hintLabel.Visible = false;
        }
    }

    private void DoTransition()
    {
        // Nhờ GameManager kích hoạt Fade Global Màn Hình và load Scene kế!
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NextLevel();
        }
    }

    private void StartDoorGlowEffect()
    {
        // Tạo hiệu ứng ánh sáng xung quanh cửa
        // Dùng Tween để pulse màu sắc node cha
        var tween = CreateTween();
        tween.SetLoops(); // Lặp vô tận
        tween.TweenProperty(this, "modulate", new Color(1.2f, 1.1f, 0.8f), 1.2f);
        tween.TweenProperty(this, "modulate", Colors.White, 1.2f);
    }
}
