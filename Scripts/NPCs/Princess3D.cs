using Godot;
using System.Collections.Generic;

/// <summary>
/// Công Chúa 3D - NPC cứu ở cuối màn 3.
/// </summary>
public partial class Princess3D : Area3D
{
    [Export] public bool RequireAllEnemiesDefeated = true;

    private AnimatedSprite3D _animSprite;
    private Label3D _messageLabel;
    private bool _isRescued = false;

    [Signal] public delegate void PrincessRescuedEventHandler();

    public override void _Ready()
    {
        _animSprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        if (_animSprite.SpriteFrames == null)
        {
            try { _animSprite.SpriteFrames = SpriteHelper.CreatePrincessSpriteFrames(); }
            catch { }
        }

        _messageLabel = new Label3D();
        _messageLabel.Text = "Hãy đánh bại hết yêu tà!";
        _messageLabel.Position = new Vector3(0, 2, 0);
        _messageLabel.Visible = false;
        _messageLabel.FontSize = 48;
        _messageLabel.Modulate = Colors.Yellow;
        _messageLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _messageLabel.PixelSize = 0.01f;
        AddChild(_messageLabel);

        BodyEntered += OnBodyEntered;
        _animSprite.Play("idle");
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_isRescued || !body.IsInGroup("player")) return;

        if (RequireAllEnemiesDefeated)
        {
            var nodes = GetTree().GetNodesInGroup("enemies");
            int aliveCount = 0;
            foreach (var n in nodes)
            {
                if (n is BaseEnemy3D e && !e.IsDead) aliveCount++;
            }

            if (aliveCount > 0)
            {
                _messageLabel.Text = $"Còn {aliveCount} yêu tà!";
                _messageLabel.Visible = true;
                var tw = CreateTween();
                tw.TweenInterval(2.0);
                tw.TweenCallback(Callable.From(() => { _messageLabel.Visible = false; }));
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
            new DialogueManager.DialogueLine("Công Chúa", "Chàng thật sự đến rồi, Cảm ơn Chàng đã cứu ta!", null, "res://Assets/Audio/Voices/princess_free1.mp3"),
            new DialogueManager.DialogueLine("Thạch Sanh", "Người vô tội không nên bị giam cầm.", null, "res://Assets/Audio/Voices/ts_end_princess.mp3"),
            new DialogueManager.DialogueLine("Ngọc Hoàng", "Chúc mừng ngươi! Hãy trở về và nhận phần thưởng xứng đáng.", null, "res://Assets/Audio/Voices/god_end_win2.mp3"),
        };
        await dm.PlayDialogue(lines);

        GameManager.Instance?.AddScore(500);
        GameManager.Instance?.NextLevel();
    }
}
