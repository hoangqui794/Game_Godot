using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class LevelManager3D : Node3D
{
    [Export] public int LevelNumber = 1;
    [Export] public PackedScene PlayerScene;

    private Node3D _spawnPoint;
    private Player3D _player;
    private List<Vector3> _checkpoints = new List<Vector3>();

    public async void FastRespawnPlayer()
    {
        if (IsInstanceValid(_player)) _player.QueueFree();
        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
        SpawnPlayer();
    }

    public override void _Ready()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.CurrentLevel = LevelNumber;

        if (LevelNumber == 1 && GameManager.Instance != null)
            GameManager.Instance.UnlockedSkillsCount = 0;

        if (HasNode("SpawnPoint"))
            _spawnPoint = GetNode<Node3D>("SpawnPoint");

        CollectCheckpoints();
        SpawnPlayer();
        ConnectPlayerSignals();
    }

    private void CollectCheckpoints()
    {
        _checkpoints.Clear();
        if (_spawnPoint != null) _checkpoints.Add(_spawnPoint.GlobalPosition);
        
        foreach (var child in GetChildren())
        {
            if (child is Marker3D marker && marker.Name.ToString().StartsWith("Checkpoint"))
            {
                _checkpoints.Add(marker.GlobalPosition);
            }
        }
        _checkpoints.Sort((a, b) => a.X.CompareTo(b.X));
    }

    private void SpawnPlayer()
    {
        int checkpointIndex = (GameManager.Instance != null) ? GameManager.Instance.CurrentCheckpointIndex : 0;

        Vector3 spawnPos = _checkpoints.Count > checkpointIndex
            ? _checkpoints[checkpointIndex]
            : (_spawnPoint?.GlobalPosition ?? Vector3.Zero);

        if (PlayerScene != null)
        {
            _player = PlayerScene.Instantiate<Player3D>();
            _player.GlobalPosition = spawnPos;
            _player.AddToGroup("player");
            AddChild(_player);
        }
    }

    private void ConnectPlayerSignals()
    {
        if (_player != null) _player.PlayerDied += OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        Engine.TimeScale = 1.0f;
        var timer = GetTree().CreateTimer(1.2, true, false, true);
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(this)) return;
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
        };
    }

    public override void _Process(double delta)
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        // Checkpoint detection
        for (int i = (GameManager.Instance != null ? GameManager.Instance.CurrentCheckpointIndex : 0) + 1; i < _checkpoints.Count; i++)
        {
            if (Mathf.Abs(_player.GlobalPosition.X - _checkpoints[i].X) < 2f) // Smaller threshold for 3D units
            {
                if (GameManager.Instance != null) GameManager.Instance.CurrentCheckpointIndex = i;
                GD.Print($"Checkpoint reached: {i}");
                break;
            }
        }
    }
}
