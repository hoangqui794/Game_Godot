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
    private ColorRect _popupSkillCard;
    private TextureRect _popupSkillIcon;
    private ReferenceRect _popupSkillBorder;
    private ColorRect _popupSkillGlowBar;
    private ColorRect _popupSkillAccentLine;
    private ColorRect _popupSkillIconFrame;
    private Label _popupSkillTag;
    private CpuParticles2D _popupSkillParticles;
    private ColorRect _popupHotkeyPill;
    private Label _popupHotkeyPillLabel;
    private Label _popupSkillTitle;
    private Label _popupSkillHotkey;
    private Label _popupSkillBody;
    private Label _popupSkillCooldown;
    private Player _currentPlayer;
    private Tween _typewriterTween;
    private Tween _skillCardPulseTween;
    private Tween _portalPulseTween;
    private bool _isTypewriting = false;

    private readonly struct SkillCardData
    {
        public readonly string Title;
        public readonly string Hotkey;
        public readonly string Body;
        public readonly string Cooldown;
        public readonly string IconPath;
        public readonly Color Accent;

        public SkillCardData(string title, string hotkey, string body, string cooldown, string iconPath, Color accent)
        {
            Title = title;
            Hotkey = hotkey;
            Body = body;
            Cooldown = cooldown;
            IconPath = iconPath;
            Accent = accent;
        }
    }

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

    public override void _ExitTree()
    {
        if (_typewriterTween != null) _typewriterTween.Kill();
        if (_skillCardPulseTween != null) _skillCardPulseTween.Kill();
        if (_portalPulseTween != null) _portalPulseTween.Kill();
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
        // Sử dụng icon Chìa Khóa Vàng mới tạo
        string skillPath = "res://Assets/Sprites/Environment/gold_key.png";
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
                    PlayDialogueAndShowPopups(player, 1);

                    GD.Print("Màn 1: Nhận kỹ năng J, K và chìa khóa xong, hiện popup mô tả.");
                }
                else if (GameManager.Instance.CurrentLevel == 2)
                {
                    // Màn 2: Mở khóa kỹ năng thứ 3 (Skill L)
                    GameManager.Instance.UnlockedSkillsCount = 3;
                    GameManager.Instance.TotalKeys++;

                    // Hiện popup mô tả cho kỹ năng L
                    PlayDialogueAndShowPopups(player, 2);

                    GD.Print("Màn 2: Nhận kỹ năng L và hiện popup mô tả.");
                }
                else
                {
                    // Ở các màn khác (nếu có rương) thì vẫn mở cổng tự động (nếu muốn)
                    GameManager.Instance.TotalKeys++;
                    PlayDialogueAndShowPopups(player, 3);
                }
            }));
        }));
    }

    private async void PlayDialogueAndShowPopups(Player player, int level)
    {
        var dm = new DialogueManager();
        AddChild(dm);
        var lines = new List<DialogueManager.DialogueLine>();

        if (level == 1)
        {
            lines.Add(new DialogueManager.DialogueLine("Ngọc Hoàng", "Thạch Sanh! Ta đang dõi theo hành trình của ngươi. Ngươi đã vượt qua rừng thiêng bằng ý chí, không hề nản lòng dù hiểm nguy. Rìu thần của ngươi xứng đáng được thức tỉnh! \"Hãy nhận lấy hai Binh Pháp đầu tiên, hãy dùng chúng bảo vệ lẽ phải trên con đường phía trước! “", null, "res://Assets/Audio/Voices/god_m1_reward.mp3"));
            lines.Add(new DialogueManager.DialogueLine("Thạch Sanh", "Hai Binh pháp từ Thiên Đình sẽ giúp ta tiêu diệt cái ác. Ta sẽ không phụ lòng mọi người!", null, "res://Assets/Audio/Voices/ts_m1_god2.mp3"));
            lines.Add(new DialogueManager.DialogueLine("Ngọc Hoàng", "Hãy nhớ cho kỹ, Thạch Sanh. Sức mạnh không phải để phô trương — mà để che chở kẻ yếu và trừng trị yêu tà. Càng vào sâu, lòng ngươi càng phải vững hơn rìu trong tay.", null, "res://Assets/Audio/Voices/god_m1_advise.mp3"));
            lines.Add(new DialogueManager.DialogueLine("Thạch Sanh", "Con xin ghi nhớ. Rìu này chỉ vung vì lẽ phải, không vì tư thù…", null, "res://Assets/Audio/Voices/ts_m1_god3.mp3"));
        }
        else if (level == 2)
        {
            lines.Add(new DialogueManager.DialogueLine("Thạch Sanh", "Lại là rương này… Ngọc Hoàng còn ban thêm sức mạnh cho ta?.", null, "res://Assets/Audio/Voices/ts_m2_god1.mp3"));
            lines.Add(new DialogueManager.DialogueLine("Ngọc Hoàng", "Thạch Sanh! Ngươi vừa một mình đương đầu với cả bầy rắn lẫn bầy đại bàng. Trời đất cảm phục sự kiên cường đó. Nhưng phía trước còn một thử thách lớn hơn đang chờ ngươi. Hãy nhận lấy binh pháp cuối cùng này, ta ban cho ngươi để hạ con yêu quái trong kia!", null, "res://Assets/Audio/Voices/god_m2_reward.mp3"));
            lines.Add(new DialogueManager.DialogueLine("Thạch Sanh", "Ta đã nhận được sức mạnh khổng lồ. Chằn tinh, ngươi chịu thua đi.... .", null, "res://Assets/Audio/Voices/ts_m2_god2.mp3"));
            lines.Add(new DialogueManager.DialogueLine("Thạch Sanh", "Con đã hiểu. Con sẽ phá cổng tối, diệt yêu tà, rồi đưa công chúa trở về.", null, "res://Assets/Audio/Voices/ts_m2_god3.mp3"));
            lines.Add(new DialogueManager.DialogueLine("Thạch Sanh", "Bầy rắn, bầy đại bàng, tất cả đã bị hạ. Hình như có Tiếng gầm từ phía trong… phải chăng là tiếng của Chằn tinh. Nhưng ta đã có đủ sức mạnh rồi, tiến lên thôi.", null, "res://Assets/Audio/Voices/ts_m2_end.mp3"));
        }

        if (lines.Count > 0)
        {
            await dm.PlayDialogue(lines);
        }
        
        if (level == 1 || level == 2)
        {
            ShowRewardPopups(player);
        }
        else
        {
            CreateEpicPortal(player);
        }
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
        if (_portalPulseTween != null) _portalPulseTween.Kill();
        _portalPulseTween = CreateTween().SetLoops();
        _portalPulseTween.TweenInterval(1.5f);
        _portalPulseTween.TweenProperty(vortex, "scale", new Godot.Vector2(1.3f, 1.3f), 0.8f);
        _portalPulseTween.TweenProperty(vortex, "scale", new Godot.Vector2(1.1f, 1.1f), 0.8f);

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
            if (_portalPulseTween != null) _portalPulseTween.Kill();
            GameManager.Instance.NextLevel();
        }));
    }

    private void ShowRewardPopups(Player player)
    {
        // guard: if chest is already disposed or player invalid, skip popup
        if (!IsInstanceValid(this) || player == null || !IsInstanceValid(player)) return;

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

        BuildSkillCard(panel);

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

        // immediately add input listeners so player can press keys or click at any moment
        var keyHandler = new PopupInputHelper();
        keyHandler.Target = this;
        _popupOverlay.AddChild(keyHandler);
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
                ShowSkillCard("J");
            }
            else if (_popupSlide == 3)
            {
                ShowSkillCard("K");
            }
        }
        else if (GameManager.Instance.CurrentLevel == 2)
        {
            if (_popupSlide == 1)
            {
                ShowSkillCard("L");
            }
        }
    }

    private void BuildSkillCard(ColorRect panel)
    {
        _popupSkillCard = new ColorRect();
        _popupSkillCard.Visible = false;
        _popupSkillCard.Size = new Vector2(860, 320);
        _popupSkillCard.Position = new Vector2(45, 55);
        _popupSkillCard.Color = new Color(0.02f, 0.05f, 0.09f, 0.97f);
        panel.AddChild(_popupSkillCard);

        var bgDepth = new ColorRect();
        bgDepth.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bgDepth.Color = new Color(0.01f, 0.02f, 0.05f, 0.45f);
        _popupSkillCard.AddChild(bgDepth);

        _popupSkillGlowBar = new ColorRect();
        _popupSkillGlowBar.Size = new Vector2(860, 58);
        _popupSkillGlowBar.Color = new Color(0.18f, 0.8f, 1f, 0.16f);
        _popupSkillCard.AddChild(_popupSkillGlowBar);

        _popupSkillAccentLine = new ColorRect();
        _popupSkillAccentLine.Position = new Vector2(14, 20);
        _popupSkillAccentLine.Size = new Vector2(6, 280);
        _popupSkillAccentLine.Color = new Color(0.3f, 0.95f, 1f, 0.9f);
        _popupSkillCard.AddChild(_popupSkillAccentLine);

        _popupSkillBorder = new ReferenceRect();
        _popupSkillBorder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _popupSkillBorder.BorderWidth = 3f;
        _popupSkillBorder.BorderColor = new Color(0.3f, 0.95f, 1f, 0.95f);
        _popupSkillBorder.EditorOnly = false;
        _popupSkillCard.AddChild(_popupSkillBorder);

        _popupSkillIconFrame = new ColorRect();
        _popupSkillIconFrame.Position = new Vector2(30, 78);
        _popupSkillIconFrame.Size = new Vector2(188, 188);
        _popupSkillIconFrame.Color = new Color(0.08f, 0.15f, 0.2f, 0.9f);
        _popupSkillCard.AddChild(_popupSkillIconFrame);

        var iconFrameBorder = new ReferenceRect();
        iconFrameBorder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        iconFrameBorder.BorderWidth = 2.5f;
        iconFrameBorder.BorderColor = new Color(0.4f, 0.95f, 1f, 0.95f);
        iconFrameBorder.EditorOnly = false;
        _popupSkillIconFrame.AddChild(iconFrameBorder);

        _popupSkillIcon = new TextureRect();
        _popupSkillIcon.Position = new Vector2(40, 88);
        _popupSkillIcon.Size = new Vector2(168, 168);
        _popupSkillIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _popupSkillIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _popupSkillIcon.TextureFilter = Control.TextureFilterEnum.Linear;
        _popupSkillCard.AddChild(_popupSkillIcon);

        // subtle particle emitter around the icon (one-shot burst)
        _popupSkillParticles = new CpuParticles2D();
        _popupSkillParticles.Amount = 20;
        _popupSkillParticles.Lifetime = 0.8f;
        _popupSkillParticles.OneShot = true;
        _popupSkillParticles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
        _popupSkillParticles.EmissionSphereRadius = 40f;
        _popupSkillParticles.InitialVelocityMin = 18f;
        _popupSkillParticles.InitialVelocityMax = 60f;
        _popupSkillParticles.ScaleAmountMin = 0.6f;
        _popupSkillParticles.ScaleAmountMax = 1.2f;
        _popupSkillParticles.Color = new Color(0.6f, 0.95f, 1f, 0.95f);
        _popupSkillParticles.Position = new Vector2(124, 172);
        _popupSkillCard.AddChild(_popupSkillParticles);

        // hotkey pill badge (right side)
        _popupHotkeyPill = new ColorRect();
        _popupHotkeyPill.Position = new Vector2(768, 36);
        _popupHotkeyPill.Size = new Vector2(110, 44);
        _popupHotkeyPill.Color = new Color(0.06f, 0.12f, 0.14f, 0.95f);
        _popupSkillCard.AddChild(_popupHotkeyPill);

        _popupHotkeyPillLabel = new Label();
        _popupHotkeyPillLabel.Position = new Vector2(768, 36);
        _popupHotkeyPillLabel.Size = new Vector2(110, 44);
        _popupHotkeyPillLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _popupHotkeyPillLabel.VerticalAlignment = VerticalAlignment.Center;
        _popupHotkeyPillLabel.AddThemeFontSizeOverride("font_size", 18);
        _popupHotkeyPillLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.96f, 1f));
        _popupSkillCard.AddChild(_popupHotkeyPillLabel);

        _popupSkillTag = new Label();
        _popupSkillTag.Position = new Vector2(240, 10);
        _popupSkillTag.Size = new Vector2(250, 26);
        _popupSkillTag.Text = "MỞ KHÓA KỸ NĂNG";
        _popupSkillTag.AddThemeFontSizeOverride("font_size", 16);
        _popupSkillTag.AddThemeColorOverride("font_color", new Color(0.46f, 0.95f, 1f, 1f));
        _popupSkillTag.AddThemeConstantOverride("outline_size", 2);
        _popupSkillTag.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.04f, 0.07f, 1f));
        _popupSkillCard.AddChild(_popupSkillTag);

        _popupSkillTitle = new Label();
        _popupSkillTitle.Position = new Vector2(240, 34);
        _popupSkillTitle.Size = new Vector2(590, 48);
        _popupSkillTitle.AddThemeFontSizeOverride("font_size", 36);
        _popupSkillTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.97f, 1f, 1f));
        _popupSkillTitle.AddThemeConstantOverride("outline_size", 3);
        _popupSkillTitle.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.04f, 0.07f, 1f));
        _popupSkillCard.AddChild(_popupSkillTitle);

        _popupSkillHotkey = new Label();
        _popupSkillHotkey.Position = new Vector2(240, 84);
        _popupSkillHotkey.Size = new Vector2(590, 32);
        _popupSkillHotkey.AddThemeFontSizeOverride("font_size", 26);
        _popupSkillHotkey.AddThemeColorOverride("font_color", new Color(0.5f, 0.95f, 1f, 1f));
        _popupSkillHotkey.AddThemeConstantOverride("outline_size", 2);
        _popupSkillHotkey.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.04f, 0.07f, 1f));
        _popupSkillCard.AddChild(_popupSkillHotkey);

        _popupSkillBody = new Label();
        _popupSkillBody.Position = new Vector2(240, 126);
        _popupSkillBody.Size = new Vector2(590, 120);
        _popupSkillBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _popupSkillBody.AddThemeFontSizeOverride("font_size", 22);
        _popupSkillBody.AddThemeColorOverride("font_color", new Color(0.86f, 0.93f, 1f, 1f));
        _popupSkillBody.AddThemeConstantOverride("line_spacing", 5);
        _popupSkillCard.AddChild(_popupSkillBody);

        _popupSkillCooldown = new Label();
        _popupSkillCooldown.Position = new Vector2(240, 258);
        _popupSkillCooldown.Size = new Vector2(590, 36);
        _popupSkillCooldown.AddThemeFontSizeOverride("font_size", 25);
        _popupSkillCooldown.AddThemeColorOverride("font_color", new Color(1f, 0.84f, 0.25f, 1f));
        _popupSkillCooldown.AddThemeConstantOverride("outline_size", 2);
        _popupSkillCooldown.AddThemeColorOverride("font_outline_color", new Color(0.2f, 0.1f, 0.02f, 1f));
        _popupSkillCard.AddChild(_popupSkillCooldown);
    }

    private void ShowSkillCard(string skillCode)
    {
        _isTypewriting = false;
        if (_typewriterTween != null) _typewriterTween.Kill();

        _popupContentLabel.Visible = false;
        _popupInfographic.Visible = false;
        _popupSkillCard.Visible = true;

        SkillCardData data = GetSkillCardData(skillCode);
        _popupSkillTitle.Text = data.Title;
        _popupSkillHotkey.Text = data.Hotkey;
        _popupSkillBody.Text = data.Body;
        _popupSkillCooldown.Text = data.Cooldown;

        Texture2D iconTex = BuildCleanIconTexture(data.IconPath);
        if (iconTex == null) iconTex = GD.Load<Texture2D>("res://icon.svg");
        _popupSkillIcon.Texture = iconTex;

        _popupSkillCard.Color = new Color(data.Accent.R * 0.10f, data.Accent.G * 0.10f, data.Accent.B * 0.16f, 0.97f);
        _popupSkillBorder.BorderColor = new Color(data.Accent.R, data.Accent.G, data.Accent.B, 0.95f);
        _popupSkillGlowBar.Color = new Color(data.Accent.R, data.Accent.G, data.Accent.B, 0.16f);
        _popupSkillAccentLine.Color = new Color(data.Accent.R, data.Accent.G, data.Accent.B, 0.95f);
        _popupSkillTag.AddThemeColorOverride("font_color", new Color(data.Accent.R, data.Accent.G, data.Accent.B, 1f));

        _popupSkillCard.Modulate = new Color(1, 1, 1, 0f);
        _popupSkillCard.Scale = new Vector2(0.97f, 0.97f);
        _popupSkillCard.PivotOffset = _popupSkillCard.Size / 2f;

        var appearTween = CreateTween().SetParallel(true);
        appearTween.TweenProperty(_popupSkillCard, "modulate:a", 1f, 0.22f);
        appearTween.TweenProperty(_popupSkillCard, "scale", new Vector2(1f, 1f), 0.26f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        if (_skillCardPulseTween != null) _skillCardPulseTween.Kill();
        _skillCardPulseTween = CreateTween().SetLoops();
        _skillCardPulseTween.TweenProperty(_popupSkillGlowBar, "modulate:a", 1f, 0.8f);
        _skillCardPulseTween.TweenProperty(_popupSkillGlowBar, "modulate:a", 0.55f, 0.8f);

        // Update hotkey pill text and color
        if (_popupHotkeyPill != null && _popupHotkeyPillLabel != null)
        {
            _popupHotkeyPillLabel.Text = data.Hotkey.Replace("Bấm ", "");
            _popupHotkeyPill.Color = new Color(data.Accent.R * 0.12f, data.Accent.G * 0.12f, data.Accent.B * 0.12f, 0.98f);
            _popupHotkeyPillLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.98f, 1f));
        }

        // Border flash for cinematic feel
        if (IsInstanceValid(_popupSkillBorder))
        {
            var flashTween = CreateTween();
            var original = _popupSkillBorder.BorderColor;
            flashTween.TweenProperty(_popupSkillBorder, "border_color", Colors.White, 0.08f);
            flashTween.TweenCallback(Callable.From(() =>
            {
                if (!IsInstanceValid(this) || !IsInstanceValid(_popupSkillBorder)) return;
                var back = CreateTween();
                back.TweenProperty(_popupSkillBorder, "border_color", original, 0.18f);
            }));
        }

        // Emit particles matching accent
        if (_popupSkillParticles != null)
        {
            _popupSkillParticles.Color = new Color(data.Accent.R, data.Accent.G, data.Accent.B, 0.95f);
            _popupSkillParticles.Emitting = true;
        }

        // initial state for hotkey pill
        _popupHotkeyPill.Modulate = new Color(1, 1, 1, 0.95f);
    }

    private SkillCardData GetSkillCardData(string skillCode)
    {
        return skillCode switch
        {
            "J" => new SkillCardData(
                "BƯỚC 3 • CHIÊU J: RÌU BAY",
                "Bấm J hoặc phím 1",
                "Ném rìu thần tự khóa mục tiêu gần nhất. Cực hợp để mở giao tranh từ xa, giữ nhịp an toàn cho người mới.",
                "Hồi chiêu: 4 giây",
                "res://Assets/Sprites/Player/Skill_1.png",
                new Color(0.2f, 0.88f, 1f)
            ),
            "K" => new SkillCardData(
                "CHIÊU K: LỐC XOÁY CẬN CHIẾN",
                "Bấm K hoặc phím 2",
                "Xoay liên hoàn gây sát thương diện rộng trong vài giây. Dùng khi bị quây đông để phá vòng vây ngay lập tức.",
                "Hồi chiêu: 6 giây",
                "res://Assets/Sprites/Player/Skill_2.png",
                new Color(1f, 0.45f, 0.25f)
            ),
            "L" => new SkillCardData(
                "CHIÊU L: ĐỊA CHẤN",
                "Bấm L hoặc phím 3",
                "Nện đất tạo địa chấn cực mạnh, khống chế vùng rộng. Để dành cho pha dồn sát thương hoặc kết liễu giao tranh khó.",
                "Hồi chiêu: 20 giây",
                "res://Assets/Sprites/Player/Skill_3.png",
                new Color(0.84f, 0.35f, 1f)
            ),
            _ => new SkillCardData(
                "KỸ NĂNG MỚI",
                "Hãy thử phím J/K/L",
                "Chiêu thức mới đã sẵn sàng. Thử ngay để cảm nhận nhịp chiến đấu của Thạch Sanh.",
                "",
                "res://icon.svg",
                new Color(0.7f, 0.8f, 1f)
            )
        };
    }

    private Texture2D BuildCleanIconTexture(string path)
    {
        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return null;
        Image img = tex.GetImage();
        if (img == null) return tex;

        img.Decompress();
        img.Convert(Image.Format.Rgba8);

        int w = img.GetWidth();
        int h = img.GetHeight();
        if (w == 0 || h == 0) return tex;

        // Sample border pixels to estimate background color
        double r = 0, g = 0, b = 0;
        int count = 0;
        int step = Math.Max(1, Math.Min(w, h) / 20);

        for (int x = 0; x < w; x += step)
        {
            var c1 = img.GetPixel(x, 0);
            if (c1.A > 0.02f) { r += c1.R; g += c1.G; b += c1.B; count++; }
            var c2 = img.GetPixel(x, h - 1);
            if (c2.A > 0.02f) { r += c2.R; g += c2.G; b += c2.B; count++; }
        }
        for (int y = 0; y < h; y += step)
        {
            var c1 = img.GetPixel(0, y);
            if (c1.A > 0.02f) { r += c1.R; g += c1.G; b += c1.B; count++; }
            var c2 = img.GetPixel(w - 1, y);
            if (c2.A > 0.02f) { r += c2.R; g += c2.G; b += c2.B; count++; }
        }

        if (count == 0)
        {
            return ImageTexture.CreateFromImage(img);
        }

        var borderAvg = new Color((float)(r / count), (float)(g / count), (float)(b / count));

        // Estimate typical variation along border to set an adaptive tolerance
        double sumDist = 0;
        int sampleCount = 0;
        for (int x = 0; x < w; x += step)
        {
            var c = img.GetPixel(x, 0);
            if (c.A > 0.02f) { sumDist += Math.Sqrt(Math.Pow(c.R - borderAvg.R, 2) + Math.Pow(c.G - borderAvg.G, 2) + Math.Pow(c.B - borderAvg.B, 2)); sampleCount++; }
            c = img.GetPixel(x, h - 1);
            if (c.A > 0.02f) { sumDist += Math.Sqrt(Math.Pow(c.R - borderAvg.R, 2) + Math.Pow(c.G - borderAvg.G, 2) + Math.Pow(c.B - borderAvg.B, 2)); sampleCount++; }
        }
        for (int y = 0; y < h; y += step)
        {
            var c = img.GetPixel(0, y);
            if (c.A > 0.02f) { sumDist += Math.Sqrt(Math.Pow(c.R - borderAvg.R, 2) + Math.Pow(c.G - borderAvg.G, 2) + Math.Pow(c.B - borderAvg.B, 2)); sampleCount++; }
            c = img.GetPixel(w - 1, y);
            if (c.A > 0.02f) { sumDist += Math.Sqrt(Math.Pow(c.R - borderAvg.R, 2) + Math.Pow(c.G - borderAvg.G, 2) + Math.Pow(c.B - borderAvg.B, 2)); sampleCount++; }
        }

        double avgDist = sampleCount > 0 ? (sumDist / sampleCount) : 0.04;
        double tol = Math.Max(0.06, avgDist * 1.8); // adaptive tolerance

        // Remove pixels close to border average color (preserve already transparent pixels)
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = img.GetPixel(x, y);
                if (p.A < 0.02f) continue;
                double dist = Math.Sqrt(Math.Pow(p.R - borderAvg.R, 2) + Math.Pow(p.G - borderAvg.G, 2) + Math.Pow(p.B - borderAvg.B, 2));
                if (dist < tol) img.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        }

        return ImageTexture.CreateFromImage(img);
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
        // guard against disposed objects
        if (!IsInstanceValid(this) || _popupOverlay == null || !IsInstanceValid(_popupOverlay)) return;

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
            if (_typewriterTween != null) _typewriterTween.Kill();
            if (_skillCardPulseTween != null) _skillCardPulseTween.Kill();

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
