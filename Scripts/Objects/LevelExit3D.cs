using Godot;

/// <summary>
/// Lối thoát màn chơi 3D - kích hoạt khi boss chết hoặc rương mở xong.
/// </summary>
public partial class LevelExit3D : Area3D
{
    private bool _isActive = false;
    private Label3D _hintLabel;

    public override void _Ready()
    {
        AddToGroup("LevelExit");
        CollisionLayer = 0;
        CollisionMask = 2; // Player

        BodyEntered += OnBodyEntered;

        _hintLabel = new Label3D();
        _hintLabel.Text = "✦ LỐI THOÁT ✦";
        _hintLabel.Position = new Vector3(0, 2, 0);
        _hintLabel.Visible = false;
        _hintLabel.FontSize = 64;
        _hintLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _hintLabel.PixelSize = 0.01f;
        _hintLabel.Modulate = new Color(0.5f, 1.0f, 0.5f);
        AddChild(_hintLabel);

        Visible = false;
    }

    public void Activate()
    {
        _isActive = true;
        Visible = true;
        _hintLabel.Visible = true;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!_isActive || !body.IsInGroup("player")) return;
        GD.Print("Hoàn thành level!");
        GameManager.Instance?.NextLevel();
    }
}
