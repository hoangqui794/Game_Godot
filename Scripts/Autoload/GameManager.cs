using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    // ── Game State ───────────────────────────────────────────────────────────
    public int Score { get; set; } = 0;
    public int CurrentLevel { get; set; } = 1;
    public int CurrentCheckpointIndex { get; set; } = 0;
    public int PlayerHealth { get; set; } = 100;
    public int MaxPlayerHealth { get; set; } = 100;
    public bool IsGameOver { get; set; } = false;
    public bool IsPaused { get; set; } = false;
    public bool HasBossKey { get; set; } = false;
    public int TotalKeys { get; set; } = 0;
    public int UnlockedSkillsCount { get; set; } = 0;
    public int PlayerLives { get; set; } = 3; // Mạng của nhân vật
    public bool HasCompletedOnboardingTutorial { get; set; } = false;
    public bool IsTutorialRunning { get; set; } = false;

    // ── Transition UI ────────────────────────────────────────────────────────
    private CanvasLayer _transitionLayer;
    private ColorRect _transitionRect;
    private Label _transitionLabel;
    private Label _respawnLabel; // Label hồi sinh
    private ProgressBar _loadingBar;
    private bool _isTransitioning = false;
    // Flag used to prevent GameOver being processed multiple times concurrently
    private bool _gameOverInProgress = false;

    private readonly string[] _levelPaths = {
        "res://Scenes/Levels/Level1.tscn",
        "res://Scenes/Levels/Level2.tscn",
        "res://Scenes/Levels/Level3.tscn"
    };

    private AudioStreamPlayer _bgMusicPlayer;
    private AudioStream _menuMusic;
    private AudioStream _gameplayMusic;

    private string _targetPath = "";
    private bool _useThreadedLoading = false;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        _menuMusic = GD.Load<AudioStream>("res://Assets/sound/little town - orchestral.ogg");
        _gameplayMusic = GD.Load<AudioStream>("res://Assets/sound/toocoolforwordsmix.ogg");

        _bgMusicPlayer = new AudioStreamPlayer { VolumeDb = -10.0f };
        _bgMusicPlayer.Finished += () => _bgMusicPlayer.Play();
        AddChild(_bgMusicPlayer);

        InitUI();
        CallDeferred(nameof(CheckInitialMusic));
    }

    private void InitUI()
    {
        _transitionLayer = new CanvasLayer { Layer = 100 };

        _transitionRect = new ColorRect
        {
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _transitionRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _transitionLayer.AddChild(_transitionRect);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        _transitionLayer.AddChild(centerContainer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 25);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        centerContainer.AddChild(vbox);

        _transitionLabel = new Label
        {
            Text = "Đang tải dữ liệu...",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false
        };
        _transitionLabel.AddThemeFontSizeOverride("font_size", 36);
        vbox.AddChild(_transitionLabel);

        _respawnLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false
        };
        _respawnLabel.AddThemeFontSizeOverride("font_size", 48);
        _respawnLabel.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.2f)); // Màu vàng cho nổi
        vbox.AddChild(_respawnLabel);

        _loadingBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(500, 15),
            Visible = false,
            ShowPercentage = false
        };

        var styleBg = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f), CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 };
        var styleFg = new StyleBoxFlat { BgColor = new Color(0.2f, 0.8f, 0.4f, 1.0f), CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 };
        _loadingBar.AddThemeStyleboxOverride("background", styleBg);
        _loadingBar.AddThemeStyleboxOverride("fill", styleFg);
        vbox.AddChild(_loadingBar);

        AddChild(_transitionLayer);
    }

    public override void _Process(double delta)
    {
        if (!_useThreadedLoading || string.IsNullOrEmpty(_targetPath)) return;

        var progress = new Godot.Collections.Array();
        var status = ResourceLoader.LoadThreadedGetStatus(_targetPath, progress);

        if (status == ResourceLoader.ThreadLoadStatus.Loaded)
        {
            _useThreadedLoading = false;
            var packed = (PackedScene)ResourceLoader.LoadThreadedGet(_targetPath);
            _loadingBar.Value = 100;
            FinishSwitch(packed);
        }
        else if (status == ResourceLoader.ThreadLoadStatus.InProgress && progress.Count > 0)
        {
            _loadingBar.Value = (float)progress[0] * 100;
        }
    }

    public async void ChangeSceneWithTransition(string path, bool isLevel = true)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        _targetPath = path;

        var tw = CreateTween();
        tw.TweenProperty(_transitionRect, "color:a", 1.0f, 0.25f);
        await ToSignal(tw, "finished");

        _transitionLabel.Visible = true;
        _loadingBar.Visible = true;
        _loadingBar.Value = 0;

        if (isLevel)
        {
            _useThreadedLoading = true;
            ResourceLoader.LoadThreadedRequest(path, "", true);
        }
        else
        {
            await Task.Delay(300);
            GetTree().ChangeSceneToFile(path);
            _isTransitioning = false;
            _transitionLabel.Visible = false;
            _loadingBar.Visible = false;
            var twIn = CreateTween();
            twIn.TweenProperty(_transitionRect, "color:a", 0.0f, 0.3f);
        }
    }

    private async void FinishSwitch(PackedScene packed)
    {
        GetTree().ChangeSceneToPacked(packed);
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), "process_frame");
        _transitionLabel.Visible = false;
        _loadingBar.Visible = false;
        var tw = CreateTween();
        tw.TweenProperty(_transitionRect, "color:a", 0.0f, 0.4f);
        tw.TweenCallback(Callable.From(() => { _isTransitioning = false; }));
    }

    // ── Game Flow Logic ──────────────────────────────────────────────────────
    public void StartIntro() { ResetStats(); ChangeSceneWithTransition("res://Scenes/Main/Intro.tscn", false); }
    public void StartGame() { ResetStats(); LoadLevel(1); }
    public void LoadLevel(int level)
    {
        if (level > _levelPaths.Length) { WinGame(); return; }
        CurrentLevel = level;
        ChangeSceneWithTransition(_levelPaths[level - 1], true);
    }
    public void RestartLevel()
    {
        // ✅ QUAN TRỌNG: Reset máu, checkpoint và trạng thái thua trước khi chơi lại
        PlayerHealth = MaxPlayerHealth;
        CurrentCheckpointIndex = 0; // Reset về đầu map
        IsGameOver = false;
        LoadLevel(CurrentLevel);
    }

    public void NextLevel()
    {
        CurrentLevel++;
        CurrentCheckpointIndex = 0; // Reset checkpoint for the new level
        LoadLevel(CurrentLevel);
    }
    public async void GameOver()
    {
        // prevent reentry
        if (_gameOverInProgress)
        {
            GD.Print("[GameManager] GameOver called again while already processing, ignoring.");
            return;
        }
        _gameOverInProgress = true;

        GD.Print($"[GameManager] GameOver called. Lives before processing: {PlayerLives}");
        Engine.TimeScale = 1.0f;
        IsPaused = false;
        GetTree().Paused = false;

        // guard: if we've already entered a game-over/respawn flow, ignore extra calls
        if (_isTransitioning)
        {
            GD.Print("[GameManager] GameOver ignored because transition is already in progress.");
            _gameOverInProgress = false;
            return;
        }

        if (PlayerLives > 1)
        {
            // --- HỒI SINH NHANH TRONG MÀN (KHÔNG LOAD LẠI) ---
            PlayerLives--;
            CurrentCheckpointIndex = 0; // Reset checkpoint về 0 để quay lại đầu map
            await ShowRespawnSequence();
            // Lệnh hồi sinh được gọi ngay ở cuối ShowRespawnSequence
            _gameOverInProgress = false;
        }
        else
        {
            // --- HẾT MẠNG - QUAY VỀ MÀN 1 ---
            IsGameOver = true;
            PlayerLives = 3; // Reset lại mạng cho lần chơi sau
            ChangeSceneWithTransition("res://Scenes/Main/GameOver.tscn", false);
            _gameOverInProgress = false;
        }
    }

    private async Task ShowRespawnSequence()
    {
        _transitionRect.Color = new Color(0, 0, 0, 0);
        var tw = CreateTween();
        tw.TweenProperty(_transitionRect, "color:a", 1.0f, 0.3f);
        await ToSignal(tw, "finished");

        _respawnLabel.Visible = true;
        for (int i = 3; i > 0; i--)
        {
            _respawnLabel.Text = $"Hồi sinh sau... {i}";
            await Task.Delay(1000);
        }
        _respawnLabel.Visible = false;

        // --- GỌI HÀM HỒI SINH NHANH TỪ LEVEL MANAGER ---
        var levelManager = GetTree().CurrentScene as LevelManager;
        if (levelManager != null)
        {
            levelManager.FastRespawnPlayer();
        }

        // Mờ dần màn đen trả lại game
        var twHide = CreateTween();
        twHide.TweenProperty(_transitionRect, "color:a", 0.0f, 0.4f);
        await ToSignal(twHide, "finished");
        _isTransitioning = false;
        _gameOverInProgress = false; // clear flag after respawn flow finished
    }

    public void WinGame() => ChangeSceneWithTransition("res://Scenes/Main/WinScreen.tscn", false);
    public void GoToMainMenu() { IsGameOver = false; PlayerLives = 3; ChangeSceneWithTransition("res://Scenes/Main/MainMenu.tscn", false); }

    private void ResetStats()
    {
        Score = 0;
        CurrentLevel = 1;
        CurrentCheckpointIndex = 0;
        PlayerHealth = MaxPlayerHealth;
        IsGameOver = false;
        PlayerLives = 3;
        UnlockedSkillsCount = 0;
        HasBossKey = false;
        TotalKeys = 0;
        HasCompletedOnboardingTutorial = false;
        IsTutorialRunning = false;
    }

    // ── Required Utilities (FIX MISSING METHODS) ─────────────────────────────
    public void PreloadScene(string path) { ResourceLoader.LoadThreadedRequest(path, "", true); }
    public void StopBackgroundMusic() => _bgMusicPlayer.Stop();
    public void UnlockNextSkill() { if (UnlockedSkillsCount < 3) (GetTree().GetFirstNodeInGroup("player"))?.Call("RefreshSkillUI"); }
    public void AddScore(int p) => Score += p;
    public void PlayerTakeDamage(int d) { PlayerHealth -= d; if (PlayerHealth <= 0) GameOver(); }
    public void HealPlayer(int a) => PlayerHealth = Math.Min(PlayerHealth + a, MaxPlayerHealth);
    public void PlayBackgroundMusic(bool isG)
    {
        AudioStream t = isG ? _gameplayMusic : _menuMusic;
        if (_bgMusicPlayer.Stream != t) { _bgMusicPlayer.Stop(); _bgMusicPlayer.Stream = t; }
        if (!_bgMusicPlayer.Playing) _bgMusicPlayer.Play();
    }
    private void CheckInitialMusic() { string p = GetTree().CurrentScene?.SceneFilePath; if (!string.IsNullOrEmpty(p)) PlayBackgroundMusic(p.Contains("Scenes/Levels/")); }
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey keyEvent && keyEvent.Echo) return;
        if (!e.IsPressed()) return;

        if (e.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            TogglePause();
        }
    }
    public void TogglePause() { IsPaused = !IsPaused; GetTree().Paused = IsPaused; }
}
