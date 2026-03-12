using Godot;

public partial class PrincessCage : StaticBody2D
{
    private Area2D _interactArea;
    private ColorRect _visual;
    private bool _isOpened = false;

    public override void _Ready()
    {
        // Thân lồng (Phần nền tối bên trong)
        _visual = new ColorRect();
        _visual.Color = new Color(0.1f, 0.1f, 0.12f, 0.7f); // Tối hơn để nổi bật Công Chúa
        _visual.Size = new Vector2(140, 180); // To bự hơn (Cũ: 80x100)
        _visual.Position = new Vector2(-70, -180);
        _visual.ZIndex = -1; // Nằm phía sau Công Chúa
        AddChild(_visual);

        // Các thanh sắt dày dặn và "to bự"
        int barCount = 7;
        for (int i = 0; i < barCount; i++)
        {
            var bar = new ColorRect();
            bar.Color = new Color(0.25f, 0.25f, 0.3f); // Màu kim loại lạnh
            bar.Size = new Vector2(8, 180); // Thanh sắt dày hơn (8px)
            bar.Position = new Vector2(-70 + (i * 140 / (barCount - 1)) - 4, -180);
            bar.ZIndex = 5; // Nằm phía trước Công Chúa để tạo cảm giác bị nhốt
            
            // Thêm viền sáng cho thanh sắt trông khối hơn
            var highlight = new ColorRect();
            highlight.Color = new Color(0.4f, 0.4f, 0.5f);
            highlight.Size = new Vector2(2, 180);
            highlight.Position = new Vector2(0, 0);
            bar.AddChild(highlight);
            
            AddChild(bar);
        }

        // Mái vòm lồng (Trang trí thêm cho uy nghi)
        var dome = new ColorRect();
        dome.Color = new Color(0.2f, 0.2f, 0.25f);
        dome.Size = new Vector2(160, 40);
        dome.Position = new Vector2(-80, -210);
        dome.ZIndex = 6; // Mái lồng cũng nằm phía trước
        AddChild(dome);

        // Vùng tương tác
        _interactArea = new Area2D();
        _interactArea.CollisionLayer = 0;
        _interactArea.CollisionMask = 1;
        _interactArea.BodyEntered += OnPlayerEntered;
        
        var shape = new CollisionShape2D();
        shape.Shape = new CircleShape2D { Radius = 100f };
        _interactArea.AddChild(shape);
        AddChild(_interactArea);

        // Label thông báo (Nâng cấp giao diện)
        var label = new Label();
        label.Name = "Hint";
        label.Text = "CẦN CHÌA KHÓA CỦA CHẰN TINH!";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Position = new Vector2(-150, -250);
        label.Scale = new Vector2(1.2f, 1.2f);
        label.Visible = false;
        AddChild(label);
    }

    private void OnPlayerEntered(Node2D body)
    {
        if (_isOpened) return;

        if (body is Player)
        {
            if (GameManager.Instance.HasBossKey)
            {
                OpenCage();
            }
            else
            {
                GetNode<Label>("Hint").Visible = true;
                var timer = GetTree().CreateTimer(2.0);
                timer.Timeout += () => GetNode<Label>("Hint").Visible = false;
            }
        }
    }

    private void OpenCage()
    {
        _isOpened = true;
        GD.Print("Lồng đã mở! Cứu được Công Chúa!");
        
        // Hiệu ứng mở lồng
        var tween = CreateTween();
        tween.TweenProperty(_visual, "modulate:a", 0.0f, 1.5f);
        tween.TweenCallback(Callable.From(() => {
            // Giải phóng va chạm để cứu công chúa
            GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
            _visual.Visible = false;
        }));
        
        // Xóa các thanh sắt và mái vòm (ColorRect nào ở Z cao hơn)
        foreach (var child in GetChildren())
        {
            if (child is ColorRect cr && cr != _visual)
            {
                var tw = CreateTween();
                // Hiệu ứng các thanh sắt rơi xuống đất
                tw.TweenProperty(cr, "position:y", cr.Position.Y + 200f, 1.2f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(cr, "modulate:a", 0f, 1.0f);
            }
        }
        
        // Thông báo giải cứu thành công
        var successLabel = GetNode<Label>("Hint");
        successLabel.Text = "BẠN ĐÃ GIẢI CỨU CÔNG CHÚA!";
        successLabel.Visible = true;
        
        // Tìm Công chúa trong đấu trường và đổi animation
        var princess = GetParent().GetNodeOrNull<Node2D>("Princess");
        if (princess != null)
        {
            var sprite = princess.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            if (sprite != null && sprite.SpriteFrames.HasAnimation("rescued"))
            {
                sprite.Play("rescued");
            }
        }
    }
}
