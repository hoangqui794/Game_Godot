using Godot;
using System.Collections.Generic;
using System;

public partial class TreasureChest : Area2D
{
    [Export] public bool RequireAllEnemiesDefeated = true;

    private AnimatedSprite2D _animSprite;
    private Label _messageLabel;
    private bool _isOpened = false;

    // Các thành phần đồ họa để làm hiệu ứng
    private Node2D _portal;
    private Node2D _keyVisual;

    public override void _Ready()
    {
        _animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        if (_animSprite.SpriteFrames == null)
        {
            CreatePlaceholderSprites();
        }

        _messageLabel = new Label();
        _messageLabel.Text = "Hãy đánh bại hết quái vật để mở Rương!";
        _messageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _messageLabel.Position = new Vector2(-120, -70);
        _messageLabel.Visible = false;
        _messageLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        _messageLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_messageLabel);

        BodyEntered += OnBodyEntered;
        _animSprite.Play("idle"); // Lão Hạc => Rương đóng
    }

    private void CreatePlaceholderSprites()
    {
        // Load hình ảnh cực xịn từ thư mục
        Texture2D chestClosed = GD.Load<Texture2D>("res://Assets/Sprites/Environment/treasure_chest_closed.png");
        Texture2D chestOpened = GD.Load<Texture2D>("res://Assets/Sprites/Environment/treasure_chest_open.png");

        if (chestClosed != null && chestOpened != null)
        {
            var animations = new Dictionary<string, Texture2D[]>
            {
                { "idle", new Texture2D[] { chestClosed } },
                { "rescued", new Texture2D[] { chestOpened } }
            };
            _animSprite.SpriteFrames = SpriteHelper.BuildSpriteFrames(animations);

            // Chỉnh Scale nhỏ lại vì hình tải trên mạng độ phân giải cao, tăng lên theo yêu cầu
            _animSprite.Scale = new Vector2(0.12f, 0.12f);
            _animSprite.Position = new Godot.Vector2(0, -40); // Đẩy nhẹ lên trên vì rương to ra
        }
        else
        {
            // Fallback nếu ảnh lỗi
            var chestC = SpriteHelper.CreateColoredRect(40, 30, new Color(0.6f, 0.4f, 0.1f));
            var chestO = SpriteHelper.CreateColoredRect(40, 30, new Color(0.9f, 0.8f, 0.2f));

            var animations = new Dictionary<string, Texture2D[]>
            {
                { "idle", new Texture2D[] { chestC } },
                { "rescued", new Texture2D[] { chestO } }
            };
            _animSprite.SpriteFrames = SpriteHelper.BuildSpriteFrames(animations);
        }

        _animSprite.Play("idle");
    }

    public override void _Process(double delta)
    {
        if (_isOpened) return;
        if (!RequireAllEnemiesDefeated) return;

        // Nếu người chơi đang đứng ở Rương, cập nhật kiểm tra liên tục để mở ngay khi quái cuối chết
        bool isPlayerInside = false;
        foreach (var body in GetOverlappingBodies())
        {
            if (body is Player)
            {
                isPlayerInside = true;
                break;
            }
        }

        if (isPlayerInside)
        {
            var allEnemiesInGroup = GetTree().GetNodesInGroup("enemies");
            int aliveCount = 0;

            foreach (var node in allEnemiesInGroup)
            {
                if (node is BaseEnemy enemy && !enemy.IsDead)
                {
                    aliveCount++;
                }
            }

            if (aliveCount == 0)
            {
                // Tìm lại player để chắc chắn
                Player p = null;
                foreach (var b in GetOverlappingBodies()) if (b is Player target) p = target;
                if (p != null) OpenChest(p);
            }
            else
            {
                // Cập nhật thông báo số quái còn lại để người chơi dễ tìm
                _messageLabel.Text = $"Còn {aliveCount} quái vật chưa tiêu diệt!";
                _messageLabel.Visible = true;

                // Debug print (chỉ in mỗi vài giây để admin check log)
                if (GD.Randi() % 120 == 0)
                {
                    GD.Print($"[DEBUG] TreasureChest needs {aliveCount} more enemies dead.");
                    foreach (var n in allEnemiesInGroup)
                    {
                        if (n is BaseEnemy e && !e.IsDead) GD.Print($" - {e.Name} at {e.GlobalPosition}");
                    }
                }
            }
        }
        else
        {
            _messageLabel.Visible = false;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        // Logic bây giờ chủ yếu xử lý ở _Process để mượt mà hơn
        if (_isOpened) return;
        if (body is Player player)
        {
            // Trigger check ngay lập tức khi vừa chạm
            _Process(0);
        }
    }

    private void OpenChest(Player player)
    {
        if (_isOpened) return;
        _isOpened = true;
        _messageLabel.Visible = false;

        // Hiệu ứng rung lắc rương dữ dội trước khi mở
        var shakeTw = CreateTween();
        for (int i = 0; i < 5; i++)
        {
            shakeTw.TweenProperty(_animSprite, "position", new Godot.Vector2(5, 0), 0.05f);
            shakeTw.TweenProperty(_animSprite, "position", new Godot.Vector2(-5, 0), 0.05f);
        }
        shakeTw.TweenProperty(_animSprite, "position", new Godot.Vector2(0, 0), 0.05f);

        // Lóe sáng chói lóa rồi chuyển sang frame Mở
        shakeTw.TweenProperty(_animSprite, "modulate", new Godot.Color(5f, 5f, 5f, 1f), 0.1f);
        shakeTw.TweenCallback(Godot.Callable.From(() =>
        {
            _animSprite.Play("rescued");
            _animSprite.Modulate = Godot.Colors.White;

            GameManager.Instance.AddScore(500);

            // Bùng nổ hạt bụi vàng
            var chestParticles = new Godot.CpuParticles2D();
            chestParticles.Position = new Godot.Vector2(0, -15);
            chestParticles.Amount = 50;
            chestParticles.Lifetime = 1.0f;
            chestParticles.OneShot = true;
            chestParticles.Explosiveness = 0.9f;
            chestParticles.EmissionShape = Godot.CpuParticles2D.EmissionShapeEnum.Rectangle;
            chestParticles.EmissionRectExtents = new Godot.Vector2(20, 10);
            chestParticles.Direction = new Godot.Vector2(0, -1);
            chestParticles.Gravity = new Godot.Vector2(0, 150);
            chestParticles.InitialVelocityMin = 80f;
            chestParticles.InitialVelocityMax = 150f;
            chestParticles.Color = Godot.Colors.Gold;
            AddChild(chestParticles);
            chestParticles.Emitting = true;
        }));

        // 1. Sinh ra Chìa khóa vàng lấp lánh (Tương tự hình ảnh xịn)
        _keyVisual = new Node2D();
        _keyVisual.Position = new Vector2(0, -20);
        AddChild(_keyVisual);

        // Hình dạng bề ngoài của chìa
        var keyRect = new ColorRect();
        keyRect.Color = Colors.Yellow;
        keyRect.Size = new Vector2(8, 20);
        keyRect.Position = new Vector2(-4, -10);
        _keyVisual.AddChild(keyRect);

        var keyHead = new ColorRect();
        keyHead.Color = Colors.Yellow;
        keyHead.Size = new Vector2(16, 10);
        keyHead.Position = new Vector2(-8, -14);
        _keyVisual.AddChild(keyHead);

        // Chìa khóa phát sáng (Particles)
        var keyGlow = new CpuParticles2D();
        keyGlow.Amount = 15;
        keyGlow.Lifetime = 0.5f;
        keyGlow.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
        keyGlow.EmissionSphereRadius = 15f;
        keyGlow.Gravity = new Vector2(0, -20);
        keyGlow.Color = new Color(1f, 1f, 0.5f, 0.8f);
        _keyVisual.AddChild(keyGlow);

        var tween = CreateTween();
        // Nhảy lên mượt mà và xoay vòng
        tween.SetParallel(true);
        tween.TweenProperty(_keyVisual, "position:y", -80f, 0.6f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_keyVisual, "rotation", Mathf.Pi * 4, 0.6f); // Xoay vòng

        // Hết nảy lên -> bay vào người nhân vật
        var seqTween = CreateTween();
        seqTween.TweenInterval(0.6f);
        seqTween.TweenCallback(Callable.From(() =>
        {
            var flyTween = CreateTween();
            flyTween.SetParallel(true);
            flyTween.TweenProperty(_keyVisual, "global_position", player.GlobalPosition, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            flyTween.TweenProperty(_keyVisual, "scale", new Vector2(0.2f, 0.2f), 0.4f);

            flyTween.Chain().TweenCallback(Callable.From(() =>
            {
                _keyVisual.QueueFree();
                GameManager.Instance.TotalKeys++;
                GD.Print("Đã nhặt 1 chìa khóa! Tổng chìa: " + GameManager.Instance.TotalKeys);

                // Mở cổng vĩ đại
                CreateEpicPortal(player);
            }));
        }));
    }

    private void CreateEpicPortal(Player player)
    {
        _portal = new Godot.Node2D();
        _portal.Position = new Godot.Vector2(60, -50); // Mở bên phải rương
        AddChild(_portal);

        // Vòng xoáy ma thuật rực rỡ sử dụng Shader xịn mình vừa code
        var vortex = new Godot.ColorRect();
        vortex.Size = new Godot.Vector2(120, 180);
        vortex.Position = new Godot.Vector2(-60, -90);
        vortex.PivotOffset = new Godot.Vector2(60, 90);

        // Load và Áp dụng Shader
        var shader = Godot.GD.Load<Godot.Shader>("res://Assets/Shaders/epic_portal.gdshader");
        if (shader != null)
        {
            var mat = new Godot.ShaderMaterial();
            mat.Shader = shader;
            // Phối màu tím huyền ảo và xanh lóa
            mat.SetShaderParameter("portal_color_1", new Godot.Color(0.4f, 0.0f, 0.8f, 1.0f));
            mat.SetShaderParameter("portal_color_2", new Godot.Color(0.0f, 0.8f, 1.0f, 1.0f));
            vortex.Material = mat;
        }
        else
        {
            vortex.Color = new Godot.Color(0.4f, 0.1f, 0.9f, 0f); // Xấu xí fallback
        }

        vortex.Modulate = new Godot.Color(1, 1, 1, 0); // Ban đầu tàng hình
        _portal.AddChild(vortex);

        // Lực hút từ tâm (sucking particles)
        var portalParticles = new Godot.CpuParticles2D();
        portalParticles.Amount = 80;
        portalParticles.Lifetime = 1.2f;
        portalParticles.EmissionShape = Godot.CpuParticles2D.EmissionShapeEnum.Sphere;
        portalParticles.EmissionSphereRadius = 100f;
        portalParticles.Gravity = new Godot.Vector2(0, 0);
        portalParticles.RadialAccelMin = -250f; // Hút cực mạnh vào lõi
        portalParticles.RadialAccelMax = -150f;
        portalParticles.ScaleAmountMin = 2f;
        portalParticles.ScaleAmountMax = 5f;
        portalParticles.Color = new Godot.Color(0.8f, 0.3f, 1f, 0.8f);
        _portal.AddChild(portalParticles);

        // Sao bay lên
        var starParticles = new Godot.CpuParticles2D();
        starParticles.Amount = 30;
        starParticles.Lifetime = 2.0f;
        starParticles.EmissionShape = Godot.CpuParticles2D.EmissionShapeEnum.Point;
        starParticles.Gravity = new Godot.Vector2(0, -60);
        starParticles.InitialVelocityMin = 50f;
        starParticles.InitialVelocityMax = 120f;
        starParticles.ScaleAmountMin = 1.5f;
        starParticles.ScaleAmountMax = 3f;
        starParticles.Color = Godot.Colors.Cyan;
        _portal.AddChild(starParticles);

        var portalTween = CreateTween().SetParallel(true);
        // Fade in cổng từ từ
        portalTween.TweenProperty(vortex, "modulate:a", 1.0f, 1.5f).SetTrans(Tween.TransitionType.Cubic);
        // Cổng nở bật ra
        vortex.Scale = new Godot.Vector2(0.1f, 0.1f);
        portalTween.TweenProperty(vortex, "scale", new Godot.Vector2(1.2f, 1.2f), 1.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        // Vòng xoáy nhấp nhô tuần hoàn sau khi mở xong
        var pulseTween = CreateTween().SetLoops();
        pulseTween.TweenInterval(1.5f);
        pulseTween.TweenProperty(vortex, "scale", new Godot.Vector2(1.3f, 1.3f), 0.8f);
        pulseTween.TweenProperty(vortex, "scale", new Godot.Vector2(1.1f, 1.1f), 0.8f);

        // Ép Nhân Vật tự đi vào cổng
        var sequence = CreateTween();
        sequence.TweenInterval(2.5f); // Đợi cổng mở full sức mạnh
        sequence.TweenCallback(Godot.Callable.From(() =>
        {
            player.WalkIntoCave(1.5f); // Đi bộ về phía cổng

            // Ép Thạch Sanh mờ dần và thu nhỏ lại (Bị hút vào không gian khác)
            var hútTween = CreateTween().SetParallel(true);
            hútTween.TweenProperty(player, "scale", new Godot.Vector2(0.2f, 0.2f), 1.2f).SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.In);
            hútTween.TweenProperty(player, "modulate:a", 0.0f, 1.2f);
        }));

        sequence.TweenInterval(2.0f);
        sequence.TweenCallback(Godot.Callable.From(() =>
        {
            GameManager.Instance.NextLevel();
        }));
    }
}
