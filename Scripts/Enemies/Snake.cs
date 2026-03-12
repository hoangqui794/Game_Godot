using Godot;
using System.Collections.Generic;

public partial class Snake : BaseEnemy
{
    public override void _Ready()
    {
        // Set snake-specific stats before base _Ready
        // ── Rắn: Kẻ thù cơ bản, gặm dần máu player ──
        // Player cần 4 đòn thường (25×4=100) để hạ, rắn cần 9 cú cắn (12×9=108) để kill player
        MaxHealth    = 80;       // Đủ chịu 3 đòn thường (25×3=75 < 80) → cần đòn thứ 4
        AttackDamage = 12;       // 12% MaxHP mỗi đòn → player chịu ~8-9 đòn trước khi chết
        MoveSpeed    = 65.0f;    // Chậm bò sát đất, chắc chắn
        ScoreValue   = 150;      // Điểm cao hơn vì HP nhiều hơn
        PatrolDistance = 100.0f; // Vùng tuần tra rộng thêm chút
        DetectRange    = 220.0f; // Nhìn ra xa vừa đủ
        AttackRange    = 48.0f;  // Cự ly cắn ngắn (phải áp sát)
        
        // Đẩy Thanh Máu vọt thẳng lên trời (-90 pixel) để vượt mảng Sprite của đầu Rắn
        HealthBarOffset = new Vector2(-20, -85);

        base._Ready();
    }

    protected override void CreateAttackVFX()
    {
        // Container cho hiệu ứng Cạp (Bite) - Căn chỉnh chính xác ngay miệng rắn
        var biteNode = new Node2D();
        float faceSign = AnimSprite.FlipH ? -1f : 1f;
        // Miệng rắn thường nằm ở tầm thấp X=25, Y=-18
        biteNode.Position = new Vector2(faceSign * 28, -18);
        AddChild(biteNode);

        // --- TẠO 4 RĂNG NANH SẮC NHỌN (2 Trên, 2 Dưới) ---
        for (int i = 0; i < 4; i++)
        {
            var fang = new Polygon2D();
            // Hình tam giác nanh nhọn
            fang.Polygon = new Vector2[] { 
                new Vector2(-4, 0), 
                new Vector2(4, 0), 
                new Vector2(0, 15) 
            };
            fang.Color = new Color(1, 1, 1); // Trắng
            
            bool isTop = i < 2;
            float xOffset = (i % 2 == 0) ? -10 : 10;
            
            fang.Position = new Vector2(xOffset, isTop ? -25 : 25);
            if (isTop) fang.Rotation = 0; // Chúc xuống
            else fang.Rotation = Mathf.Pi; // Chúc lên
            
            biteNode.AddChild(fang);

            // Animation Cạp (Snap)
            var tw = fang.CreateTween();
            tw.SetParallel(true);
            
            // Hàm trên dập xuống, hàm dưới dập lên
            float targetY = isTop ? -2 : 2;
            tw.TweenProperty(fang, "position:y", targetY, 0.12f).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.In);
            
            // Chớp màu tím độc khi cắn trúng
            tw.Chain().TweenProperty(fang, "color", new Color(0.6f, 0.2f, 1.0f), 0.05f);
            tw.TweenProperty(fang, "modulate:a", 0f, 0.2f).SetDelay(0.1f);
        }

        // --- HIỆU ỨNG NHỎ GIỌT ĐỘC TỐ (Poison Drip) ---
        var drip = new CpuParticles2D();
        drip.Amount = 8;
        drip.Lifetime = 0.6f;
        drip.Explosiveness = 0.5f;
        drip.Direction = new Vector2(0, 1); // Rơi xuống
        drip.Spread = 20f;
        drip.Gravity = new Vector2(0, 500);
        drip.InitialVelocityMin = 50f;
        drip.InitialVelocityMax = 100f;
        drip.ScaleAmountMin = 3f;
        drip.ScaleAmountMax = 6f;
        drip.Color = new Color(0.3f, 0.8f, 0.1f, 0.8f); // Xanh lá độc
        biteNode.AddChild(drip);
        drip.Emitting = true;

        // --- LUỒNG KHÍ ĐỘC PHUN RA ---
        var gas = new CpuParticles2D();
        gas.Amount = 20;
        gas.OneShot = true;
        gas.Explosiveness = 1.0f;
        gas.Direction = new Vector2(faceSign, -0.2f);
        gas.Spread = 40f;
        gas.Gravity = Vector2.Zero;
        gas.InitialVelocityMin = 120f;
        gas.InitialVelocityMax = 200f;
        gas.ScaleAmountMin = 4f;
        gas.ScaleAmountMax = 8f;
        gas.Color = new Color(0.5f, 0.1f, 0.7f, 0.5f); // Tím mờ
        biteNode.AddChild(gas);

        // Tự hủy sau khi diễn xong
        var cleanup = GetTree().CreateTimer(0.8f);
        cleanup.Timeout += () => { if (IsInstanceValid(biteNode)) biteNode.QueueFree(); };
    }

    protected override void CreatePlaceholderSprites()
    {
        AnimSprite.SpriteFrames = SpriteHelper.CreateSnakeSpriteFrames();
        AnimSprite.Play("walk");
    }
}
