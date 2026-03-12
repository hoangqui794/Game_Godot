using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DialogueManager : CanvasLayer
{
    private PanelContainer _panel;
    private TextureRect _avatarRect;
    private Label _nameLabel;
    private Label _textLabel;
    private Label _promptLabel;
    private AudioStreamPlayer _voicePlayer;

    private bool _isTyping = false;
    private bool _skipTypingRequested = false;
    private bool _nextLineRequested = false;

    // Cấu trúc đại diện cho 1 câu thoại
    public struct DialogueLine
    {
        public string CharacterName;
        public string Text;
        public Texture2D Avatar;
        public string VoicePath;

        public DialogueLine(string characterName, string text, Texture2D avatar = null, string voicePath = "")
        {
            CharacterName = characterName;
            Text = text;
            Avatar = avatar;
            VoicePath = voicePath;
        }
    }

    public override void _Ready()
    {
        // Chế độ Always để hộp thoại vẫn hoạt động khi game bị Tạm Dừng (Pause)
        ProcessMode = ProcessModeEnum.Always;
        Layer = 125; // Cao hơn UI lớp màn hình, để luôn hiện trên cùng lúc chơi
        BuildUi();
    }

    // Hàm gọi để phát 1 mảng các câu thoại
    public async Task PlayDialogue(List<DialogueLine> lines)
    {
        // Dừng thời gian game khi đang hội thoại
        if (GameManager.Instance != null && GameManager.Instance.GetTree() != null)
        {
            GameManager.Instance.GetTree().Paused = true;
        }

        Visible = true;
        _panel.Modulate = new Color(1, 1, 1, 0);
        var twIn = CreateTween();
        twIn.SetPauseMode(Tween.TweenPauseMode.Process); // Đảm bảo Tween chạy lúc Pause
        twIn.TweenProperty(_panel, "modulate:a", 1f, 0.2f);
        await ToSignal(twIn, Tween.SignalName.Finished);

        // Duyệt qua từng câu thoại
        foreach (var line in lines)
        {
            await ShowLine(line);

            // Hiện nhấp nháy dòng báo hiệu cho người chơi biết đoạn thoại đã gõ xong
            _nextLineRequested = false;
            _promptLabel.Visible = true;

            var blinkTw = CreateTween().SetLoops();
            blinkTw.SetPauseMode(Tween.TweenPauseMode.Process); // Đảm bảo Tween chạy lúc Pause
            blinkTw.TweenProperty(_promptLabel, "modulate:a", 0.2f, 0.5f);
            blinkTw.TweenProperty(_promptLabel, "modulate:a", 1.0f, 0.5f);

            // Chờ người chơi nhấn Phím chuyển câu
            while (!_nextLineRequested)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            blinkTw.Kill();
            _promptLabel.Visible = false;
        }

        // Hội thoại kết thúc, mờ dần rồi xóa
        var twOut = CreateTween();
        twOut.SetPauseMode(Tween.TweenPauseMode.Process);
        twOut.TweenProperty(_panel, "modulate:a", 0f, 0.2f);
        await ToSignal(twOut, Tween.SignalName.Finished);

        Visible = false;

        // Tiếp tục trò chơi
        if (GameManager.Instance != null && GameManager.Instance.GetTree() != null)
        {
            GameManager.Instance.GetTree().Paused = false;
        }

        QueueFree(); // Tự hủy bộ quản lý khi đọc thoại xong
    }

    private async Task ShowLine(DialogueLine line)
    {
        _nameLabel.Text = line.CharacterName;
        _textLabel.Text = line.Text;
        _textLabel.VisibleCharacters = 0;

        // Đổi màu tên tùy vào nhân vật
        if (line.CharacterName == "Thạch Sanh")
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1f)); // Xanh lơ sáng thiện
        else if (line.CharacterName.Contains("Chằn Tinh") || line.CharacterName.Contains("Đại Xà") || line.CharacterName.Contains("Rắn") || line.CharacterName.Contains("Đại Bàng"))
            _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f)); // Đỏ ác
        else if (line.CharacterName.Contains("Ngọc Hoàng"))
            _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f)); // Vàng quyền uy
        else
            _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f)); // Trắng mặc định

        // Avatar
        if (line.Avatar != null)
        {
            _avatarRect.Texture = line.Avatar;
            _avatarRect.Visible = true;
        }
        else
        {
            _avatarRect.Visible = false;
        }

        // Voice/Audio
        if (!string.IsNullOrEmpty(line.VoicePath) && ResourceLoader.Exists(line.VoicePath))
        {
            AudioStream stream = GD.Load<AudioStream>(line.VoicePath);
            _voicePlayer.Stream = stream;
            _voicePlayer.Play();
        }
        else
        {
            _voicePlayer.Stop();
        }

        _isTyping = true;
        _skipTypingRequested = false;

        int totalChars = line.Text.Length;
        float timePerChar = 0.035f; // Tốc độ gõ chữ (càng nhỏ càng nhanh)

        for (int i = 0; i <= totalChars; i++)
        {
            // Bấm bỏ qua thì nhảy hết chữ
            if (_skipTypingRequested)
            {
                _textLabel.VisibleCharacters = totalChars;
                break;
            }

            _textLabel.VisibleCharacters = i;

            // processAlways=true, ignoreTimeScale=true để chạy lúc Pause
            await ToSignal(GetTree().CreateTimer(timePerChar, true, false, true), SceneTreeTimer.SignalName.Timeout);
        }

        _isTyping = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Echo) return;
        if (!@event.IsPressed()) return;

        // Bấm Phím Space, Phím Chém (H), hoặc Chuột Trái để Qua câu/Skip gõ
        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("attack") || @event.IsActionPressed("jump"))
        {
            if (_isTyping)
            {
                _skipTypingRequested = true; // Hiện dòng thoại ngay lập tức
            }
            else
            {
                _nextLineRequested = true; // Chuyển câu thoại kế
            }

            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        Visible = false;

        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _voicePlayer = new AudioStreamPlayer();
        _voicePlayer.ProcessMode = ProcessModeEnum.Always; // Giữ âm thanh phát kể cả khi pause
        AddChild(_voicePlayer);

        _panel = new PanelContainer();
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        // Vị trí Box thoại: Cách màn hình đáy một khoảng nhỏ
        _panel.OffsetLeft = -420;
        _panel.OffsetRight = 420;
        _panel.OffsetTop = -250;
        _panel.OffsetBottom = -60;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.04f, 0.04f, 0.07f, 0.95f);
        panelStyle.BorderWidthLeft = 2;
        panelStyle.BorderWidthRight = 2;
        panelStyle.BorderWidthTop = 2;
        panelStyle.BorderWidthBottom = 2;
        panelStyle.BorderColor = new Color(0.6f, 0.5f, 0.3f, 1f); // Viền vàng mờ
        panelStyle.SetCornerRadiusAll(15);
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        root.AddChild(_panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        _panel.AddChild(margin);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 24);
        margin.AddChild(hbox);

        // Hộp Avatar (Sẽ hiện Ảnh chân dung nhân vật nếu bạn set)
        _avatarRect = new TextureRect();
        _avatarRect.CustomMinimumSize = new Vector2(140, 140);
        _avatarRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _avatarRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _avatarRect.Visible = false;
        hbox.AddChild(_avatarRect);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 10);
        hbox.AddChild(vbox);

        _nameLabel = new Label();
        _nameLabel.AddThemeFontSizeOverride("font_size", 26);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f, 1f));
        vbox.AddChild(_nameLabel);

        _textLabel = new Label();
        _textLabel.AddThemeFontSizeOverride("font_size", 22);
        _textLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f, 1f));
        _textLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart; // Tự động ngắt dòng
        _textLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(_textLabel);

        _promptLabel = new Label();
        _promptLabel.Text = "Nhấn Phím Space hoặc H để tiếp tục ▼";
        _promptLabel.AddThemeFontSizeOverride("font_size", 16);
        _promptLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 1f));
        _promptLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _promptLabel.Visible = false;
        vbox.AddChild(_promptLabel);
    }
}
