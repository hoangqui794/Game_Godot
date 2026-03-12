using Godot;
using System.Collections.Generic;
public partial class BossEnemy : BaseEnemy
{
    [Export] public PackedScene KeyScene;

    private Timer _contactDamageTimer;
    private bool _canDealContactDamage = true;
    private Vector2 _lockedPosition;
    private int _attackCount = 0;

    public override void _Ready()
    {
        // Stats của Đại Boss
        MaxHealth = 300;
        AttackDamage = 10; // Giảm xuống 10 theo yêu cầu
        MoveSpeed = 0;
        ScoreValue = 2000;
        DetectRange = 500.0f;
        AttackRange = 200.0f; // Điều chỉnh tầm đánh về 200px theo yêu cầu mới nhất
        AttackCooldown = 1.5f; // Cooldown 1.5s
        PatrolDistance = 0;
        PatrolDirection = -1;

        // Căn chỉnh thanh máu: thấp thêm 50px nữa theo yêu cầu (-180 + 50 = -130)
        HealthBarOffset = new Vector2(-80, -130);

        base._Ready();
        
        // Cập nhật lại wait time của timer vì base._Ready() đã gán giá trị mặc định 1.0s
        if (AttackCooldownTimer != null) AttackCooldownTimer.WaitTime = 1.5f;

        Scale = new Vector2(1.5f, 1.5f);
        _lockedPosition = GlobalPosition;

        if (_healthBarNode != null)
        {
            // Làm thanh máu Boss dài ra nhìn cho "trâu"
            _healthBarNode.Scale = new Vector2(4.0f, 1.5f);
            (_healthBarNode as Node2D).ZIndex = 4005;
        }

        SetFacingDirection(true);

        ZIndex = 4000;
        if (AnimSprite != null) {
            AnimSprite.ZIndex = 4001;
            AnimSprite.Visible = true;
            AnimSprite.FlipH = false;
        }

        // Timer cho sát thương va chạm (2s)
        _contactDamageTimer = new Timer();
        _contactDamageTimer.WaitTime = 2.0f;
        _contactDamageTimer.OneShot = false;
        _contactDamageTimer.Timeout += () => _canDealContactDamage = true;
        AddChild(_contactDamageTimer);
        _contactDamageTimer.Start();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;

        // 1. Kháng đẩy tuyệt đối & Vật thể rắn: Khóa vị trí không cho Player đẩy hoặc đi xuyên qua
        GlobalPosition = _lockedPosition;
        Velocity = Vector2.Zero;
        
        // Đảm bảo va chạm luôn được tính toán để Player không đi xuyên qua
        // (CharacterBody2D khi khóa vị trí vẫn cản bước các vật thể khác nếu Layer/Mask đúng)
        MoveAndSlide(); 

        // Gravity vẫn tính nhưng Position đã bị khóa (Boss thường ở nền nhà)
        
        // 2. Ép Animation Idle khi không đánh/đau
        if (CurrentState == EnemyState.Patrol || CurrentState == EnemyState.Chase)
        {
            AnimSprite.Play("idle");
        }

        // 3. Tự động tấn công khi Player ở gần
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null && !player.IsQueuedForDeletion())
        {
            float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
            
            if (CurrentState != EnemyState.Attack && dist <= AttackRange)
            {
                if (CanAttackPlayer && CurrentState != EnemyState.Hurt)
                {
                    CurrentState = EnemyState.Attack;
                    AnimSprite.Play("attack");
                }
            }
            else if (CurrentState == EnemyState.Attack && dist > AttackRange + 30.0f)
            {
                CurrentState = EnemyState.Patrol;
                AnimSprite.Play("idle");
            }

            // 4. Sát thương va chạm (Contact Damage) & Sát thương khi Đánh
            // Nếu đang chém (Attack), dùng toàn bộ AttackRange. 
            // Nếu không thì dùng phạm vi va chạm mặc định (150px cho Boss to).
            float damageRange = (CurrentState == EnemyState.Attack) ? AttackRange : 150.0f;
            
            if (_canDealContactDamage && dist < damageRange)
            {
                player.TakeDamage(AttackDamage);
                _canDealContactDamage = false;
                GD.Print($"[BossEnemy] Gây sát thương! (Range: {damageRange})");
            }
        }

        if (AnimSprite != null && AnimSprite.FlipH)
        {
            AnimSprite.FlipH = false;
        }
    }

    protected override void OnAnimationFinished()
    {
        if (AnimSprite.Animation == "attack")
        {
            _attackCount++;
            GD.Print($"[BossEnemy] Attack sequence progress: {_attackCount}/3");

            var player = GetTree().GetFirstNodeInGroup("player") as Player;
            bool playerInRange = player != null && !player.IsQueuedForDeletion() && 
                                GlobalPosition.DistanceTo(player.GlobalPosition) <= AttackRange + 50.0f;

            // Mỗi 3 đòn tấn công thì hất văng player
            if (_attackCount >= 3)
            {
                if (playerInRange)
                {
                    GD.Print("[BossEnemy] Cú đánh thứ 3! Hất văng Player!");
                    Vector2 knockbackDir = (player.GlobalPosition - GlobalPosition).Normalized();
                    player.ApplyKnockback(new Vector2(knockbackDir.X * 700, -450));
                    player.TakeDamage(AttackDamage * 2);
                }
                _attackCount = 0;
            }
            else
            {
                // Đòn 1 và 2 cũng phải chắc chắn gây sát thương nếu trúng
                if (playerInRange)
                {
                    player.TakeDamage(AttackDamage);
                }
            }

            CurrentState = EnemyState.Patrol;
            CanAttackPlayer = false;
            AttackCooldownTimer.Start();
        }
    }

    protected override async void Die()
    {
        base.Die();
        DeathTimer.Stop(); // Ngăn Boss biến mất ngay lập tức để đợi hội thoại
        
        var dm = new DialogueManager();
        GetParent().AddChild(dm);
        var lines = new List<DialogueManager.DialogueLine>
        {
            new DialogueManager.DialogueLine("Chằn Tinh", "Không thể, cái binh pháp quỷ này, ta đã kiêu ngạo, quá lâu.", null, "res://Assets/Audio/Voices/chantinh_die.mp3"),
            new DialogueManager.DialogueLine("Thạch Sanh", "Ngươi đã thua rồi, Chằn Tinh. Không phải vì ta mạnh hơn — mà vì lẽ phải trời đất luôn thắng kẻ hung tàn!", null, "res://Assets/Audio/Voices/ts_boss_end1.mp3")
        };
        await dm.PlayDialogue(lines);

        SpawnKey();
        QueueFree(); // Xóa boss sau khi hội thoại xong
    }

    private void SpawnKey()
    {
        GD.Print("Đại Boss gục ngã! Lối thoát đã mở.");
        
        // Kích hoạt cổng thoát (nếu có trong scene)
        var exit = GetTree().GetFirstNodeInGroup("LevelExit") as LevelExit;
        if (exit != null)
        {
            exit.Activate();
        }

        // Load key scene nếu chưa gán
        if (KeyScene == null)
        {
            KeyScene = GD.Load<PackedScene>("res://Scenes/Items/BossKey.tscn");
        }

        if (KeyScene != null)
        {
            var key = KeyScene.Instantiate<Node2D>();
            key.GlobalPosition = GlobalPosition;
            GetParent().AddChild(key);
            
            // Hiệu ứng nảy chìa khóa
            var tween = key.CreateTween();
            tween.TweenProperty(key, "position:y", key.Position.Y - 50, 0.5f).SetTrans(Tween.TransitionType.Back);
        }
    }

    protected override void CreatePlaceholderSprites()
    {
        // Thử tải cả hai loại để chắc chắn quái nào cũng được xử lý
        AnimSprite.SpriteFrames = SpriteHelper.CreateFinalBossSpriteFrames();
        if (AnimSprite.SpriteFrames == null)
        {
            GD.Print("[BossEnemy] FinalBoss.jpg không tìm thấy, thử dùng BossRan.png");
            AnimSprite.SpriteFrames = SpriteHelper.CreateBossSpriteFrames();
        }

        if (AnimSprite.SpriteFrames == null)
        {
            GD.PrintErr("Không thể tạo SpriteFrames cho Boss, dùng fallback.");
            base.CreatePlaceholderSprites();
            return;
        }

        AnimSprite.Play("idle");

        // Điều chỉnh Offset để Boss to đứng đúng trên mặt đất
        // Hạ thấp Boss xuống một chút (từ -280 xuống -240)
        AnimSprite.Offset = new Vector2(0, -240.0f);
        AnimSprite.Visible = true;
        
        GD.Print("[BossEnemy] Đã chuyển sang dùng FinalBoss.jpg thành công.");
    }

    protected override void SetFacingDirection(bool faceLeft)
    {
        // Vô hiệu hóa để không bị BaseEnemy điều khiển
    }

    public override void _Process(double delta)
    {
        // Khóa hướng mặt ở mức _Process để đảm bảo ổn định tuyệt đối
        if (AnimSprite != null)
        {
            if (AnimSprite.FlipH) AnimSprite.FlipH = false;
        }
    }
}
