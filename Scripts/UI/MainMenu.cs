using Godot;

/// <summary>
/// Màn Hình Chính của Game
/// Được nạp khi mới mở game, hoặc khi người chơi chọn "Về Menu" từ các màn khác
/// </summary>
public partial class MainMenu : Control
{
    public override void _Ready()
    {
        // Đảm bảo game không bị pause khi ở Menu chính
        GetTree().Paused = false;
        Engine.TimeScale = 1.0f;
        if (GameManager.Instance != null) GameManager.Instance.IsPaused = false;
        
        // Liên kết node nút bấm (không cần qua VBoxContainer nữa)
        var playButton = GetNode<Button>("PlayButton");
        var quitButton = GetNode<Button>("QuitButton");
        var titleLabel = GetNode<Label>("TitleLabel");

        // ── Thêm nút Loa (Bật/Tắt âm thanh) ở góc phải ──────────────────
        BuildMuteButton();

        // Gắn sự kiện chuyển cảnh
        playButton.Pressed += OnPlayPressed;
        quitButton.Pressed += OnQuitPressed;

        // Mặc định trò chơi bật lên sẽ Focus nút Play 
        playButton.GrabFocus();

        // Load trước Intro và Level 1 ngay khi vào Main Menu (ngầm dưới RAM)
        GameManager.Instance.PreloadScene("res://Scenes/Main/Intro.tscn");
        GameManager.Instance.PreloadScene("res://Scenes/Levels/Level1.tscn");

        // [Tùy chọn] Animation cho TitleLabel (trôi từ trên xuống nhẹ)
        titleLabel.Modulate = new Color(1, 1, 1, 0);
        titleLabel.Position += new Vector2(0, -30);
        var tw = CreateTween();
        tw.SetParallel(true); // Chạy 2 tween cùng lúc
        tw.TweenProperty(titleLabel, "modulate:a", 1.0f, 0.8f).SetTrans(Tween.TransitionType.Quad);
        tw.TweenProperty(titleLabel, "position:y", titleLabel.Position.Y + 30, 0.8f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void BuildMuteButton()
    {
        var muteButton = new Button();
        
        // Lấy trạng thái âm thanh hiện tại để set icon ban đầu
        bool isMuted = AudioServer.IsBusMute(AudioServer.GetBusIndex("Master"));
        muteButton.Text = isMuted ? "🔇" : "🔊";
        muteButton.AddThemeFontSizeOverride("font_size", 30);
        muteButton.CustomMinimumSize = new Vector2(60, 60);

        // Style cho nút loa
        var styleNormal = new StyleBoxFlat();
        styleNormal.BgColor = new Color(0, 0, 0, 0.3f); // Nền đen mờ
        styleNormal.SetCornerRadiusAll(30); // Hình tròn

        var styleHover = new StyleBoxFlat();
        styleHover.BgColor = new Color(1, 1, 1, 0.1f);
        styleHover.SetCornerRadiusAll(30);

        muteButton.AddThemeStyleboxOverride("normal", styleNormal);
        muteButton.AddThemeStyleboxOverride("hover", styleHover);
        muteButton.AddThemeStyleboxOverride("pressed", styleHover);
        muteButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        // Đặt vị trí góc trên bên phải
        muteButton.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        muteButton.OffsetLeft = -70;
        muteButton.OffsetTop = 10;
        muteButton.OffsetRight = -10;
        muteButton.OffsetBottom = 70;

        muteButton.Pressed += () => {
            bool currentMute = AudioServer.IsBusMute(AudioServer.GetBusIndex("Master"));
            bool newMute = !currentMute;
            AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), newMute);
            muteButton.Text = newMute ? "🔇" : "🔊";
        };

        AddChild(muteButton);
    }

    private void OnPlayPressed()
    {
        // Khi nhấn Bắt đầu chơi: Cần chạy Intro trước khi vào Level 1
        GameManager.Instance.StartIntro();
    }

    private void OnQuitPressed()
    {
        // Thoát khỏi trò chơi xuống màn hình desktop
        GetTree().Quit();
    }
}
