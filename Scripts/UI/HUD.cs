using Godot;

public partial class HUD : CanvasLayer
{
    private ProgressBar _healthBar;
    private Label _scoreLabel;
    private Label _levelLabel;
    private Panel _pausePanel;
    private Player _player;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("MarginContainer/HBoxContainer/HealthBar");
        _scoreLabel = GetNode<Label>("MarginContainer/HBoxContainer/ScoreLabel");
        _levelLabel = GetNode<Label>("MarginContainer/HBoxContainer/LevelLabel");

        // Setup pause panel
        if (HasNode("PausePanel"))
        {
            _pausePanel = GetNode<Panel>("PausePanel");
            _pausePanel.Visible = false;
        }

        UpdateUI();
    }

    public override void _Process(double delta)
    {
        // Find player if not found
        if (_player == null)
        {
            var playerNode = GetTree().GetFirstNodeInGroup("player");
            if (playerNode is Player p)
            {
                _player = p;
                _player.HealthChanged += OnHealthChanged;
            }
        }

        UpdateUI();

        if (_pausePanel != null)
        {
            _pausePanel.Visible = GameManager.Instance.IsPaused;
        }
    }

    private void UpdateUI()
    {
        if (_healthBar != null)
        {
            _healthBar.MaxValue = GameManager.Instance.MaxPlayerHealth;
            _healthBar.Value = GameManager.Instance.PlayerHealth;

            // Set màu cho thanh máu
            float percent = (float)GameManager.Instance.PlayerHealth / GameManager.Instance.MaxPlayerHealth;
            Color color;
            if (percent > 0.66f)
            {
                color = Colors.Green;
            }
            else if (percent > 0.33f)
            {
                color = Colors.Yellow;
            }
            else
            {
                color = Colors.Red;
            }
            _healthBar.Modulate = color;
        }

        if (_scoreLabel != null)
        {
            _scoreLabel.Text = $"Điểm: {GameManager.Instance.Score}";
        }

        if (_levelLabel != null)
        {
            string levelName = GameManager.Instance.CurrentLevel switch
            {
                1 => "Đường Rừng Hiểm Trở",
                2 => "Hang Tối Hiểm Nguy",
                3 => "Đại Chiến Chằn Tinh",
                _ => $"Level {GameManager.Instance.CurrentLevel}"
            };
            _levelLabel.Text = levelName;
        }
    }

    private void OnHealthChanged(int newHealth, int maxHealth)
    {
        if (_healthBar != null)
        {
            _healthBar.MaxValue = maxHealth;
            _healthBar.Value = newHealth;

            // Chia thành 3 tầng màu dựa trên ngưỡng máu
            float percent = (float)newHealth / maxHealth;
            Color color;
            if (percent > 0.66f)
            {
                color = Colors.Green; // Xanh lá khi >66%
            }
            else if (percent > 0.33f)
            {
                color = Colors.Yellow; // Vàng khi 33-66%
            }
            else
            {
                color = Colors.Red; // Đỏ khi <33%
            }
            _healthBar.Modulate = color;
        }
    }
}
