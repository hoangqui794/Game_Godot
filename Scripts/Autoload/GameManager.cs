using Godot;
using System;

public partial class GameManager : Node
{
    // Singleton
    public static GameManager Instance { get; private set; }

    // Game State
    public int Score { get; set; } = 0;
    public int CurrentLevel { get; set; } = 1;
    public int PlayerHealth { get; set; } = 100;
    public int MaxPlayerHealth { get; set; } = 100;
    public bool IsGameOver { get; set; } = false;
    public bool IsPaused { get; set; } = false;
    public bool HasBossKey { get; set; } = false;
    public int TotalKeys { get; set; } = 0;

    // Transition UI
    private CanvasLayer _transitionLayer;
    private ColorRect _transitionRect;
    private Label _transitionLabel;
    private bool _isTransitioning = false;

    // Level paths
    private readonly string[] _levelPaths = {
        "res://Scenes/Levels/Level1.tscn",
        "res://Scenes/Levels/Level2.tscn",
        "res://Scenes/Levels/Level3.tscn"
    };

    // Audio
    private AudioStreamPlayer _bgMusicPlayer;
    private AudioStream _menuMusic;
    private AudioStream _gameplayMusic;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Cấu hình âm nhạc toàn cục
        _menuMusic = GD.Load<AudioStream>("res://Assets/sound/little town - orchestral.ogg");
        _gameplayMusic = GD.Load<AudioStream>("res://Assets/sound/toocoolforwordsmix.ogg");

        _bgMusicPlayer = new AudioStreamPlayer();
        _bgMusicPlayer.VolumeDb = -10.0f;
        _bgMusicPlayer.Finished += () => _bgMusicPlayer.Play(); // Lặp lại
        AddChild(_bgMusicPlayer);

        // Tự động thêm Layer chuyển cảnh đen toàn cầu
        _transitionLayer = new CanvasLayer();
        _transitionLayer.Layer = 100; // Đảm bảo luôn nằm trên cùng

        _transitionRect = new ColorRect();
        _transitionRect.Color = new Color(0, 0, 0, 0);
        _transitionRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _transitionRect.MouseFilter = Control.MouseFilterEnum.Ignore;

        _transitionLabel = new Label();
        _transitionLabel.Text = "Đang tải...";
        _transitionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _transitionLabel.VerticalAlignment = VerticalAlignment.Center;
        _transitionLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _transitionLabel.AddThemeFontSizeOverride("font_size", 28);
        _transitionLabel.Visible = false;

        _transitionLayer.AddChild(_transitionRect);
        _transitionLayer.AddChild(_transitionLabel);
        AddChild(_transitionLayer);

        // Phát nhạc nếu không phải Intro
        CallDeferred(nameof(CheckInitialMusic));
    }

    private void CheckInitialMusic()
    {
        string path = GetTree().CurrentScene?.SceneFilePath;
        if (string.IsNullOrEmpty(path) || path == "res://Scenes/Main/Intro.tscn") return;

        bool isGameplay = path.Contains("Scenes/Levels/");
        PlayBackgroundMusic(isGameplay);
    }

    public void PlayBackgroundMusic(bool isGameplay)
    {
        AudioStream target = isGameplay ? _gameplayMusic : _menuMusic;

        if (_bgMusicPlayer.Stream != target)
        {
            _bgMusicPlayer.Stop();
            _bgMusicPlayer.Stream = target;
        }

        if (!_bgMusicPlayer.Playing) _bgMusicPlayer.Play();
    }

    public void StopBackgroundMusic()
    {
        if (_bgMusicPlayer.Playing) _bgMusicPlayer.Stop();
    }

    // ── Global Scene Transition ──────────────────────────────────
    public void ChangeSceneWithTransition(string path, bool showLoading = false)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        // Kiểm tra nhạc nền cho scene sắp tới
        if (path == "res://Scenes/Main/Intro.tscn")
        {
            StopBackgroundMusic();
        }
        else
        {
            bool isGameplay = path.Contains("Scenes/Levels/");
            PlayBackgroundMusic(isGameplay);
        }

        var tw = CreateTween();
        // 1. Fade màn hình sang đen (nhanh)
        tw.TweenProperty(_transitionRect, "color:a", 1.0f, 0.2f)
          .SetTrans(Tween.TransitionType.Quad)
          .SetEase(Tween.EaseType.In);

        // 2. Hiện "Đang tải..."
        tw.TweenCallback(Callable.From(() =>
        {
            if (showLoading) _transitionLabel.Visible = true;
        }));

        tw.TweenInterval(0.1f); // Cho user nhìn thấy để khỏi tưởng lag

        // 3. Chuyển scene trong khi màn hình vẫn đang đen thui!
        tw.TweenCallback(Callable.From(() =>
        {
            GetTree().ChangeSceneToFile(path);
        }));

        tw.TweenInterval(0.15f); // Đợi scene mới render frames đầu tiên dưới nền đen

        // 4. Fade từ đen thành trong suốt (hiện scene mới lên)
        tw.TweenCallback(Callable.From(() =>
        {
            _transitionLabel.Visible = false;
        }));
        tw.TweenProperty(_transitionRect, "color:a", 0.0f, 0.4f)
          .SetTrans(Tween.TransitionType.Quad)
          .SetEase(Tween.EaseType.Out);

        tw.TweenCallback(Callable.From(() => { _isTransitioning = false; }));
    }

    public void StartIntro()
    {
        Score = 0;
        CurrentLevel = 1;
        PlayerHealth = MaxPlayerHealth;
        IsGameOver = false;
        ChangeSceneWithTransition("res://Scenes/Main/Intro.tscn", showLoading: false);
    }

    public void StartGame()
    {
        Score = 0;
        CurrentLevel = 1;
        PlayerHealth = MaxPlayerHealth;
        IsGameOver = false;
        LoadLevel(CurrentLevel);
    }

    public void LoadLevel(int level)
    {
        CurrentLevel = level;
        if (level > _levelPaths.Length)
        {
            WinGame();
            return;
        }
        ChangeSceneWithTransition(_levelPaths[level - 1], showLoading: true);
    }

    public void NextLevel()
    {
        CurrentLevel++;
        LoadLevel(CurrentLevel);
    }

    public void AddScore(int points) => Score += points;

    public void PlayerTakeDamage(int damage)
    {
        PlayerHealth -= damage;
        if (PlayerHealth <= 0)
        {
            PlayerHealth = 0;
            GameOver();
        }
    }

    public void HealPlayer(int amount) => PlayerHealth = Math.Min(PlayerHealth + amount, MaxPlayerHealth);

    public void GameOver()
    {
        IsGameOver = true;
        ChangeSceneWithTransition("res://Scenes/Main/GameOver.tscn", showLoading: false);
    }

    public void WinGame()
    {
        ChangeSceneWithTransition("res://Scenes/Main/WinScreen.tscn", showLoading: false);
    }

    public void GoToMainMenu()
    {
        IsGameOver = false;
        Score = 0;
        CurrentLevel = 1;
        PlayerHealth = MaxPlayerHealth;
        ChangeSceneWithTransition("res://Scenes/Main/MainMenu.tscn", showLoading: false);
    }

    public void RestartLevel()
    {
        PlayerHealth = MaxPlayerHealth;
        IsGameOver = false;
        LoadLevel(CurrentLevel);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("pause")) TogglePause();
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        GetTree().Paused = IsPaused;
    }
}
