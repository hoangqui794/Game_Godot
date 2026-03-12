using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class LevelManager : Node2D
{
    [Export] public int LevelNumber = 1;
    [Export] public PackedScene PlayerScene;

    private Node2D _spawnPoint;
    private HUD _hud;
    private Player _player;
    private TutorialManager _tutorialManager;

    // --- Level 3 specific references ---
    private Node2D _princess;
    private Node2D _cage;
    private Node2D _boss;
    private bool _level3EnemiesCleared = false;
    private bool _level3BossSpawned = false;

    private List<Vector2> _checkpoints = new List<Vector2>();

    public override void _Ready()
    {
        GameManager.Instance.CurrentLevel = LevelNumber;

        // Giữ phần reset skill của bạn
        if (LevelNumber == 1)
        {
            GameManager.Instance.UnlockedSkillsCount = 0;
            GD.Print("Level 1: Reset UnlockedSkillsCount to 0");
        }
        else if (LevelNumber == 3 || LevelNumber == 4)
        {
            GameManager.Instance.UnlockedSkillsCount = 3;
            GD.Print($"Level {LevelNumber}: Unlock all skills (JKL)");
        }

        if (HasNode("SpawnPoint"))
            _spawnPoint = GetNode<Node2D>("SpawnPoint");

        CollectCheckpoints();
        SpawnPlayer();
        ConnectPlayerSignals();
        CallDeferred(nameof(TryStartBeginnerTutorial));

if (LevelNumber == 3)
{
    SetupLevel3();
}

        // Giữ phần bẫy đá từ GitHub
        if (LevelNumber == 1)
        {
            SpawnLevel1CustomTraps();
        }

        // Tự động biến các cục đá trang trí ở giữa đường thành chướng ngại vật (vật cản)
        MakeRocksSolidObstacles();
    }

    private void MakeRocksSolidObstacles()
    {
        foreach (Node child in GetChildren())
        {
            if (child is Sprite2D sprite && sprite.Name.ToString().StartsWith("Rock_D"))
            {
                // Kéo rock lên lớp z_index cao hơn để đứng ngang hàng với Player (-1 thay vì -11)
                sprite.ZIndex = -1;

                // Tạo vật lý cản đường (Environment layer = 2)
                var staticBody = new StaticBody2D();
                staticBody.CollisionLayer = 2;

                var collisionShape = new CollisionShape2D();
                var circleShape = new CircleShape2D();
                // Bán kính hình tròn cản (rock texture lớn, scale 0.3 -> effective radius ~ 35-40px)
                circleShape.Radius = 140f;
                collisionShape.Shape = circleShape;

                // Tâm của đá hơi nhích xuống dưới một chút để Player có thể nhảy lên hoặc đụng cạnh chặn lại
                collisionShape.Position = new Vector2(0, 30f);

                staticBody.AddChild(collisionShape);
                sprite.AddChild(staticBody);
            }
        }
    }

    private void SpawnLevel1CustomTraps()
    {
        var rock1 = new FallingRockTrap();
        rock1.Position = new Vector2(750, -50);
        rock1.TriggerRange = 600f;
        AddChild(rock1);

        var rock2 = new FallingRockTrap();
        rock2.Position = new Vector2(1750, 0);
        rock2.TriggerRange = 550f;
        AddChild(rock2);

        var rock3 = new FallingRockTrap();
        rock3.Position = new Vector2(3000, -100);
        rock3.TriggerRange = 650f;
        AddChild(rock3);
    }

    private void CollectCheckpoints()
    {
        _checkpoints.Clear();
        if (_spawnPoint != null) _checkpoints.Add(_spawnPoint.GlobalPosition);
        foreach (var child in GetChildren())
        {
            if (child is Marker2D marker && child.Name.ToString().StartsWith("Checkpoint"))
            {
                _checkpoints.Add(marker.GlobalPosition);
            }
        }
        _checkpoints.Sort((a, b) => a.X.CompareTo(b.X));
    }

    private void SpawnPlayer()
    {
        int checkpointIndex = GameManager.Instance.CurrentCheckpointIndex;

        Vector2 spawnPos = _checkpoints.Count > checkpointIndex
            ? _checkpoints[checkpointIndex]
            : (_spawnPoint?.GlobalPosition ?? Vector2.Zero);

        if (PlayerScene != null)
        {
            _player = PlayerScene.Instantiate<Player>();
            _player.GlobalPosition = spawnPos;
            _player.AddToGroup("player");
            AddChild(_player);

            if (LevelNumber == 4)
            {
                _player.Call("RefreshSkillUI");
            }
        }
    }

    private void ConnectPlayerSignals()
    {
        if (_player != null) _player.PlayerDied += OnPlayerDied;
    }

    private async void TryStartBeginnerTutorial()
    {
        if (LevelNumber != 1) return;
        if (_player == null || !IsInstanceValid(_player)) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.HasCompletedOnboardingTutorial) return;

        _tutorialManager = new TutorialManager();
        AddChild(_tutorialManager);

        await _tutorialManager.RunTutorial(_player);
    }

    public void ActivateCheckpoint(int index)
    {
        if (index > GameManager.Instance.CurrentCheckpointIndex && index < _checkpoints.Count)
        {
            GameManager.Instance.CurrentCheckpointIndex = index;
            GD.Print($"Đã lưu Checkpoint: {index}");
        }
    }

    // --- Level 3 Specific Setup ---
private void SetupLevel3()
{
    _princess = GetNodeOrNull<Node2D>("Princess");
    _cage = GetNodeOrNull<Node2D>("BossCage");
    _boss = GetNodeOrNull<Node2D>("ChanTinh");

    if (_princess != null) _princess.Visible = false;
    if (_cage != null) _cage.Visible = false;
    if (_boss != null)
    {
        _boss.Visible = false;
        _boss.ProcessMode = ProcessModeEnum.Disabled; // Don't let it act yet
    }
    
    GD.Print("Level 3 Setup: Hidden Princess, Cage, and Boss.");
}

private void ProcessLevel3Logic()
{
    if (!_level3EnemiesCleared)
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        bool anySmallEnemies = false;
        foreach (Node item in enemies)
        {
            if (item != _boss && IsInstanceValid(item) && !item.IsQueuedForDeletion())
            {
                anySmallEnemies = true;
                break;
            }
        }

        if (!anySmallEnemies)
        {
            _level3EnemiesCleared = true;
            if (_princess != null) _princess.Visible = true;
            if (_cage != null) _cage.Visible = true;
            GD.Print("Level 3: All small enemies defeated! Princess appeared.");
        }
    }
    else if (!_level3BossSpawned)
    {
        if (_player != null && _princess != null)
        {
            float dist = _player.GlobalPosition.DistanceTo(_princess.GlobalPosition);
            if (dist < 400f) // Threshold to spawn Boss
            {
                _level3BossSpawned = true;
                if (_boss != null)
                {
                    _boss.Visible = true;
                    _boss.ProcessMode = ProcessModeEnum.Inherit;
                    GD.Print("Level 3: Player approached Princess! Boss Chan Tinh spawned!");
                }
            }
        }
    }
}

public void FastRespawnPlayer()
    {
        if (_player != null && IsInstanceValid(_player))
        {
            // 1. Reset vị trí về Checkpoint gần nhất
            int checkpointIndex = GameManager.Instance.CurrentCheckpointIndex;
            Vector2 spawnPos = _checkpoints.Count > checkpointIndex
                ? _checkpoints[checkpointIndex]
                : (_spawnPoint?.GlobalPosition ?? Vector2.Zero);

            _player.GlobalPosition = spawnPos;

            // 2. Reset trạng thái nhân vật (Máu, sống lại) thông qua call method trong Player.cs
            _player.Call("FastReset");
            // Double-check: give a brief invulnerability window from level manager as well.
            if (_player.HasMethod("StartInvulnerability"))
            {
                _player.Call("StartInvulnerability", 1.0f);
            }

            GD.Print("Đã hồi sinh nhanh tại chỗ!");
        }
        else
        {
            // Nếu không tìm thấy player (ví dụ lỡ bị xóa), thì spawn mới
            SpawnPlayer();
        }
    }

    private void OnPlayerDied()
    {
        Engine.TimeScale = 1.0f;
        var timer = GetTree().CreateTimer(1.2, true, false, true);
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(this)) return;
            GameManager.Instance.GameOver();
        };
    }

    public override void _Process(double delta)
    {
        if (_player == null || _player.IsQueuedForDeletion()) return;

        if (LevelNumber == 3)
        {
            ProcessLevel3Logic();
        }

        for (int i = GameManager.Instance.CurrentCheckpointIndex + 1; i < _checkpoints.Count; i++)
        {
            if (Mathf.Abs(_player.GlobalPosition.X - _checkpoints[i].X) < 60f)
            {
                ActivateCheckpoint(i);
                break;
            }
        }
    }
}