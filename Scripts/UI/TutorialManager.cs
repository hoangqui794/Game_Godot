using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class TutorialManager : CanvasLayer
{
    private Player _player;
    private bool _skipRequested = false;

    private PanelContainer _panel;
    private Label _stepLabel;
    private Label _titleLabel;
    private Label _bodyLabel;
    private Label _hintLabel;
    private Button _skipButton;
    private AudioStreamPlayer _voicePlayer;

    private bool _moveDone;
    private bool _jumpDone;
    private bool _attackDone;

    private readonly struct TutorialStep
    {
        public readonly string ActionId;
        public readonly string Title;
        public readonly string Body;
        public readonly string Hint;
        public readonly string VoicePath;
        public readonly Func<bool> IsCompleted;

        public TutorialStep(string actionId, string title, string body, string hint, string voicePath, Func<bool> isCompleted)
        {
            ActionId = actionId;
            Title = title;
            Body = body;
            Hint = hint;
            VoicePath = voicePath;
            IsCompleted = isCompleted;
        }
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 120;
        BuildUi();
    }

    public async Task<bool> RunTutorial(Player player)
    {
        _player = player;
        if (!IsInstanceValid(_player)) return false;

        _skipRequested = false;
        ResetStepFlags();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.IsTutorialRunning = true;
        }

        Visible = true;

        var steps = new List<TutorialStep>
        {
            new TutorialStep(
                "move",
                "🗣️ Thạch Sanh",
                "Đường rừng này không có kẻ địch, nhưng chỉ cần sơ sẩy một bước là trả giá. Phải thật tỉnh táo thôi.",
                "A/D hoặc ←/→ để đi.",
                "res://Assets/Audio/Voices/ts_tut_move.mp3",
                () => _moveDone
            ),
            new TutorialStep(
                "jump",
                "🗣️ Thạch Sanh",
                "Khoảng cách này không thể bước thường. Phải lấy đà rồi nhảy thật chuẩn.",
                "Nhấn Space (Phím cách) để nhảy qua.",
                "res://Assets/Audio/Voices/ts_tut_jump.mp3",
                () => _jumpDone
            ),
            new TutorialStep(
                "djump",
                "🗣️ Thạch Sanh",
                "Vách đá dựng đứng… Một cú nhảy là chưa đủ. Ta phải đạp gió thêm lần nữa để nhảy cao hơn mới được!",
                "Nhấn Space thêm một lần nữa trên không (Double Jump).",
                "res://Assets/Audio/Voices/ts_tut_djump.mp3",
                () => _jumpDone // Có thể dùng _jumpDone cho djump ở bước này
            ),
            new TutorialStep(
                "attack",
                "🗣️ Thạch Sanh",
                "Trước khi tiến vào sâu trong hang, ta phải khởi động tay chân. Rìu thần sẽ không nương tay với những yêu tà đâu!",
                "Nhấn H hoặc chuột trái để chém.",
                "res://Assets/Audio/Voices/ts_tut_attack.mp3",
                () => _attackDone
            )
        };

        for (int i = 0; i < steps.Count; i++)
        {
            if (_skipRequested) break;

            var step = steps[i];
            ShowStep(step, i + 1, steps.Count);

            if (IsInstanceValid(_player))
            {
                _player.SetTutorialExpectedAction(step.ActionId);
            }

            while (!_skipRequested && !step.IsCompleted())
            {
                CaptureStepInput(step.ActionId);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            await ToSignal(GetTree().CreateTimer(0.12f), SceneTreeTimer.SignalName.Timeout);
            ResetStepFlags();
        }

        FinishTutorial();
        return !_skipRequested;
    }

    private void BuildUi()
    {
        Visible = false;

        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _voicePlayer = new AudioStreamPlayer();
        AddChild(_voicePlayer);

        _panel = new PanelContainer();
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _panel.OffsetLeft = -330;
        _panel.OffsetTop = 28;
        _panel.OffsetRight = 330;
        _panel.OffsetBottom = 250;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.06f, 0.08f, 0.12f, 0.9f);
        panelStyle.BorderWidthLeft = 2;
        panelStyle.BorderWidthRight = 2;
        panelStyle.BorderWidthTop = 2;
        panelStyle.BorderWidthBottom = 2;
        panelStyle.BorderColor = new Color(0.9f, 0.78f, 0.36f, 1f);
        panelStyle.SetCornerRadiusAll(18);
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        root.AddChild(_panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        _panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        _stepLabel = new Label();
        _stepLabel.AddThemeFontSizeOverride("font_size", 20);
        _stepLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.92f, 1f, 1f));
        vbox.AddChild(_stepLabel);

        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 32);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.45f, 1f));
        vbox.AddChild(_titleLabel);

        _bodyLabel = new Label();
        _bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _bodyLabel.AddThemeFontSizeOverride("font_size", 24);
        _bodyLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.96f, 1f, 1f));
        vbox.AddChild(_bodyLabel);

        _hintLabel = new Label();
        _hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hintLabel.AddThemeFontSizeOverride("font_size", 19);
        _hintLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.93f, 0.75f, 1f));
        vbox.AddChild(_hintLabel);

        var bottomRow = new HBoxContainer();
        bottomRow.Alignment = BoxContainer.AlignmentMode.End;
        bottomRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(bottomRow);

        _skipButton = new Button();
        _skipButton.Text = "Bỏ qua";
        _skipButton.CustomMinimumSize = new Vector2(150, 44);
        _skipButton.Pressed += OnSkipPressed;
        bottomRow.AddChild(_skipButton);
    }

    private void ShowStep(TutorialStep step, int currentStep, int totalSteps)
    {
        _stepLabel.Text = $"Hướng dẫn tân thủ • Bước {currentStep}/{totalSteps}";
        _titleLabel.Text = step.Title;
        _bodyLabel.Text = step.Body;
        _hintLabel.Text = step.Hint;

        _panel.Modulate = new Color(1, 1, 1, 0f);
        var tw = CreateTween();
        tw.TweenProperty(_panel, "modulate:a", 1f, 0.2f);

        // Phát file âm thanh nếu có cấu hình VoicePath
        if (!string.IsNullOrEmpty(step.VoicePath))
        {
            if (ResourceLoader.Exists(step.VoicePath))
            {
                var stream = GD.Load<AudioStream>(step.VoicePath);
                _voicePlayer.Stream = stream;
                _voicePlayer.Play();
            }
            else
            {
                GD.PrintErr($"[TutorialManager] Không tìm thấy file âm thanh: {step.VoicePath}");
            }
        }
    }

    private void CaptureStepInput(string actionId)
    {
        if (actionId == "move")
        {
            if (!_moveDone && Mathf.Abs(Input.GetAxis("move_left", "move_right")) > 0.1f)
            {
                _moveDone = true;
            }
            return;
        }

        if (actionId == "jump" || actionId == "djump")
        {
            if (!_jumpDone && Input.IsActionJustPressed("jump"))
            {
                _jumpDone = true;
            }
            return;
        }

        if (actionId == "attack")
        {
            if (!_attackDone && Input.IsActionJustPressed("attack"))
            {
                _attackDone = true;
            }
            return;
        }

    }

    private void ResetStepFlags()
    {
        _moveDone = false;
        _jumpDone = false;
        _attackDone = false;
    }

    private void OnSkipPressed()
    {
        _skipRequested = true;
    }

    private void FinishTutorial()
    {
        if (IsInstanceValid(_player))
        {
            _player.ClearTutorialLock();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.IsTutorialRunning = false;
            GameManager.Instance.HasCompletedOnboardingTutorial = true;
        }

        QueueFree();
    }
}
