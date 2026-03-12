using Godot;
using System.Collections.Generic;

public partial class Princess : Area2D
{
    [Export] public bool RequireAllEnemiesDefeated = true;

    private AnimatedSprite2D _animSprite;
    private Label _messageLabel;
    private bool _isRescued = false;

    [Signal] public delegate void PrincessRescuedEventHandler();

    public override void _Ready()
    {
        _animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Auto-create sprites if none assigned
        if (_animSprite.SpriteFrames == null)
        {
            CreatePlaceholderSprites();
        }

        // Create message label
        _messageLabel = new Label();
        _messageLabel.Text = "Hãy đánh bại hết quái vật!";
        _messageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _messageLabel.Position = new Vector2(-100, -70);
        _messageLabel.Visible = false;
        _messageLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        _messageLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_messageLabel);

        BodyEntered += OnBodyEntered;
        _animSprite.Play("idle");
    }

    private void CreatePlaceholderSprites()
    {
        _animSprite.SpriteFrames = SpriteHelper.CreatePrincessSpriteFrames();
        _animSprite.Play("idle");
    }

    public override void _Process(double delta)
    {
        if (!_isRescued)
        {
            _animSprite.Play("idle");
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_isRescued) return;
        if (body is not Player) return;

        if (RequireAllEnemiesDefeated)
        {
            // Check if all enemies are defeated
            var enemies = GetTree().GetNodesInGroup("enemies");
            if (enemies.Count > 0)
            {
                // Show message
                _messageLabel.Visible = true;
                var tween = CreateTween();
                tween.TweenInterval(2.0);
                tween.TweenCallback(Callable.From(() => { _messageLabel.Visible = false; }));
                return;
            }
        }

        RescuePrincess();
    }

    private async void RescuePrincess()
    {
        _isRescued = true;
        _animSprite.Play("rescued");
        _messageLabel.Text = "Cảm ơn Thạch Sanh! ❤️";
        _messageLabel.Visible = true;

        EmitSignal(SignalName.PrincessRescued);

        var dm = new DialogueManager();
        AddChild(dm);
        var lines = new List<DialogueManager.DialogueLine>
        {
            new DialogueManager.DialogueLine("Công Chúa", "Chàng thật sự đến rồi, Cảm ơn Chàng đã cứu ta, Chàng thật dũng cảm.", null, "res://Assets/Audio/Voices/princess_free1.mp3"),
            new DialogueManager.DialogueLine("Thạch Sanh", "Người vô tội không nên bị giam cầm. Đó là lý do duy nhất ta vào đây. Không có gì cao cả hơn thế.", null, "res://Assets/Audio/Voices/ts_end_princess.mp3"),
            new DialogueManager.DialogueLine("Ngọc Hoàng", "Chúc mừng ngươi, không chỉ bằng sức mạnh, mà bằng lòng ngay thẳng không đổi. Giờ đây ngươi hãy trở về và nhận được những thứ đáng được hưởng.", null, "res://Assets/Audio/Voices/god_end_win2.mp3"),
            new DialogueManager.DialogueLine("Công Chúa", "Cảm ơn chàng, Thạch Sanh. Cảm ơn chàng rất nhiều.", null, "res://Assets/Audio/Voices/princess_free2.mp3")
        };
        await dm.PlayDialogue(lines);

        GameManager.Instance.AddScore(500);
        GameManager.Instance.NextLevel();
    }
}
