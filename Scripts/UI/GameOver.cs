using Godot;

/// <summary>
/// Màn Game Over
/// SPACE / Enter         → Chơi lại
/// ESC                   → Về Menu
/// Click nút THỬ LẠI     → Chơi lại
/// Click nút VỀ MENU     → Về Menu
/// </summary>
public partial class GameOver : Control
{
    private Label _scoreLabel;
    private Button _retryButton;
    private Button _menuButton;
    private bool _inputEnabled = false;

    public override void _Ready()
    {
        // ── Liên kết node từ .tscn ──────────────────────────────
        _scoreLabel = GetNode<Label>("ScoreLabel");
        _retryButton = GetNode<Button>("RetryButton");
        _menuButton = GetNode<Button>("MenuButton");

        // Set state ban đầu
        _scoreLabel.Text = $"Điểm: {GameManager.Instance.Score}";

        // Bắt sự kiện click vào nút (người chơi bắt buộc phải click TRÚNG nút)
        _retryButton.Pressed += () =>
        {
            if (_inputEnabled) GameManager.Instance.StartGame(); // Hết mạng thì phải chơi lại từ Map 1
        };

        _menuButton.Pressed += () =>
        {
            if (_inputEnabled) GameManager.Instance.GoToMainMenu();
        };

        _retryButton.GrabFocus();

        // ── Bảo vệ chống bấm nhầm lập tức khi vừa chết ──────────
        // Chờ 0.5s mới cho phép nhận tín hiệu phím / chuột để tránh việc
        // người chơi đang giữ nút nhảy/đánh lúc chết làm văng game ngay lập tức.
        var tw = CreateTween();
        tw.TweenInterval(0.5f);
        tw.TweenCallback(Callable.From(() =>
        {
            _inputEnabled = true;
        }));
    }

    public override void _Input(InputEvent @event)
    {
        if (!_inputEnabled) return;
        if (@event is InputEventKey keyEvent && keyEvent.Echo) return;
        if (!@event.IsPressed()) return;

        // Chỉ nhận Nhấn Phím SPACE, ENTER để chơi lại
        // (Bỏ bắt sự kiện Click chuột trái toàn màn hình ở đây, tránh bị đè lên nút Về Menu)
        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("jump"))
        {
            GetViewport().SetInputAsHandled();
            GameManager.Instance.StartGame(); // Hết mạng thì phải chơi lại từ Map 1
        }
        // ESC -> Menu Chính
        else if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            GameManager.Instance.GoToMainMenu();
        }
    }
}
