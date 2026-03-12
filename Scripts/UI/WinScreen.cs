using Godot;
using System.Threading.Tasks;

public partial class WinScreen : Control
{
    private VBoxContainer _mainContainer;
    private TextureRect _bgImage;
    private ColorRect _fadeRect;
    private Label _subtitleLabel;
    private AudioStreamPlayer _audioPlayer;

    public override void _Ready()
    {
        _mainContainer = GetNode<VBoxContainer>("MainContainer");
        _mainContainer.Visible = false;

        var menuButton = GetNode<Button>("MainContainer/MenuButton");
        menuButton.Pressed += OnMenuPressed;

        var scoreLabel = GetNode<Label>("MainContainer/ScoreLabel");
        scoreLabel.Text = $"Tổng điểm: {GameManager.Instance.Score}";

        SetupCutsceneUI();
        CallDeferred(nameof(StartCutscene));
    }

    private void SetupCutsceneUI()
    {
        _bgImage = new TextureRect();
        _bgImage.SetAnchorsPreset(LayoutPreset.FullRect);
        _bgImage.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _bgImage.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        AddChild(_bgImage);

        // Subtitle background
        var subBg = new ColorRect();
        subBg.Color = new Color(0, 0, 0, 0.7f);
        subBg.SetAnchorsPreset(LayoutPreset.BottomWide);
        subBg.CustomMinimumSize = new Vector2(0, 180);
        subBg.Size = new Vector2(0, 180);
        subBg.AnchorTop = 1;
        subBg.AnchorBottom = 1;
        subBg.OffsetTop = -180;
        subBg.OffsetBottom = 0;
        _bgImage.AddChild(subBg);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        subBg.AddChild(margin);

        _subtitleLabel = new Label();
        _subtitleLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _subtitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitleLabel.VerticalAlignment = VerticalAlignment.Center;
        _subtitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _subtitleLabel.AddThemeFontSizeOverride("font_size", 22);
        _subtitleLabel.AddThemeColorOverride("font_color", Colors.White);
        margin.AddChild(_subtitleLabel);

        _fadeRect = new ColorRect();
        _fadeRect.Color = Colors.Black;
        _fadeRect.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_fadeRect);

        _audioPlayer = new AudioStreamPlayer();
        AddChild(_audioPlayer);
    }

    private async void StartCutscene()
    {
        string[] imagesFiles = {"Escape_cave_endgame", "explain_King", "wedding"}; 
        string[] lines = {
            "Chằn Tinh gục xuống, sào huyệt bắt đầu rung chuyển dữ dội như muốn chôn vùi mọi dấu tích của quỷ dữ. Không một chút chần chừ, Thạch Sanh bế công chúa Quỳnh Nga, băng qua những tảng đá rơi và làn khói bụi mờ mịt. Ánh sáng phía cuối hang động không chỉ là lối thoát, mà là khởi đầu cho một trang sử mới của một người anh hùng đích thực.",
            "Vượt qua bao dặm đường rừng, Thạch Sanh dắt tay Công chúa trở về giữa sự ngỡ ngàng của kinh thành. Trước điện bệ uy nghiêm, chàng quỳ xuống bẩm báo rõ ngọn ngành về hang yêu, về cuộc chiến sinh tử để cứu nguy cho người con gái của Đức vua. Sự chân thành trong từng lời nói và ánh mắt chính trực của người tráng sĩ ấy đã khiến trái tim nhà vua xúc động, nhận ra đâu mới là bậc anh hùng đích thực.",
            "Tiếng đàn thần vang lên, hòa cùng tiếng trống hội tưng bừng khắp bờ cõi. Trong bộ lễ phục rực rỡ mang đậm bản sắc Việt, Thạch Sanh và Quỳnh Nga nên duyên vợ chồng dưới sự chúc phúc của cả vương quốc. Từ một chàng trai nghèo dưới gốc đa, Thạch Sanh chính thức trở thành Phò mã, dùng tài năng và đức độ để bảo vệ sự bình yên cho bờ cõi."
        };

        for (int i = 0; i < 3; i++)
        {
            Texture2D tex = GD.Load<Texture2D>($"res://Assets/Visuals/Cutscenes/{imagesFiles[i]}.png");
            if (tex == null) tex = GD.Load<Texture2D>($"res://Assets/Visuals/Cutscenes/{imagesFiles[i]}.jpg");
            if (tex == null) tex = GD.Load<Texture2D>($"res://Assets/Visuals/Cutscenes/{imagesFiles[i]}.jpeg");
            
            if (tex != null)
            {
                _bgImage.Texture = tex;
            }
            
            _subtitleLabel.Text = lines[i];
            _subtitleLabel.VisibleRatio = 0;

            AudioStream audio = GD.Load<AudioStream>($"res://Assets/Audio/Voices/{imagesFiles[i]}.mp3");
            
            // Fade In
            var twIn = CreateTween();
            twIn.TweenProperty(_fadeRect, "color:a", 0.0f, 1.0f);
            await ToSignal(twIn, "finished");

            float duration = 4.0f;
            if (audio != null)
            {
                duration = (float)audio.GetLength();
                _audioPlayer.Stream = audio;
                _audioPlayer.Play();
            }
            else
            {
                duration = lines[i].Length * 0.06f;
            }

            // Hiệu ứng chữ chạy mượt mà theo độ dài âm thanh
            var typeTw = CreateTween();
            typeTw.TweenProperty(_subtitleLabel, "visible_ratio", 1.0f, duration);

            if (audio != null)
            {
                await ToSignal(_audioPlayer, "finished");
            }
            else
            {
                await ToSignal(typeTw, "finished");
            }
            await Task.Delay(800);

            // Fade Out between slides
            if (i < 2)
            {
                var twOut = CreateTween();
                twOut.TweenProperty(_fadeRect, "color:a", 1.0f, 1.0f);
                await ToSignal(twOut, "finished");
            }
        }

        // Mờ màn hình chuyển sang Win Screen Menu
        var twFinal = CreateTween();
        twFinal.TweenProperty(_fadeRect, "color:a", 1.0f, 1.0f);
        await ToSignal(twFinal, "finished");

        _bgImage.Visible = false;
        _mainContainer.Visible = true;
        
        var twShowUI = CreateTween();
        twShowUI.TweenProperty(_fadeRect, "color:a", 0.0f, 1.0f);
        await ToSignal(twShowUI, "finished");
        _fadeRect.Visible = false;
        
        // Hiệu ứng Title bay lên nhẹ nhàng The Win Screen Title Default Animation
        var title = GetNode<Label>("MainContainer/Title");
        title.Modulate = new Color(1, 1, 1, 0);
        var t = CreateTween();
        t.TweenProperty(title, "modulate:a", 1.0f, 1.0f);
        t.SetParallel();
        title.Position += new Vector2(0, 50);
        t.TweenProperty(title, "position:y", title.Position.Y - 50, 1.0f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void OnMenuPressed()
    {
        GameManager.Instance.GoToMainMenu();
    }
}
