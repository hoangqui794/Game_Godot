using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody2D
{
    // Skill cooldowns
    private float _skill1Timer = 0f;
    private float _skill2Timer = 0f;
    private float _skill3Timer = 0f;

    // ── Cooldown kỹ năng ──
    // Skill1 (Rìu thần): bấm thường xuyên được, chỉ mạnh hơn đòn thường
    // Skill2 (Lốc xoáy): damage tổng cao nhưng cần đứng yên, nguy hiểm
    // Skill3 (Thiên địa chấn): Mạnh nhất, chỉ dùng khi cấp bách
    // private const float Skill1Cooldown = 4.0f;  
    // private const float Skill2Cooldown = 10.0f; 
    // private const float Skill3Cooldown = 18.0f; 
    private const float Skill1Cooldown = 0f;  
    private const float Skill2Cooldown = 0f; 
    private const float Skill3Cooldown = 0f; 

    // --- Biến lưu trữ UI Chiêu thức ---
    private CanvasLayer _skillPanelLayer;
    private TextureRect[] _skillIcons = new TextureRect[3];
    private TextureRect[] _cooldownOverlays = new TextureRect[3];
    private Label[] _cooldownLabels = new Label[3];
    private static Rect2I _unifiedSkillBounds = new Rect2I();
    private static bool _skillBoundsCalculated = false;

    private bool _isSpinning = false;
    private static Texture2D _cachedAxeTexture = null;
    private static Texture2D _cachedPortraitTexture = null;

    private void PrepareAxeTexture()
    {
        if (_cachedAxeTexture != null) return;

        var fullTexture = GD.Load<Texture2D>("res://Assets/Sprites/Player/Riu_Skill.png");
        if (fullTexture == null)
        {
            fullTexture = GD.Load<Texture2D>("res://Assets/Sprites/Player/divine_axe.png");
            if (fullTexture == null) return;
        }

        Image img = fullTexture.GetImage();
        if (img == null) return;
        img.Decompress();
        img.Convert(Image.Format.Rgba8);

        // THUẬT TOÁN TÁCH NỀN THÔNG MINH
        Color bgColor = img.GetPixel(0, 0); // Lấy màu ở góc trái trên làm màu nền
        for (int y = 0; y < img.GetHeight(); y++)
        {
            for (int x = 0; x < img.GetWidth(); x++)
            {
                Color p = img.GetPixel(x, y);
                // Tính khoảng cách màu
                float diff = Mathf.Sqrt(Mathf.Pow(p.R - bgColor.R, 2) + Mathf.Pow(p.G - bgColor.G, 2) + Mathf.Pow(p.B - bgColor.B, 2));
                if (diff < 0.35f)
                    img.SetPixel(x, y, new Color(1, 1, 1, 0)); // Làm trong suốt
            }
        }

        _cachedAxeTexture = ImageTexture.CreateFromImage(img);
    }

    private Texture2D GetCleanSkillIcon(string[] paths, int index)
    {
        if (!_skillBoundsCalculated)
        {
            int gMinX = 99999, gMinY = 99999, gMaxX = 0, gMaxY = 0;
            int imgW = 0, imgH = 0;
            foreach (var p in paths)
            {
                var t = GD.Load<Texture2D>(p);
                if (t == null) continue;
                var testImg = t.GetImage();
                imgW = testImg.GetWidth(); imgH = testImg.GetHeight();
                testImg.Decompress(); testImg.Convert(Image.Format.Rgba8);
                Color cB = testImg.GetPixel(0, 0);
                for (int y = 0; y < testImg.GetHeight(); y++)
                {
                    for (int x = 0; x < testImg.GetWidth(); x++)
                    {
                        Color col = testImg.GetPixel(x, y);
                        bool isG = col.G > 0.4f && col.G > col.R * 1.1f && col.G > col.B * 1.1f;
                        float diff = Mathf.Sqrt(Mathf.Pow(col.R - cB.R, 2) + Mathf.Pow(col.G - cB.G, 2) + Mathf.Pow(col.B - cB.B, 2));
                        if (!(isG || diff < 0.15f) && col.A > 0.1f)
                        {
                            if (x < gMinX) gMinX = x; if (x > gMaxX) gMaxX = x;
                            if (y < gMinY) gMinY = y; if (y > gMaxY) gMaxY = y;
                        }
                    }
                }
            }
            if (gMaxX > gMinX && gMaxY > gMinY)
            {
                int centerX = imgW / 2;
                int centerY = imgH / 2;
                int distLeft = centerX - gMinX;
                int distRight = gMaxX - centerX;
                int distTop = centerY - gMinY;
                int distBottom = gMaxY - centerY;
                int maxDist = Mathf.Max(Mathf.Max(distLeft, distRight), Mathf.Max(distTop, distBottom));
                int maxD = maxDist * 2;
                maxD = (int)(maxD * 1.05f); // Padding vừa chạm
                _unifiedSkillBounds = new Rect2I(centerX - maxD / 2, centerY - maxD / 2, maxD, maxD);
            }
            _skillBoundsCalculated = true;
        }

        var tex = GD.Load<Texture2D>(paths[index]);
        if (tex == null) return GD.Load<Texture2D>("res://icon.svg");

        Image img = tex.GetImage();
        if (img == null) return tex;
        img.Decompress();
        img.Convert(Image.Format.Rgba8);

        Color bgColor = img.GetPixel(0, 0);
        for (int y = 0; y < img.GetHeight(); y++)
        {
            for (int x = 0; x < img.GetWidth(); x++)
            {
                Color p = img.GetPixel(x, y);
                bool isGreen = p.G > 0.4f && p.G > p.R * 1.1f && p.G > p.B * 1.1f;
                float diff = Mathf.Sqrt(Mathf.Pow(p.R - bgColor.R, 2) + Mathf.Pow(p.G - bgColor.G, 2) + Mathf.Pow(p.B - bgColor.B, 2));

                if (isGreen || diff < 0.15f)
                {
                    img.SetPixel(x, y, new Color(1, 1, 1, 0));
                }
            }
        }

        if (_unifiedSkillBounds.Size.X > 5)
        {
            return SpriteHelper.SmartPad(img, _unifiedSkillBounds, 256, 256);
        }
        return ImageTexture.CreateFromImage(img);
    }

    private void SetupSkillUI()
    {
        _skillPanelLayer = new CanvasLayer();
        _skillPanelLayer.Layer = 5; // Hiển thị đè lên trên nhân vật
        AddChild(_skillPanelLayer);

        // Khung Margin bám Toàn màn hình để tự căn chỉnh góc
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.TopWide); // Đổi thành TopWide: Chỉ bám nóc màn hình y chang khung Margin của Thanh máu (HUD.tscn)
        margin.AddThemeConstantOverride("margin_right", 100); // Chừa 100px bên phải cho nút Cài đặt (Bánh răng)
        margin.AddThemeConstantOverride("margin_top", -40); // BÙ TRỪ KHOẢNG RỖNG: Dùng thông số Âm để giật ngược toàn bộ khung kỹ năng lên trên cao, đâm xuyên lên thanh máu
        _skillPanelLayer.AddChild(margin);

        // Đảm bảo khung kỹ năng luôn hiện diện để đồng nhất giữa các màn 
        _skillPanelLayer.Visible = true;

        // HBoxContainer tự dồn các nút về góc trên phải
        var hbox = new HBoxContainer();
        hbox.Alignment = BoxContainer.AlignmentMode.End;
        hbox.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin; // Bám chặt lên TOP màn hình
        hbox.AddThemeConstantOverride("separation", 15); // Thu hẹp khoảng cách kéo sát các nút lại gần nhau
        margin.AddChild(hbox);

        string[] paths = {
            "res://Assets/Sprites/Player/Skill_1.png",
            "res://Assets/Sprites/Player/Skill_2.png",
            "res://Assets/Sprites/Player/Skill_3.png"
        };

        string[] hotkeys = { "1", "2", "3" };

        for (int i = 0; i < 3; i++)
        {
            var btnHolder = new CenterContainer(); // CenterContainer ép chặt tâm tọa độ của các thành phần bên trong nó với nhau
            btnHolder.CustomMinimumSize = new Vector2(160, 160); // Phóng to Nút thêm 100% theo yêu cầu
            hbox.AddChild(btnHolder);

            // Tải ảnh tự động Tách nền rác và Crop phần rìa thừa (Lấy đúng tỷ lệ kỹ năng)
            var tex = GetCleanSkillIcon(paths, i);

            _skillIcons[i] = new TextureRect
            {
                Texture = tex,
                CustomMinimumSize = new Vector2(160, 160), // Kích thước tăng gấp đôi
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = Control.TextureFilterEnum.Linear // Bật chế độ Tinh chỉnh Viền làm mịn rực rỡ HD
            };
            btnHolder.AddChild(_skillIcons[i]);

            // Bỏ Text phím nóng 1 2 3 vì trên ảnh gốc của User đã được tự vẽ số

            // Lớp màn lọc tối (Bật lên khi bị hồi chiêu) - Dùng lại Ảnh xịn để có viền bo tròn hoàn hảo
            _cooldownOverlays[i] = new TextureRect
            {
                Texture = tex,
                CustomMinimumSize = new Vector2(160, 160),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = Control.TextureFilterEnum.Linear,
                Modulate = new Color(0, 0, 0, 0.75f), // Đen mờ 75%
                Visible = false
            };
            btnHolder.AddChild(_cooldownOverlays[i]);

            // Chữ hiển thị số đếm lùi thời gian hồi (Nằm giữa nút)
            _cooldownLabels[i] = new Label
            {
                Text = "",
                CustomMinimumSize = new Vector2(160, 160),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false
            };
            _cooldownLabels[i].AddThemeColorOverride("font_color", new Color(1, 1, 1));
            _cooldownLabels[i].AddThemeConstantOverride("outline_size", 8);
            _cooldownLabels[i].AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
            _cooldownLabels[i].AddThemeFontSizeOverride("font_size", 56); // Cỡ chữ phóng to cùng với diện tích Nút
            btnHolder.AddChild(_cooldownLabels[i]);

            // Ẩn những kỹ năng chưa được mở khóa
            if (i >= GameManager.Instance.UnlockedSkillsCount)
            {
                btnHolder.Visible = false;
            }
        }
    }

    public void RefreshSkillUI()
    {
        if (_skillPanelLayer == null) return;
        _skillPanelLayer.Visible = true;

        for (int i = 0; i < 3; i++)
        {
            if (_skillIcons[i] != null && _skillIcons[i].GetParent() is Control holder)
            {
                // Chỉ hiện icon của kỹ năng trong phạm vi đã mở khóa
                holder.Visible = (i < GameManager.Instance.UnlockedSkillsCount);
            }
        }
    }

    private void UpdateSkillUICooldowns()
    {
        if (_skillPanelLayer == null) return;
        float[] timers = { _skill1Timer, _skill2Timer, _skill3Timer };

        for (int i = 0; i < 3; i++)
        {
            if (timers[i] > 0)
            {
                _cooldownOverlays[i].Visible = true;
                _cooldownLabels[i].Visible = true;
                // Làm tròn lên thành giây chẵn
                _cooldownLabels[i].Text = Mathf.CeilToInt(timers[i]).ToString();
            }
            else
            {
                _cooldownOverlays[i].Visible = false;
                _cooldownLabels[i].Visible = false;
            }
        }
    }

    private void HandleSkills(float dt)
    {
        if (_skillPanelLayer == null) SetupSkillUI();

        if (_skill1Timer > 0) _skill1Timer -= dt;
        if (_skill2Timer > 0) _skill2Timer -= dt;
        if (_skill3Timer > 0) _skill3Timer -= dt;

        UpdateSkillUICooldowns();

        if (_inCutscene || _isDead || _isHurt) return;
        if (!IsActionAllowedByTutorial("skill")) return;

        // Lắng nghe phím nhấn từ mọi nguồn (tay cầm, phím cứng, phím ảo)
        bool press1 = Input.IsActionJustPressed("skill1") || Input.IsKeyPressed(Key.Key1);
        bool press2 = Input.IsActionJustPressed("skill2") || Input.IsKeyPressed(Key.Key2);
        bool press3 = Input.IsActionJustPressed("skill3") || Input.IsKeyPressed(Key.Key3);
        int unlocked = GameManager.Instance.UnlockedSkillsCount;

        // Đảm bảo chặn mọi chiêu nếu nhân vật đang xuất chiêu (-_isAttacking) HOẶC bộ định giờ (Timer) chưa về 0
        if (press1 && _skill1Timer <= 0 && !_isAttacking && unlocked >= 1)
        {
            ExecuteAxeThrow();
        }
        else if (press2 && _skill2Timer <= 0 && !_isAttacking && unlocked >= 2)
        {
            ExecuteWhirlwind();
        }
        else if (press3 && _skill3Timer <= 0 && !_isAttacking && unlocked >= 3)
        {
            ExecuteEarthBreaker();
        }
    }

    // --- SKILL 1: AXE THROW ---
    private async void ExecuteAxeThrow()
    {
        _skill1Timer = Skill1Cooldown;
        _isAttacking = true; // Lock movement

        _sfxPlayer.Stream = SFX.GetAxeThrowSound();
        _sfxPlayer.Play();

        // Create axe projectile
        PrepareAxeTexture();
        var axe = new AxeProjectile();
        axe.Texture = _cachedAxeTexture;
        axe.GlobalPosition = GlobalPosition + new Vector2(_facingDirection * 20, -10);
        axe.Direction = new Vector2(_facingDirection, -0.1f).Normalized();
        // Skill1: 1.8× đòn thường (30×1.8=54)
        // → 2 nhát kill rắn (54×2=108 > 100), 3 nhát nhừ đại bàng (54×3=162 > 150)
        axe.Damage = AttackDamage * 1.8f;

        // Find nearest enemy
        var enemies = GetTree().GetNodesInGroup("enemies");
        Node2D nearest = null;
        float minDist = 500f;
        foreach (Node2D e in enemies.Cast<Node2D>())
        {
            float d = GlobalPosition.DistanceTo(e.GlobalPosition);
            if (d < minDist) { minDist = d; nearest = e; }
        }
        axe.Target = nearest;

        GetParent().AddChild(axe);
        _animatedSprite.Play("attack1");

        await ToSignal(GetTree().CreateTimer(0.3f), "timeout");
        if (!IsInstanceValid(this)) return;
        _isAttacking = false;
    }

    // --- SKILL 2: WHIRLWIND SPIN ---
    private async void ExecuteWhirlwind()
    {
        _skill2Timer = Skill2Cooldown;
        _isSpinning = true;

        // CÁC THÔNG SỐ CÓ THỂ TÙY CHỈNH DỄ DÀNG:
        float duration = 3.0f;       // 1. Thời gian xoay (Ví dụ: 3 giây)
        float rotationSpeed = 25.0f; // 2. Tốc độ xoay (Giá trị càng lớn xoay càng nhanh)
        float damageRadius = 130f;   // 3. Bán kính sát thương (Thu gọn lại để tụ lực hơn)
        float damageInterval = 0.2f; // Mỗi 0.2 giây gây sát thương 1 lần

        float elapsed = 0f;
        float damageTimer = 0f;

        _sfxPlayer.Stream = SFX.GetSpinSound();
        _sfxPlayer.Play();

        // 1. Lưu lại trạng thái gốc chính xác
        Vector2 baseScale = _animatedSprite.Scale;

        // 2. Hiệu ứng vòng xoáy (Lốc xoáy phong thần sắc lẹm)
        var spinVFX = new SpinVFX();
        // Để nguyên tỷ lệ thực 1:1, phần tính toán phối cảnh 3D sẽ do hàm _Draw tự xử lý 
        // Điều này giúp cơn bão cao sừng sững mà không bị "lùn" do lệnh kéo dẹt Scale cũ.
        spinVFX.Scale = new Vector2(1.0f, 1.0f);
        spinVFX.Position = new Vector2(0, -25); // Canh ở giữa nhân vật làm trọng tâm
        AddChild(spinVFX);

        while (elapsed < duration && IsInstanceValid(this) && !_isDead && !_isHurt)
        {
            float dt = 0.04f;
            elapsed += dt;
            damageTimer += dt;

            float angle = elapsed * rotationSpeed;

            // --- NHÂN VẬT: Xoay vòng quanh trục dọc (Đứng thẳng múa rìu) ---
            _animatedSprite.Scale = baseScale;
            _animatedSprite.Rotation = 0; // Luôn đứng thẳng, KHÔNG lộn nhào
            _animatedSprite.FlipH = Mathf.Cos(angle) < 0; // Lật liên tục siêu tốc để tạo ảo giác xoay 360 độ quanh trục Y
            _animatedSprite.Play("attack1"); // Anim múa rìu

            // Cập nhật góc xoay cho hiệu ứng Vệt chém
            spinVFX.RotationAngle = angle;

            // Tạo bóng ma tốc độ 
            if (Time.GetTicksMsec() % 100 < 50) CreateGhostEffect();

            // --- CƠ CHẾ GÂY SÁT THƯƠNG LIÊN TỤC THEO BÁN KÍNH ---
            if (damageTimer >= damageInterval)
            {
                damageTimer = 0f; // Chỉ chém mỗi 0.2s để không tính damage quá dày
                var enemies = GetTree().GetNodesInGroup("enemies");
                foreach (Node2D e in enemies)
                {
                    if (GlobalPosition.DistanceTo(e.GlobalPosition) <= damageRadius)
                    {
                        if (e.HasMethod("TakeDamage"))
                        {
                        // Skill2: 0.4×/tick, 0.2s/tick, 3s = 15 tick
                            // Tổng: 30×0.4×15=180 (kill rắn 1.8×, đại bàng 1.2× nhừ)
                            e.Call("TakeDamage", (int)(AttackDamage * 0.4f));
                        }
                    }
                }
            }

            await ToSignal(GetTree().CreateTimer(dt), "timeout");
            if (!IsInstanceValid(this)) return;
        }

        // Reset về đúng trạng thái ban đầu sau khi xoay xong
        _animatedSprite.Scale = baseScale;
        _animatedSprite.Rotation = 0;
        _animatedSprite.FlipH = _facingDirection < 0;
        _animatedSprite.Play("idle");

        _isSpinning = false;
        if (IsInstanceValid(spinVFX)) spinVFX.QueueFree();
    }

    private void CreateGhostEffect()
    {
        var ghost = new Sprite2D();
        // Lấy texture hiện tại của animation
        ghost.Texture = _animatedSprite.SpriteFrames.GetFrameTexture(_animatedSprite.Animation, _animatedSprite.Frame);
        ghost.GlobalPosition = _animatedSprite.GlobalPosition;
        ghost.Scale = _animatedSprite.GlobalScale;
        ghost.FlipH = _animatedSprite.FlipH;
        ghost.Rotation = _animatedSprite.Rotation;
        ghost.Modulate = new Color(1, 1, 1, 0.4f);
        GetParent().AddChild(ghost);

        var tw = ghost.CreateTween();
        tw.TweenProperty(ghost, "modulate:a", 0f, 0.3f);
        tw.Finished += () => ghost.QueueFree();
    }

    private void PreparePortraitTexture()
    {
        if (_cachedPortraitTexture != null) return;

        var fullTexture = GD.Load<Texture2D>("res://Assets/Sprites/Player/Thach_sanh_hoat_hoa.png");
        if (fullTexture == null)
        {
            fullTexture = GD.Load<Texture2D>("res://Assets/Sprites/Player/thach_sanh_portrait_shout.png");
            if (fullTexture == null) return;
        }

        Image img = fullTexture.GetImage();
        if (img == null) return;
        img.Decompress();
        img.Convert(Image.Format.Rgba8);

        // TÁCH NỀN CHO AVATAR (Dựa vào màu góc trái trên)
        Color bgColor = img.GetPixel(1, 1);
        for (int y = 0; y < img.GetHeight(); y++)
        {
            for (int x = 0; x < img.GetWidth(); x++)
            {
                Color p = img.GetPixel(x, y);
                float diff = Mathf.Sqrt(Mathf.Pow(p.R - bgColor.R, 2) + Mathf.Pow(p.G - bgColor.G, 2) + Mathf.Pow(p.B - bgColor.B, 2));
                // Avatar thường có nền phức tạp hơn nên dùng ngưỡng thấp hơn chút
                if (diff < 0.4f)
                    img.SetPixel(x, y, new Color(1, 1, 1, 0));
            }
        }

        _cachedPortraitTexture = ImageTexture.CreateFromImage(img);
    }

    // --- SKILL 3: EARTH BREAKER ULTIMATE ---
    private async void ExecuteEarthBreaker()
    {
        if (_isSpinning) return;
        _skill3Timer = Skill3Cooldown;
        _isAttacking = true;

        // 1. Phát âm thanh kích hoạt cực mạnh (Battle Cry & Energy Build-up)
        _sfxPlayer.Stream = SFX.GetUltimateSound();
        _sfxPlayer.VolumeDb = 10f;
        _sfxPlayer.Play();

        // 2. Chuẩn bị Texture chân dung (Xóa nền)
        PreparePortraitTexture();

        // 3. Hiện Avatar nhân vật cực ngầu bên trái (Cinematic Intro)
        var portrait = new SkillPortraitUI(_cachedPortraitTexture);
        GetTree().Root.AddChild(portrait);

        // 4. Chuẩn bị Rìu Thần khổng lồ
        PrepareAxeTexture();
        var bigAxe = new Sprite2D();
        bigAxe.Texture = _cachedAxeTexture;
        // Nhỏ lại 35% so với kích thước cũ (0.3 * 0.35 = 0.105)
        bigAxe.Scale = new Vector2(0.105f, 0.105f);
        // Để màu gốc của ảnh bạn thiết kế
        bigAxe.Modulate = new Color(1.1f, 1.1f, 1.1f);
        bigAxe.ZIndex = 2;
        AddChild(bigAxe);

        // 5. NHẢY LÊN LẤY ĐÀ (High Leap)
        _animatedSprite.Play("jump");
        Vector2 startPos = GlobalPosition;

        var leapTween = CreateTween().SetParallel(true);
        leapTween.TweenProperty(this, "global_position:y", startPos.Y - 180, 0.4f).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);

        // Thêm âm thanh nhún nhảy cực mạnh
        var jumpPlayer = new AudioStreamPlayer2D();
        jumpPlayer.Stream = SFX.GetJumpSound();
        jumpPlayer.VolumeDb = 5f;
        AddChild(jumpPlayer);
        jumpPlayer.Play();
        jumpPlayer.Finished += () => jumpPlayer.QueueFree();

        bigAxe.Position = new Vector2(-40 * _facingDirection, -80);
        bigAxe.Rotation = -Mathf.Pi / 1.5f * _facingDirection;
        leapTween.TweenProperty(bigAxe, "position", new Vector2(-20 * _facingDirection, -110), 0.4f);
        leapTween.TweenProperty(bigAxe, "rotation", -Mathf.Pi / 4 * _facingDirection, 0.4f);

        await ToSignal(leapTween, "finished");
        if (!IsInstanceValid(this)) return;

        // 6. FREEZE FRAME
        Engine.TimeScale = 0.15f;
        await ToSignal(GetTree().CreateTimer(0.12f), "timeout");
        if (!IsInstanceValid(this)) return;
        Engine.TimeScale = 1.0f;

        // 7. GIÁNG ĐÒN THIÊN THẠCH
        var slamTween = CreateTween().SetParallel(true);
        slamTween.TweenProperty(this, "global_position:y", startPos.Y, 0.15f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        slamTween.TweenProperty(bigAxe, "rotation", Mathf.Pi * 1.25f * _facingDirection, 0.13f);
        // Bổ rìu ra phía trước nhân vật
        slamTween.TweenProperty(bigAxe, "position", new Vector2(70 * _facingDirection, 40), 0.13f);

        await ToSignal(slamTween, "finished");
        if (!IsInstanceValid(this)) return;

        // 8. VA CHẠM ĐỊA CHẤN (Impact)
        if (IsInstanceValid(bigAxe)) bigAxe.QueueFree();

        // Phát âm thanh nện đất ĐỊA CHẤN (Vụ nổ cực mạnh)
        _sfxPlayer.Stream = SFX.GetEarthImpactSound();
        _sfxPlayer.VolumeDb = 12f; // Tăng cực đại uy lực
        _sfxPlayer.Play();

        // Tạo thêm một AudioPlayer phụ để chồng âm thanh va chạm kim loại
        var metalHit = new AudioStreamPlayer2D();
        metalHit.Stream = SFX.GetAttackSound(3);
        metalHit.VolumeDb = 8f;
        AddChild(metalHit);
        metalHit.Play();
        metalHit.Finished += () => metalHit.QueueFree();

        // Rung màn hình mãnh liệt
        var cam = GetViewport().GetCamera2D() as FollowCamera;
        if (cam != null) cam.Shake(1.0f, 45f);

        var crack = new GroundCrackVFX();
        // Xuất hiện vệt nứt ở phía trước chân nhân vật 60 pixel
        Vector2 impactPos = GlobalPosition + new Vector2(60 * _facingDirection, 15);
        crack.GlobalPosition = impactPos;
        GetParent().AddChild(crack);

        var enemies = GetTree().GetNodesInGroup("enemies");
        foreach (Node2D e in enemies)
        {
            // Tính sát thương dựa trên tâm nổ phía trước nhân vật
            if (impactPos.DistanceTo(e.GlobalPosition) < 350f)
            {
                if (e.HasMethod("TakeDamage"))
                {
                    // Skill3 (Thiên địa chấn): 5× ATK = 30×5 = 150 damage
                    // → kill rắn (150 > 100), hạ 1HP đại bàng (150 = 150), boss giáp sống
                    // Mạnh nhất nhưng vẫn không 1-shot boss để giữ thử thách
                    e.Call("TakeDamage", AttackDamage * 5);
                }
            }
        }

        _isAttacking = false;
        _animatedSprite.Play("idle");
    }
}

// --- PROJECTILE CLASS ---
public partial class AxeProjectile : Area2D
{
    public Vector2 Direction;
    public float Speed = 650f;
    public float Damage;
    public Node2D Target;
    public Texture2D Texture;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 4;

        var sprite = new Sprite2D();
        sprite.Texture = Texture;
        // Nhỏ lại phân nửa (0.12 * 0.5 = 0.06)
        // Nhỏ lại phù hợp (nâng lên 0.08 để thấy chi tiết)
        sprite.Scale = new Vector2(0.08f, 0.08f);
        // Để màu gốc của ảnh
        sprite.Modulate = new Color(1.0f, 1.0f, 1.0f);
        AddChild(sprite);

        // --- HIỆU ỨNG LỬA THẦN (Cân đối lại siêu nhỏ) ---
        var fire = new CpuParticles2D();
        fire.Amount = 80; // Giảm bớt lượng hạt cho đỡ rối
        fire.Lifetime = 0.4f;
        fire.SpeedScale = 3.0f;
        fire.LocalCoords = false;
        fire.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
        fire.EmissionSphereRadius = 6f; // Rất nhỏ để ôm sát rìu
        fire.Gravity = new Vector2(0, -50);
        fire.InitialVelocityMin = 40f;
        fire.InitialVelocityMax = 80f;
        fire.ScaleAmountMin = 2f;
        fire.ScaleAmountMax = 5f;
        fire.Color = new Color(1, 0.4f, 0, 0.8f);
        AddChild(fire);

        var trails = new CpuParticles2D();
        trails.Amount = 50;
        trails.Lifetime = 0.6f;
        trails.LocalCoords = false;
        trails.Gravity = Vector2.Zero;
        trails.ScaleAmountMin = 3f;
        trails.ScaleAmountMax = 6f;
        trails.Color = new Color(1, 0.8f, 0.2f, 0.5f);
        AddChild(trails);

        var shape = new CollisionShape2D();
        shape.Shape = new CircleShape2D { Radius = 35 };
        AddChild(shape);

        AreaEntered += (area) =>
        {
            if (area.IsInGroup("enemies") || area.GetParent().IsInGroup("enemies"))
                HandleHit(area);
        };
        BodyEntered += (body) =>
        {
            if (body.IsInGroup("enemies"))
                HandleHit(body);
        };

        GetTree().CreateTimer(3.0f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }

    private void HandleHit(Node node)
    {
        if (node.HasMethod("TakeDamage"))
            node.Call("TakeDamage", (int)Damage);
        else if (node.GetParent().HasMethod("TakeDamage"))
            node.GetParent().Call("TakeDamage", (int)Damage);

        QueueFree();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (IsInstanceValid(Target))
        {
            Vector2 toTarget = (Target.GlobalPosition - GlobalPosition).Normalized();
            Direction = Direction.Lerp(toTarget, dt * 6f).Normalized();
        }

        // Xoay tít mù tạo cảm giác rìu ném rất mạnh
        Rotation += dt * 35f;
        Position += Direction * Speed * dt;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("enemies") && body.HasMethod("TakeDamage"))
        {
            body.Call("TakeDamage", (int)Damage);
            QueueFree();
        }
    }
}

// --- SPIN VFX CLASS ---
public partial class SpinVFX : Node2D
{
    public float RotationAngle = 0f;

    public override void _Draw()
    {
        // VÒNG XOÁY PHONG THẦN (CẤU TRÚC XOẮN ỐC HELIX CHUẨN LỐC BÃO)
        Color coreColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        Color bladeColor = new Color(0.6f, 0.9f, 1.0f, 0.8f);
        Color trailColor = new Color(0.6f, 0.85f, 0.95f, 0.2f);

        // Trục 3D (X: to, Y: dẹt để tạo góc nhìn chéo từ trên xuống mặt đất)
        float scaleX = 1.6f;
        float scaleY = 0.40f;

        // --- 1. LUỒNG GIÓ XOẮN DỌC TỪ DƯỚI LÊN TẠO HÌNH CỘT BÃO ---
        int numCurrents = 5;
        for (int i = 0; i < numCurrents; i++)
        {
            float offset = (Mathf.Pi * 2f / numCurrents) * i;
            int segments = 24;
            Vector2[] points = new Vector2[segments];
            float totalTwist = Mathf.Pi * 2.8f; // Xoắn 1.4 vòng quanh người

            for (int p = 0; p < segments; p++)
            {
                float t = (float)p / (segments - 1);
                // Chiều cao bão: Từ chân (Y=45) vươn lên tới ngực/vai (Y=-45)
                float currentY = 45f - t * 90f;
                // Bán kính phễu (Radius): Dưới chân gom nhỏ (10), trên tỏa to (60)
                float radius = 10f + t * 50f;
                float angle = (RotationAngle * 1.5f) + offset + (t * totalTwist);

                float x = Mathf.Cos(angle) * radius * scaleX;
                float z = Mathf.Sin(angle) * radius * scaleY;
                points[p] = new Vector2(x, currentY + z); // Z depth được cộng trực tiếp vào Y
            }

            // Vẽ đa tuyến vuốt mượt: Đầu và đuôi nhỏ mờ, giữa to rõ ràng
            for (int p = 0; p < segments - 1; p++)
            {
                float t = (float)p / (segments - 1);
                float alpha = Mathf.Clamp(Mathf.Sin(t * Mathf.Pi) * 1.2f, 0, 1);

                Color cTrail = new Color(trailColor.R, trailColor.G, trailColor.B, trailColor.A * alpha);
                Color cBlade = new Color(bladeColor.R, bladeColor.G, bladeColor.B, bladeColor.A * alpha);
                Color cCore = new Color(coreColor.R, coreColor.G, coreColor.B, coreColor.A * alpha);

                DrawLine(points[p], points[p + 1], cTrail, 15f * alpha);
                DrawLine(points[p], points[p + 1], cBlade, 4f * alpha);
                DrawLine(points[p], points[p + 1], cCore, 1.5f * alpha);
            }
        }

        // --- 2. NHÁT CHÉM KHÔNG KHÍ NẰM NGANG XUNG QUANH ---
        for (int i = 0; i < 3; i++)
        {
            float yPos = 30f - i * 30f + Mathf.Sin(RotationAngle + i) * 15f;
            float radius = 25f + i * 15f;
            float startAngle = RotationAngle * 2.5f + (Mathf.Pi / 1.5f * i);
            float arcLen = 1.6f;

            int arcSegs = 10;
            for (int p = 0; p < arcSegs - 1; p++)
            {
                float t1 = (float)p / (arcSegs - 1);
                float t2 = (float)(p + 1) / (arcSegs - 1);

                float a1 = startAngle + t1 * arcLen;
                float a2 = startAngle + t2 * arcLen;

                Vector2 p1 = new Vector2(Mathf.Cos(a1) * radius * scaleX, yPos + Mathf.Sin(a1) * radius * scaleY);
                Vector2 p2 = new Vector2(Mathf.Cos(a2) * radius * scaleX, yPos + Mathf.Sin(a2) * radius * scaleY);

                float alpha = Mathf.Sin(t1 * Mathf.Pi);
                Color cTrail = new Color(trailColor.R, trailColor.G, trailColor.B, trailColor.A * alpha);
                Color cBlade = new Color(bladeColor.R, bladeColor.G, bladeColor.B, bladeColor.A * alpha);
                Color cCore = new Color(coreColor.R, coreColor.G, coreColor.B, coreColor.A * alpha);

                DrawLine(p1, p2, cTrail, 8f * alpha);
                DrawLine(p1, p2, cBlade, 2f * alpha);
                DrawLine(p1, p2, cCore, 1f * alpha);
            }
        }
    }
    public override void _Process(double delta) { QueueRedraw(); }
}

// --- GROUND CRACK VFX ---
public partial class GroundCrackVFX : Node2D
{
    private float _life = 1.0f;
    private List<Vector2> _points = new List<Vector2>();

    public override void _Ready()
    {
        // Tạo các điểm nứt ngẫu nhiên lan tỏa sang 2 bên
        for (int i = 0; i < 20; i++)
        {
            _points.Add(new Vector2(GD.Randf() * 500 - 250, GD.Randf() * 30));
        }

        var tw = CreateTween();
        tw.TweenProperty(this, "_life", 0f, 1.4f).SetTrans(Tween.TransitionType.Sine);
        tw.Finished += () => QueueFree();
    }
    public override void _Draw()
    {
        Color gold = new Color(1.0f, 0.9f, 0.2f, _life);
        Color lava = new Color(0.9f, 0.3f, 0.0f, _life * 0.6f);

        foreach (var p in _points)
        {
            float t = 1.2f - _life;
            // Vẽ vệt nứt lan tỏa
            DrawLine(new Vector2(0, -10), p * t, gold, 8f * _life);
            // Vẽ các đốm lửa bùng lên
            DrawCircle(p * t, 15f * _life, lava);
        }
    }
    public override void _Process(double delta) { QueueRedraw(); }
}

// --- SKILL PORTRAIT UI ---
public partial class SkillPortraitUI : CanvasLayer
{
    private Texture2D _portraitTex;

    public SkillPortraitUI(Texture2D tex)
    {
        _portraitTex = tex;
    }

    public override void _Ready()
    {
        Layer = 1;

        var control = new Control();
        control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(control);

        // HIỆN ẢNH THẠCH SANH HOẠT HỌA (Nguyên bản, không khung)
        if (_portraitTex != null)
        {
            var portrait = new TextureRect();
            portrait.Texture = _portraitTex;
            portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

            // Kích thước hiển thị vừa vặn hơn
            portrait.Size = new Vector2(320, 420);
            portrait.Position = new Vector2(-400, 10); // Đưa sát lên mép trên (Y=10)
            portrait.Modulate = new Color(1.1f, 1.1f, 1.1f, 0f);
            control.AddChild(portrait);

            // Tên chiêu thức
            var label = new Label
            {
                Text = "SƠN HÀ THIÊN TỔ!",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            label.Modulate = new Color(2.5f, 2.0f, 0.5f, 0);
            label.Scale = new Vector2(2.5f, 2.5f);
            label.Position = new Vector2(100, 150); // Hạ thấp nhãn xuống theo Avatar (Gần sát mép trên hơn)
            control.AddChild(label);

            // ANIMATION SLIDE-IN
            var tw = CreateTween().SetParallel(true);
            tw.TweenProperty(portrait, "position:x", 30f, 0.6f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(portrait, "modulate:a", 1.0f, 0.4f);

            tw.TweenProperty(label, "modulate:a", 1.0f, 0.5f);
            tw.TweenProperty(label, "position:y", 210f, 0.6f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

            // Tự hủy sau màn intro
            var timer = GetTree().CreateTimer(1.8f);
            timer.Timeout += () =>
            {
                var fade = CreateTween();
                fade.TweenProperty(control, "modulate:a", 0f, 0.5f);
                fade.Finished += () => QueueFree();
            };
        }
        else { QueueFree(); }
    }
}
