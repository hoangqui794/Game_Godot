using Godot;

public partial class HUD : CanvasLayer
{
    // ── HUD widgets ──────────────────────────────────────────────────────────
    private ProgressBar _healthBar;
    private Label       _scoreLabel;
    private Label       _levelLabel;
    private Label       _livesLabel;
    private Label       _keysLabel;

    // ── Pause UI ─────────────────────────────────────────────────────────────
    // _overlayLayer dùng Layer=10, luôn đè lên skill icons (Layer=5)
    // → nút bánh răng và pause panel KHÔNG BAO GIỜ bị skill icons che khuất
    private CanvasLayer _overlayLayer;
    private Button      _gearButton;
    private Panel       _pausePanel;
    private Button      _resumeButton;
    private Button      _muteButton;
    private Button      _exitButton;
    private Label       _countdownLabel;

    // ── State ────────────────────────────────────────────────────────────────
    private Player _player;
    private bool   _isMuted = false;

    // ═════════════════════════════════════════════════════════════════════════
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        // ── lấy node gốc có sẵn trong scene ──────────────────────────────
        _healthBar  = GetNode<ProgressBar>("MarginContainer/HBoxContainer/HealthBar");
        _scoreLabel = GetNode<Label>      ("MarginContainer/HBoxContainer/ScoreLabel");
        _levelLabel = GetNode<Label>      ("MarginContainer/HBoxContainer/LevelLabel");
        
        if (HasNode("MarginContainer/HBoxContainer/HealthIcon"))
            GetNode<Control>("MarginContainer/HBoxContainer/HealthIcon").Visible = false;

        var mainHBox = GetNode<BoxContainer>("MarginContainer/HBoxContainer");

        // ── VBox nhóm thanh máu + mạng ────────────────────────────────────
        var healthSection = new VBoxContainer();
        healthSection.AddThemeConstantOverride("separation", 2);
        mainHBox.AddChild(healthSection);
        mainHBox.MoveChild(healthSection, 0);

        _healthBar.GetParent().RemoveChild(_healthBar);
        healthSection.AddChild(_healthBar);

        // Label ❤️ mạng
        _livesLabel = new Label();
        _livesLabel.AddThemeFontSizeOverride("font_size", 18);
        _livesLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _livesLabel.AddThemeColorOverride("font_color", new Color(1, 0.3f, 0.3f));
        _livesLabel.AddThemeConstantOverride("outline_size", 3);
        _livesLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        healthSection.AddChild(_livesLabel);

        // Label 🗝️ chìa khóa
        _keysLabel = new Label();
        _keysLabel.AddThemeFontSizeOverride("font_size", 18);
        _keysLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _keysLabel.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.2f));
        _keysLabel.AddThemeConstantOverride("outline_size", 3);
        _keysLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        healthSection.AddChild(_keysLabel);

        // ── CanvasLayer riêng cho gear + pause ───────────────────────────
        // Skill icons dùng Layer=5 → đặt layer này = 10 để KHÔNG bao giờ bị che
        _overlayLayer = new CanvasLayer();
        _overlayLayer.Layer = 10;
        _overlayLayer.ProcessMode = ProcessModeEnum.Always;
        AddChild(_overlayLayer);

        // ── ⚙️ Nút bánh răng góc PHẢI TRÊN ───────────────────────────────
        BuildGearButton();

        // ── Overlay tạm dừng ──────────────────────────────────────────────
        BuildPausePanel();

        // ── Nhãn đếm ngược ────────────────────────────────────────────────
        _countdownLabel = new Label();
        _countdownLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _countdownLabel.VerticalAlignment   = VerticalAlignment.Center;
        _countdownLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _countdownLabel.GrowHorizontal = Control.GrowDirection.Both;
        _countdownLabel.GrowVertical   = Control.GrowDirection.Both;
        _countdownLabel.AddThemeFontSizeOverride("font_size", 120);
        _countdownLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _countdownLabel.AddThemeConstantOverride("outline_size", 15);
        _countdownLabel.Visible = false;
        _overlayLayer.AddChild(_countdownLabel); // nằm trong layer riêng

        UpdateUI();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  BUILD GEAR BUTTON  (đưa vào _overlayLayer)
    // ═════════════════════════════════════════════════════════════════════════
    private void BuildGearButton()
    {
        var anchor = new Control();
        anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        anchor.MouseFilter = Control.MouseFilterEnum.Ignore;
        _overlayLayer.AddChild(anchor); // ← vào _overlayLayer, không vào HUD layer

        _gearButton = new Button();
        _gearButton.Text = "⚙";
        _gearButton.AddThemeFontSizeOverride("font_size", 28);
        _gearButton.CustomMinimumSize = new Vector2(54, 54);

        var styleNormal = new StyleBoxFlat();
        styleNormal.BgColor = new Color(0.08f, 0.08f, 0.10f, 0.78f);
        styleNormal.SetCornerRadiusAll(27);
        styleNormal.BorderWidthLeft  = styleNormal.BorderWidthRight  =
        styleNormal.BorderWidthTop   = styleNormal.BorderWidthBottom = 2;
        styleNormal.BorderColor = new Color(0.55f, 0.55f, 0.60f, 0.9f);

        var styleHover = new StyleBoxFlat();
        styleHover.BgColor = new Color(0.18f, 0.18f, 0.22f, 0.92f);
        styleHover.SetCornerRadiusAll(27);
        styleHover.BorderWidthLeft  = styleHover.BorderWidthRight  =
        styleHover.BorderWidthTop   = styleHover.BorderWidthBottom = 2;
        styleHover.BorderColor = new Color(0.85f, 0.75f, 0.30f, 1f); // vàng khi hover

        _gearButton.AddThemeStyleboxOverride("normal",  styleNormal);
        _gearButton.AddThemeStyleboxOverride("hover",   styleHover);
        _gearButton.AddThemeStyleboxOverride("pressed", styleHover);
        _gearButton.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
        _gearButton.AddThemeColorOverride("font_color", Colors.White);

        // Góc phải trên (Skill Icons đã được dịch sang trái để chừa chỗ này)
        _gearButton.AnchorLeft   = 1f;
        _gearButton.AnchorTop    = 0f;
        _gearButton.AnchorRight  = 1f;
        _gearButton.AnchorBottom = 0f;
        _gearButton.OffsetLeft   = -80f;   // Nằm trong khoảng 100px trống bên phải
        _gearButton.OffsetTop    = 15f;    // Căn chỉnh cho đẹp với Hub/Skill
        _gearButton.OffsetRight  = -16f;
        _gearButton.OffsetBottom = 79f;

        _gearButton.Pressed += OnGearPressed;
        anchor.AddChild(_gearButton);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  BUILD PAUSE PANEL  (đưa vào _overlayLayer)
    // ═════════════════════════════════════════════════════════════════════════
    private void BuildPausePanel()
    {
        _pausePanel = new Panel();
        _pausePanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pausePanel.MouseFilter = Control.MouseFilterEnum.Stop;
        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0f, 0f, 0f, 0.52f);
        _pausePanel.AddThemeStyleboxOverride("panel", bgStyle);
        _pausePanel.Visible = false;
        _overlayLayer.AddChild(_pausePanel); // ← vào _overlayLayer

        // --- Hộp menu trung tâm ---
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Ignore;
        _pausePanel.AddChild(center);

        var box = new PanelContainer();
        box.CustomMinimumSize = new Vector2(380, 0);
        var boxStyle = new StyleBoxFlat();
        boxStyle.BgColor = new Color(0.07f, 0.08f, 0.11f, 0.97f);
        boxStyle.SetCornerRadiusAll(20);
        boxStyle.BorderWidthLeft = boxStyle.BorderWidthRight =
        boxStyle.BorderWidthTop  = boxStyle.BorderWidthBottom = 3;
        boxStyle.BorderColor = new Color(0.85f, 0.7f, 0.25f, 1f);
        boxStyle.ShadowColor = new Color(0, 0, 0, 0.5f);
        boxStyle.ShadowSize  = 12;
        box.AddThemeStyleboxOverride("panel", boxStyle);
        center.AddChild(box);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   36);
        margin.AddThemeConstantOverride("margin_right",  36);
        margin.AddThemeConstantOverride("margin_top",    32);
        margin.AddThemeConstantOverride("margin_bottom", 32);
        box.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        margin.AddChild(vbox);

        // --- Tiêu đề ---
        var titleRow = new HBoxContainer();
        titleRow.Alignment = BoxContainer.AlignmentMode.Center;
        titleRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(titleRow);

        var gearIcon = new Label();
        gearIcon.Text = "⚙";
        gearIcon.AddThemeFontSizeOverride("font_size", 36);
        gearIcon.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.4f));
        titleRow.AddChild(gearIcon);

        var titleLabel = new Label();
        titleLabel.Text = "TẠM DỪNG";
        titleLabel.AddThemeFontSizeOverride("font_size", 36);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.4f));
        titleLabel.AddThemeConstantOverride("outline_size", 5);
        titleLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        titleRow.AddChild(titleLabel);

        // Divider
        var divider = new ColorRect();
        divider.Color = new Color(0.85f, 0.7f, 0.25f, 0.4f);
        divider.CustomMinimumSize = new Vector2(0, 2);
        vbox.AddChild(divider);

        // --- Nút TIẾP TỤC ---
        _resumeButton = MakePauseButton(
            "▶  TIẾP TỤC",
            new Color(0.13f, 0.55f, 0.13f),
            new Color(0.18f, 0.72f, 0.18f));
        _resumeButton.Pressed += StartResumeCountdown;
        vbox.AddChild(_resumeButton);

        // --- Nút TẮT/BẬT ÂM THANH ---
        // Đồng bộ với trạng thái thực tế của hệ thống
        int masterIndex = AudioServer.GetBusIndex("Master");
        _isMuted = AudioServer.IsBusMute(masterIndex);
        
        _muteButton = MakePauseButton(
            _isMuted ? "🔇  ÂM THANH: TẮT" : "🔊  ÂM THANH: BẬT",
            _isMuted ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.15f, 0.35f, 0.60f),
            _isMuted ? new Color(0.45f, 0.45f, 0.45f) : new Color(0.20f, 0.50f, 0.80f));
        _muteButton.Pressed += OnMutePressed;
        vbox.AddChild(_muteButton);

        // --- Nút THOÁT GAME ---
        _exitButton = MakePauseButton(
            "✕  THOÁT GAME",
            new Color(0.65f, 0.13f, 0.13f),
            new Color(0.85f, 0.20f, 0.20f));
        _exitButton.Pressed += () => GameManager.Instance.GoToMainMenu();
        vbox.AddChild(_exitButton);
    }

    private Button MakePauseButton(string text, Color normalColor, Color hoverColor)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(0, 58);
        btn.AddThemeFontSizeOverride("font_size", 22);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);

        var sNormal = new StyleBoxFlat();
        sNormal.BgColor = normalColor;
        sNormal.SetCornerRadiusAll(10);

        var sHover = new StyleBoxFlat();
        sHover.BgColor = hoverColor;
        sHover.SetCornerRadiusAll(10);
        sHover.BorderWidthLeft = sHover.BorderWidthRight =
        sHover.BorderWidthTop  = sHover.BorderWidthBottom = 2;
        sHover.BorderColor = Colors.White with { A = 0.3f };

        btn.AddThemeStyleboxOverride("normal",  sNormal);
        btn.AddThemeStyleboxOverride("hover",   sHover);
        btn.AddThemeStyleboxOverride("pressed", sHover);
        btn.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
        return btn;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  SỰ KIỆN
    // ═════════════════════════════════════════════════════════════════════════
    private void OnGearPressed() => GameManager.Instance.TogglePause();

    private void OnMutePressed()
    {
        _isMuted = !_isMuted;
        AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), _isMuted);
        _muteButton.Text = _isMuted ? "🔇  ÂM THANH: TẮT" : "🔊  ÂM THANH: BẬT";

        var sNormal = new StyleBoxFlat();
        sNormal.BgColor = _isMuted
            ? new Color(0.35f, 0.35f, 0.35f)
            : new Color(0.15f, 0.35f, 0.60f);
        sNormal.SetCornerRadiusAll(10);
        _muteButton.AddThemeStyleboxOverride("normal", sNormal);
    }

    private void StartResumeCountdown()
    {
        _pausePanel.Visible = false;
        _countdownLabel.Visible = true;
        _countdownLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f));

        var tw = CreateTween();
        tw.TweenCallback(Callable.From(() => _countdownLabel.Text = "3")).SetDelay(0f);
        tw.TweenCallback(Callable.From(() => _countdownLabel.Text = "2")).SetDelay(1.0f);
        tw.TweenCallback(Callable.From(() => _countdownLabel.Text = "1")).SetDelay(1.0f);
        tw.TweenCallback(Callable.From(() => _countdownLabel.Text = "GO!")).SetDelay(1.0f);
        tw.TweenCallback(Callable.From(() =>
        {
            _countdownLabel.Visible = false;
            GameManager.Instance.TogglePause();
        })).SetDelay(0.5f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  PROCESS
    // ═════════════════════════════════════════════════════════════════════════
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_player))
        {
            if (GetTree().GetFirstNodeInGroup("player") is Player p)
            {
                _player = p;
                _player.HealthChanged += OnHealthChanged;
            }
        }

        UpdateUI();

        if (_pausePanel != null && !_countdownLabel.Visible)
            _pausePanel.Visible = GameManager.Instance.IsPaused;

        // Hiệu ứng xoay nhẹ bánh răng khi pause
        if (_gearButton != null)
        {
            float target = GameManager.Instance.IsPaused ? Mathf.Pi / 6f : 0f;
            _gearButton.PivotOffset = _gearButton.Size / 2f;
            _gearButton.Rotation = Mathf.Lerp(_gearButton.Rotation, target, 0.12f);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UPDATE UI
    // ═════════════════════════════════════════════════════════════════════════
    private void UpdateUI()
    {
        if (_healthBar != null)
        {
            _healthBar.MaxValue = GameManager.Instance.MaxPlayerHealth;
            _healthBar.Value    = GameManager.Instance.PlayerHealth;
            float pct = (float)GameManager.Instance.PlayerHealth / GameManager.Instance.MaxPlayerHealth;
            _healthBar.Modulate = pct > 0.66f ? Colors.Green
                                : pct > 0.33f ? Colors.Yellow
                                :               Colors.Red;
        }

        if (_scoreLabel != null)
            _scoreLabel.Text = $"Điểm: {GameManager.Instance.Score}";

        if (_levelLabel != null)
        {
            _levelLabel.Text = GameManager.Instance.CurrentLevel switch
            {
                1 => "Đường Rừng Hiểm Trở",
                2 => "Hang Tối Hiểm Nguy",
                3 => "Đại Chiến Chằn Tinh",
                _ => $"Level {GameManager.Instance.CurrentLevel}"
            };
        }

        if (_livesLabel != null)
        {
            string hearts = "";
            for (int i = 0; i < GameManager.Instance.PlayerLives; i++) hearts += "❤️ ";
            _livesLabel.Text = hearts.Trim();
        }

        if (_keysLabel != null)
        {
            _keysLabel.Text    = $"🗝️ Chìa khóa: {GameManager.Instance.TotalKeys} / 3";
            _keysLabel.Visible = GameManager.Instance.TotalKeys > 0;
        }
    }

    private void OnHealthChanged(int newHealth, int maxHealth)
    {
        if (_healthBar == null) return;
        _healthBar.MaxValue = maxHealth;
        _healthBar.Value    = newHealth;
        float pct = (float)newHealth / maxHealth;
        _healthBar.Modulate = pct > 0.66f ? Colors.Green
                            : pct > 0.33f ? Colors.Yellow
                            :               Colors.Red;
    }
}
