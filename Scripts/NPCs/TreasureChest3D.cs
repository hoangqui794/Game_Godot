using Godot;

/// <summary>
/// Rương kho báu 3D - Kiểm tra quái chết hết thì mở, cho điểm, chuyển màn.
/// Phiên bản đơn giản hóa cho 2.5D, giữ lại core logic.
/// </summary>
public partial class TreasureChest3D : Area3D
{
    [Export] public bool RequireAllEnemiesDefeated = true;

    private AnimatedSprite3D _animSprite;
    private Label3D _messageLabel;
    private bool _isOpened = false;

    public override void _Ready()
    {
        _animSprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        if (_animSprite.SpriteFrames == null) CreatePlaceholderSprites();

        _messageLabel = new Label3D();
        _messageLabel.Text = "Hãy đánh bại hết quái vật!";
        _messageLabel.Position = new Vector3(0, 1.5f, 0);
        _messageLabel.Visible = false;
        _messageLabel.FontSize = 48;
        _messageLabel.Modulate = Colors.Yellow;
        _messageLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _messageLabel.PixelSize = 0.01f;
        AddChild(_messageLabel);

        BodyEntered += OnBodyEntered;
        _animSprite.Play("idle");

        if (RequireAllEnemiesDefeated)
            Visible = false;
    }

    private void CreatePlaceholderSprites()
    {
        try
        {
            var chestClosed = GD.Load<Texture2D>("res://Assets/Sprites/Environment/treasure_chest_closed.png");
            var chestOpened = GD.Load<Texture2D>("res://Assets/Sprites/Environment/treasure_chest_open.png");

            if (chestClosed != null && chestOpened != null)
            {
                var frames = new SpriteFrames();
                frames.AddAnimation("idle");
                frames.AddFrame("idle", chestClosed);
                frames.AddAnimation("opened");
                frames.AddFrame("opened", chestOpened);
                _animSprite.SpriteFrames = frames;
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr("TreasureChest3D: " + e.Message);
        }
    }

    public override void _Process(double delta)
    {
        if (_isOpened) return;

        // Check if all enemies are dead for auto-reveal
        if (RequireAllEnemiesDefeated && !Visible)
        {
            var enemies = GetTree().GetNodesInGroup("enemies");
            bool anyAlive = false;
            foreach (var n in enemies)
            {
                if (n is BaseEnemy3D e && !e.IsDead) { anyAlive = true; break; }
            }
            if (!anyAlive && enemies.Count > 0)
            {
                Visible = true;
                var tw = CreateTween();
                tw.TweenProperty(this, "scale", Vector3.One, 0.5f).From(Vector3.Zero);
            }
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_isOpened || !body.IsInGroup("player")) return;

        if (!RequireAllEnemiesDefeated)
        {
            OpenChest();
            return;
        }

        var enemies = GetTree().GetNodesInGroup("enemies");
        int aliveCount = 0;
        foreach (var n in enemies)
            if (n is BaseEnemy3D e && !e.IsDead) aliveCount++;

        if (aliveCount == 0)
        {
            Visible = true;
            OpenChest();
        }
        else
        {
            _messageLabel.Text = $"Còn {aliveCount} quái vật!";
            _messageLabel.Visible = true;
        }
    }

    private void OpenChest()
    {
        if (_isOpened) return;
        _isOpened = true;
        _messageLabel.Visible = false;

        _animSprite.Play("opened");
        GameManager.Instance?.AddScore(500);

        // Simple tween animation
        var tw = CreateTween();
        tw.TweenProperty(_animSprite, "offset:y", 40f, 0.3f).SetTrans(Tween.TransitionType.Bounce);

        // After opening, unlock skills / keys based on level
        var timer = GetTree().CreateTimer(1.0f);
        timer.Timeout += () =>
        {
            if (GameManager.Instance == null) return;
            int level = GameManager.Instance.CurrentLevel;
            if (level == 1)
            {
                GameManager.Instance.UnlockedSkillsCount = 2;
                GameManager.Instance.TotalKeys++;
            }
            else if (level == 2)
            {
                GameManager.Instance.UnlockedSkillsCount = 3;
                GameManager.Instance.TotalKeys++;
            }
            else if (level == 3)
            {
                GameManager.Instance.HasBossKey = true;
                GameManager.Instance.TotalKeys++;
            }

            // Transition to next level after delay
            var t2 = GetTree().CreateTimer(2.0f);
            t2.Timeout += () => GameManager.Instance?.NextLevel();
        };
    }
}
