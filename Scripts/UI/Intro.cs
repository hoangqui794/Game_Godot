using Godot;
using System;
using System.Collections.Generic;

public partial class Intro : Control
{
	private TextureRect _bgRect;
	private Texture2D[] _bgTextures;
	private AudioStreamPlayer _audioPlayer;
	private AudioStreamPlayer _musicPlayer;
	private Button _skipButton;
	private SceneTreeTimer _nextSlideTimer;

	private int _currentLine = 0;
	private float _timer = 0;
	private float _totalDuration = 48f;
	private float _introTimer = 0f;
	private const float IntroSpeedMultiplier = 1.25f;

	// Thời lượng hiển thị cho từng câu (giây) để chuyển Scene nền khớp với giọng đọc
	private float[] _lineDurations = new float[] { 6.8f, 6.5f, 6.5f, 6.5f, 6.5f, 6.5f, 8.0f };

	public override void _Ready()
	{
		_bgRect = GetNode<TextureRect>("BackgroundRect");
		_audioPlayer = GetNode<AudioStreamPlayer>("IntroAudio");
		_musicPlayer = GetNode<AudioStreamPlayer>("MusicPlayer");

		// Lặp lại nhạc nền
		_musicPlayer.Finished += () => _musicPlayer.Play();
		_audioPlayer.PitchScale = IntroSpeedMultiplier;
		_musicPlayer.PitchScale = IntroSpeedMultiplier;

		// Load 6 hình nền cho Intro
		_bgTextures = new Texture2D[6];
		for (int i = 0; i < 6; i++)
		{
			_bgTextures[i] = GD.Load<Texture2D>($"res://Assets/Sprites/Backgrounds/{i + 1}.jpeg");
		}

		_bgRect.Modulate = new Color(1, 1, 1, 0);

		// ✅ PRELOAD LEVEL 1 TỪ SỚM khi Intro chạy
		// Như vậy khi nhấn BỎ QUA, cấp sẽ đã load xong → vào game ngay tức thì!
		GameManager.Instance.PreloadScene("res://Scenes/Levels/Level1.tscn");

		// Phát lời thoại
		_audioPlayer.Play();

		// Bắt đầu chuỗi chuyển cảnh
		ShowNextLine();

		// ── TẠO NÚT BỎ QUA (SKIP) BẰNG CODE ────────────────────────
		_skipButton = new Button();
		_skipButton.Text = "BỎ QUA >>";
		_skipButton.CustomMinimumSize = new Vector2(150, 50);

		// Căn vị trí góc trên bên phải
		_skipButton.SetPosition(new Vector2(1152 - 170, 20));

		// Style cho nút (Nền đen mờ, bo tròn)
		var styleNormal = new StyleBoxFlat();
		styleNormal.BgColor = new Color(0, 0, 0, 0.4f);
		styleNormal.SetCornerRadiusAll(10);
		styleNormal.ExpandMarginLeft = 10;
		styleNormal.ExpandMarginRight = 10;

		var styleHover = (StyleBoxFlat)styleNormal.Duplicate();
		styleHover.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);

		_skipButton.AddThemeStyleboxOverride("normal", styleNormal);
		_skipButton.AddThemeStyleboxOverride("hover", styleHover);
		_skipButton.AddThemeStyleboxOverride("pressed", styleHover);
		_skipButton.AddThemeFontSizeOverride("font_size", 20);

		_skipButton.Pressed += StartGame;
		AddChild(_skipButton);

		// Hiệu ứng nút nhấp nháy nhẹ để thu hút sự chú ý
		var skipTw = CreateTween().SetLoops();
		skipTw.TweenProperty(_skipButton, "modulate:a", 0.7f, 0.8f / IntroSpeedMultiplier);
		skipTw.TweenProperty(_skipButton, "modulate:a", 1.0f, 0.8f / IntroSpeedMultiplier);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_timer += dt;
		_introTimer += dt;

		// Nếu hết tiếng hoặc hết thời gian thì vào game
		if (_introTimer >= (_totalDuration / IntroSpeedMultiplier) || (_introTimer > (5f / IntroSpeedMultiplier) && !_audioPlayer.Playing))
		{
			StartGame();
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Echo) return;
		if (!@event.IsPressed()) return;

		// Cho phép bỏ qua Intro bằng phím ESC, SPACE, hoặc Enter
		if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("ui_accept") || @event.IsActionPressed("jump"))
		{
			GetViewport().SetInputAsHandled();
			StartGame();
		}
	}

	private void ShowNextLine()
	{
		// Kiểm tra an toàn: Nếu màn hình Intro đã bị đóng (do bấm Skip hoặc hết giờ) 
		// thì không chạy tiếp logic chuyển cảnh nữa để tránh crash.
		if (!IsInstanceValid(this) || _bgRect == null || !IsInstanceValid(_bgRect)) return;

		if (_currentLine >= _lineDurations.Length) return;

		float duration = _lineDurations[_currentLine] / IntroSpeedMultiplier;

		// 6 ảnh cho 7 đoạn thoại -> đoạn cuối giữ nguyên ảnh cuối
		int bgIndex = Math.Min(_currentLine, 5);
		Texture2D nextBg = _bgTextures[bgIndex];

		_currentLine++;

		// 🟢 Tween cho BACKGROUND (Chuyển Scene mượt mà)
		if (_bgRect.Texture != nextBg)
		{
			var bgTw = CreateTween();
			bgTw.TweenProperty(_bgRect, "modulate:a", 0.5f, 0.5f / IntroSpeedMultiplier);
			bgTw.TweenCallback(Callable.From(() => _bgRect.Texture = nextBg));
			bgTw.TweenProperty(_bgRect, "modulate:a", 1.0f, 1.0f / IntroSpeedMultiplier);
		}

		// Tự động chuyển slide theo thời gian đã định
		_nextSlideTimer = GetTree().CreateTimer(duration);
		_nextSlideTimer.Timeout += () => ShowNextLine();
	}

	private void StartGame()
	{
		if (!IsInstanceValid(this)) return;

		// Dừng nhạc và hội thoại ngay lập tức
		if (_audioPlayer != null && _audioPlayer.Playing) _audioPlayer.Stop();
		if (_musicPlayer != null && _musicPlayer.Playing) _musicPlayer.Stop();

		SetProcess(false);
		GameManager.Instance.StartGame();
	}
}
