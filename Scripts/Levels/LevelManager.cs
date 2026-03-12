using Godot;
using System.Collections.Generic;

public partial class LevelManager : Node2D
{
    [Export] public int LevelNumber = 1;
    [Export] public PackedScene PlayerScene;

    private Node2D _spawnPoint;
    private HUD _hud;
    private Player _player;

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
        else if (LevelNumber == 4)
        {
            GameManager.Instance.UnlockedSkillsCount = 3;
            GD.Print("Level 4: Unlock all skills (JKL)");
        }

        if (HasNode("SpawnPoint"))
            _spawnPoint = GetNode<Node2D>("SpawnPoint");

        CollectCheckpoints();
        SpawnPlayer();
        ConnectPlayerSignals();

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

        // Force the player to start at the beginning of levels 2, 3 and 4
        if (LevelNumber >= 2 && LevelNumber <= 4)
        {
            checkpointIndex = 0;
            GameManager.Instance.CurrentCheckpointIndex = 0;
        }

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

    public void ActivateCheckpoint(int index)
    {
        if (index > GameManager.Instance.CurrentCheckpointIndex && index < _checkpoints.Count)
        {
            GameManager.Instance.CurrentCheckpointIndex = index;
            GD.Print($"Đã lưu Checkpoint: {index}");
        }
    }

    private void OnPlayerDied()
    {
        var timer = GetTree().CreateTimer(1.2);
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(this)) return;
            GameManager.Instance.GameOver();
        };
    }

    public override void _Process(double delta)
    {
        if (_player == null || _player.IsQueuedForDeletion()) return;
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