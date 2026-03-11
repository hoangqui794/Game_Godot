using Godot;
using System.Collections.Generic;
using System;

public partial class TreasureChest : Area2D
{
    [Export] public bool RequireAllEnemiesDefeated = true;

    private AnimatedSprite2D _animSprite;
    private Label _messageLabel;
    private bool _isOpened = false;
    private Node2D _keyVisual;
    private Node2D _portal;
    private int _popupSlide = 1;
    private CanvasLayer _popupOverlay;
    private Label _popupContentLabel;
    private TextureRect _popupInfographic;
    private Player _currentPlayer;
    private Tween _typewriterTween;
    private bool _isTypewriting = false;

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

        // Ẩn rương ngay từ đầu nếu yêu cầu diệt hết quái (Màn 1)
        if (RequireAllEnemiesDefeated)
        {
            Visible = false;
        }
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
                // Tìm vị trí con quái vừa chết để bay tới đó (Chỉ thực hiện ở Màn 1)
                if (GameManager.Instance.CurrentLevel == 1)
                {
                    var enemies = GetTree().GetNodesInGroup("enemies");
                    foreach (var node in enemies)
                    {
                        if (node is BaseEnemy e && e.IsDead)
                        {
                            GlobalPosition = e.GlobalPosition;
                            GD.Print($"Rương đã dịch chuyển tới vị trí quái chết: {GlobalPosition}");
                            break;
                        }
                    }
                }

                // Hiện rương ra khi quái đã chết hết!
                Visible = true;

                // Tìm lại player để chắc chắn
                Player p = null;
                foreach (var b in GetOverlappingBodies()) if (b is Player target) p = target;
                if (p != null) OpenChest(p);
            }
            else
            {
                // Cập nhật thông báo số quái còn lại để người chơi dễ tìm (Chỉ hiện khi rương đã hiện)
                if (Visible)
                {
                    _messageLabel.Text = $"Còn {aliveCount} quái vật chưa tiêu diệt!";
                    _messageLabel.Visible = true;
                }
            }
        }
        else
        {
            _messageLabel.Visible = false;

            // Một cơ chế đặc biệt: Nếu quái đã chết hết nhưng người chơi ở xa, rương vẫn phải hiện ra để người chơi biết đường mà tới
            if (RequireAllEnemiesDefeated && !Visible)
            {
                var enemies = GetTree().GetNodesInGroup("enemies");
                bool anyAlive = false;
                foreach (var n in enemies) if (n is BaseEnemy e && !e.IsDead) anyAlive = true;

                if (!anyAlive)
                {
                    Visible = true;
                    // Chạy hiệu ứng xuất hiện cho ngầu
                    Modulate = new Color(1, 1, 1, 0);
                    var tw = CreateTween();
                    tw.TweenProperty(this, "modulate:a", 1.0f, 1.0f);
                }
            }
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

        // --- PHẦN THƯỞNG KĨ NĂNG (Icon Rìu Bay đã tách nền) ---
        _keyVisual = new Node2D();
        _keyVisual.Position = new Vector2(0, -20);
        AddChild(_keyVisual);

        var rewardSprite = new Sprite2D();
        // Sử dụng hàm PrepareAxeTexture của Player để lấy texture xịn không nền
        string skillPath = "res://Assets/Sprites/Player/Skill_1.png";
        var tex = GD.Load<Texture2D>(skillPath);

        if (tex != null)
        {
            // Tách nền để icon đẹp hơn (RGB 35, 35, 35 thường là nền đen trong sprite game này)
            Image img = tex.GetImage();
            img.Decompress();
            img.Convert(Image.Format.Rgba8);

            // Xóa nền đen/xám nếu có
            Color bgColor = img.GetPixel(0, 0);
            for (int y = 0; y < img.GetHeight(); y++)
            {
                for (int x = 0; x < img.GetWidth(); x++)
                {
                    Color p = img.GetPixel(x, y);
                    float diff = Mathf.Sqrt(Mathf.Pow(p.R - bgColor.R, 2) + Mathf.Pow(p.G - bgColor.G, 2) + Mathf.Pow(p.B - bgColor.B, 2));
                    if (diff < 0.2f) img.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
            rewardSprite.Texture = ImageTexture.CreateFromImage(img);
            rewardSprite.Scale = new Vector2(0.35f, 0.35f); // Nhỏ gọn và tinh tế hơn
        }
        else
        {
            rewardSprite.Texture = GD.Load<Texture2D>("res://icon.svg");
            rewardSprite.Scale = new Vector2(0.2f, 0.2f);
        }
        _keyVisual.AddChild(rewardSprite);

        // Hiệu ứng hạt bụi lấp lánh quanh Icon (Màu xanh Cyan rực rỡ)
        var keyGlow = new CpuParticles2D();
        keyGlow.Amount = 30;
        keyGlow.Lifetime = 0.6f;
        keyGlow.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
        keyGlow.EmissionSphereRadius = 25f;
        keyGlow.Gravity = new Vector2(0, -30);
        keyGlow.Color = new Color(0f, 1f, 1f, 0.9f);
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

                if (GameManager.Instance.CurrentLevel == 1)
                {
                    // Màn 1: Mở khóa J và K (tổng 2 kỹ năng)
                    GameManager.Instance.UnlockedSkillsCount = 2; 
                    GameManager.Instance.TotalKeys++; // Nhận luôn 1 chìa khóa ở Màn 1

                    // Hiện 3 trang mô tả (Slide): Chìa khóa, Skill J, Skill K
                    ShowRewardPopups(player);

                    GD.Print("Màn 1: Nhận kỹ năng J, K và chìa khóa xong, hiện popup mô tả.");
                }
                else if (GameManager.Instance.CurrentLevel == 2)
                {
                    // Màn 2: Mở khóa kỹ năng thứ 3 (Skill L)
                    GameManager.Instance.UnlockedSkillsCount = 3;
                    GameManager.Instance.TotalKeys++;
                    
                    // Hiện popup mô tả cho kỹ năng L
                    ShowRewardPopups(player);
                    
                    GD.Print("Màn 2: Nhận kỹ năng L và hiện popup mô tả.");
                }
                else
                {
                    // Ở các màn khác (nếu có rương) thì vẫn mở cổng tự động (nếu muốn)
                    GameManager.Instance.TotalKeys++;
                    CreateEpicPortal(player);
                }
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

    private void ShowRewardPopups(Player player)
    {
        _currentPlayer = player;
        _popupSlide = 1;

        _popupOverlay = new CanvasLayer();
        _popupOverlay.Layer = 100;
        GetTree().Root.AddChild(_popupOverlay);

        var bg = new ColorRect();
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.7f); // Nền tối mờ phía sau
        _popupOverlay.AddChild(bg);

        // Khung chính (Giấy cuộn)
        var panel = new ColorRect();
        panel.Size = new Vector2(950, 450);
        panel.Position = new Vector2((1152 - 950) / 2, (648 - 450) / 2 - 20);
        panel.Color = new Color(0.92f, 0.85f, 0.65f); // Tông màu vàng cũ (Parchment)
        _popupOverlay.AddChild(panel);

        // Viền trang trí (Border)
        var border = new ReferenceRect();
        border.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        border.EditorOnly = false;
        border.BorderColor = new Color(0.5f, 0.35f, 0.15f); // Màu nâu đậm cổ điển
        border.BorderWidth = 4f;
        panel.AddChild(border);

        // Góc trang trí (Gợi ý thị giác)
        for (int i = 0; i < 4; i++)
        {
            var corner = new ColorRect();
            corner.Size = new Vector2(20, 20);
            corner.Color = new Color(0.4f, 0.25f, 0.1f);
            if (i == 0) corner.Position = new Vector2(0, 0);
            if (i == 1) corner.Position = new Vector2(930, 0);
            if (i == 2) corner.Position = new Vector2(0, 430);
            if (i == 3) corner.Position = new Vector2(930, 430);
            panel.AddChild(corner);
        }

        _popupContentLabel = new Label();
        _popupContentLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _popupContentLabel.VerticalAlignment = VerticalAlignment.Center;
        _popupContentLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _popupContentLabel.Size = new Vector2(850, 350);
        _popupContentLabel.Position = new Vector2(50, 50);
        _popupContentLabel.AddThemeFontSizeOverride("font_size", 30);
        _popupContentLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.3f));
        _popupContentLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _popupContentLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        panel.AddChild(_popupContentLabel);

        // Thêm TextureRect cho các trang Infographic
        _popupInfographic = new TextureRect();
        _popupInfographic.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _popupInfographic.StretchMode = TextureRect.StretchModeEnum.Scale; // Phóng to vừa khít khung
        _popupInfographic.Size = new Vector2(950, 450);
        _popupInfographic.Position = new Vector2(0, 0);
        _popupInfographic.Visible = false;
        panel.AddChild(_popupInfographic);

        var prompt = new Label();
        prompt.Text = "--- [ Nhấn phím bất kỳ hoặc Click để tiếp tục ] ---";
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        prompt.Size = new Vector2(950, 40);
        prompt.Position = new Vector2(0, 400);
        prompt.AddThemeFontSizeOverride("font_size", 18);
        prompt.AddThemeColorOverride("font_color", new Color(0.3f, 0.2f, 0.1f));
        panel.AddChild(prompt);

        // Hoạt ảnh xuất hiện (Fade & Scale)
        panel.Scale = new Vector2(0.5f, 0.5f);
        panel.PivotOffset = panel.Size / 2;
        panel.Modulate = new Color(1, 1, 1, 0);
        var entryTween = CreateTween().SetParallel(true);
        entryTween.TweenProperty(panel, "scale", new Vector2(1, 1), 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        entryTween.TweenProperty(panel, "modulate:a", 1.0f, 0.3f);

        UpdatePopupText();

        var timer = GetTree().CreateTimer(0.6);
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(_popupOverlay)) return;

            var listener = new Control();
            listener.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _popupOverlay.AddChild(listener);
            listener.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed) OnNextSlide();
            };

            var keyHandler = new Node();
            keyHandler.SetScript(GD.Load<Script>("res://Scripts/NPCs/PopupInputHelper.cs") ?? null);
            _popupOverlay.AddChild(keyHandler);
        };
    }

    private void UpdatePopupText()
    {
        if (GameManager.Instance.CurrentLevel == 1)
        {
            if (_popupSlide == 1)
            {
                _popupContentLabel.Visible = true;
                _popupInfographic.Visible = false;

                string storyText = "CHÌA KHÓA VÀNG ĐÃ ĐƯỢC TÌM THẤY!\n\nChiếc chìa khóa này tỏa ra ánh sáng yếu ớt, như đang dẫn lối.\nCánh cổng đá cổ phía trước dường như cần 3 chìa khóa để mở.\n\n[ Bạn đã có: 1 / 3 ]\nHãy tiếp tục tìm 2 chiếc còn lại trong hang động.";
                _popupContentLabel.Text = storyText;
                _popupContentLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.15f, 0.05f));

                RunTypewriter(storyText.Length);
            }
            else if (_popupSlide == 2)
            {
                ShowInfographic("res://Assets/UI/Skill_J_Info.jpg");
            }
            else if (_popupSlide == 3)
            {
                ShowInfographic("res://Assets/UI/Skill_K_Info.jpg");
            }
        }
        else if (GameManager.Instance.CurrentLevel == 2)
        {
            if (_popupSlide == 1)
            {
                ShowInfographic("res://Assets/UI/Skill_L_Info.jpg");
            }
        }
    }

    private void ShowInfographic(string path)
    {
        _popupContentLabel.Visible = false;
        _popupInfographic.Visible = true;

        var tex = GD.Load<Texture2D>(path);
        // Fallback sang .png nếu không có .jpg
        if (tex == null) tex = GD.Load<Texture2D>(path.Replace(".jpg", ".png"));

        if (tex != null)
        {
            _popupInfographic.Texture = tex;
        }
        else
        {
            _popupContentLabel.Visible = true;
            _popupContentLabel.Text = $"KỸ NĂNG MỚI\n\n[ Không tìm thấy ảnh tại {path} ]";
            _popupContentLabel.AddThemeColorOverride("font_color", Colors.Red);
        }
    }

    private void RunTypewriter(int length)
    {
        if (_typewriterTween != null) _typewriterTween.Kill();

        _popupContentLabel.VisibleCharacters = 0;
        _isTypewriting = true;

        _typewriterTween = CreateTween();
        _typewriterTween.TweenProperty(_popupContentLabel, "visible_characters", length, length * 0.05f) // Tốc độ 0.05s/chữ
            .SetTrans(Tween.TransitionType.Linear);

        _typewriterTween.Finished += () => _isTypewriting = false;
    }

    public void OnNextSlide()
    {
        // Nếu đang đánh máy mà nhấn phím thì hiện hết chữ luôn
        if (_isTypewriting)
        {
            _isTypewriting = false;
            if (_typewriterTween != null) _typewriterTween.Kill();
            _popupContentLabel.VisibleCharacters = -1; // Hiện toàn bộ
            return;
        }

        int maxSlides = 0;
        if (GameManager.Instance.CurrentLevel == 1) maxSlides = 3;
        else if (GameManager.Instance.CurrentLevel == 2) maxSlides = 1;

        _popupSlide++;
        if (_popupSlide <= maxSlides)
        {
            UpdatePopupText();
        }
        else
        {
            if (_popupOverlay != null)
            {
                _popupOverlay.QueueFree();
                _popupOverlay = null;
            }
            
            // Xong popup thì mới mở cổng (nếu là Màn 2 hoặc Màn 1 đã xong việc)
            if (_currentPlayer != null)
            {
                _currentPlayer.Call("RefreshSkillUI");
                
                // Sau khi xem xong Skill L ở Màn 2, mở cổng
                if (GameManager.Instance.CurrentLevel == 2)
                {
                    CreateEpicPortal(_currentPlayer);
                }
                // Ở Màn 1, nếu muốn mở cổng ngay sau popup thì thêm ở đây
                // Nhưng hiện tại Màn 1 yêu cầu 3 chìa nên có thể cổng đã có sẵn hoặc chờ logic khác
                else if (GameManager.Instance.CurrentLevel == 1)
                {
                    // Có thể gọi hàm để player biết đường đi tiếp
                }
            }
        }
    }
}
