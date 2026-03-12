using Godot;
using System;
using System.Threading.Tasks;

public partial class ChanTinh : BaseEnemy
{
    [Export] public PackedScene KeyScene;
    [Export] public PackedScene ChestScene;

    private enum BossState
    {
        Idle,
        Chase,
        Telegraph,
        Attack,
        Cooldown
    }

    private BossState _bossState = BossState.Idle;
    private float _stateTimer = 0f;
    private string _queuedAttack = "";
    private bool _hasHitTarget = false;

    // Phải khớp CHÍNH XÁC với tên trong SpriteHelper.cs
    // 8 chiêu thức đa dạng: chém, đập, quay, lửa, nhảy, sét, ném, năng lượng
    private readonly string[] _attacks = {
        "attack_melee", "attack_smash", "attack_spin", 
        "attack_fire", "attack_jump", "attack_lightning",
        "attack_throw", "attack_energy",
        "attack_chem", "attack_ngang", "attack_tren"
    };

    public override void _Ready()
    {
        MaxHealth = 1000;
        AttackDamage = 30;
        MoveSpeed = 65f;
        ScoreValue = 5000;

        DetectRange = 900f;
        AttackRange = 180f;
        AttackCooldown = 1.2f; // Tăng tần suất từ 2.0s xuống 1.2s

        // Health Bar offset cho nửa màn hình
        HealthBarOffset = new Vector2(-40, -220); 

        base._Ready();

        // Sprite setup - Canvas 600px, dùng Scale 0.6 để hiển thị nửa màn hình
        AnimSprite.SpriteFrames = SpriteHelper.CreateChanTinhSpriteFrames();
        AnimSprite.Offset = new Vector2(0, -300f);
        AnimSprite.Centered = true;
        AnimSprite.Play("idle");

        // Scale 0.6: sprite 500px * 0.6 = 300px hiển thị ≈ nửa màn hình 648px
        Scale = new Vector2(0.6f, 0.6f); 
        ZIndex = 4000;

        if (_healthBarNode != null)
        {
            // Scale ngược lại cho health bar (1/0.6 ≈ 1.67) để giữ kích thước đọc được
            _healthBarNode.Scale = new Vector2(5.0f, 2.5f);
            ((Node2D)_healthBarNode).ZIndex = 4005;
            _healthBarNode.Position = HealthBarOffset;
        }

        UpdateCollisionShapes();
        GD.Print("[ChanTinh] Boss initialized with improved Health Bar and State Logic.");
    }

    public override void TakeDamage(int damage)
    {
        // Khi bị trúng đòn, Boss sẽ bị khựng lại
        bool isHeavyAttack = _queuedAttack == "attack_fire" || _queuedAttack == "attack_energy" || 
                             _queuedAttack == "attack_smash" || _queuedAttack == "attack_lightning" ||
                             _queuedAttack == "attack_tren" || _queuedAttack == "attack_chem";
        
        // Super Armor: Boss không bị khựng nếu đang ra chiêu nặng hoặc máu còn > 50%
        bool hasSuperArmor = (_bossState == BossState.Attack && isHeavyAttack) || (Health > MaxHealth * 0.5f);
        
        bool wasBusy = (_bossState == BossState.Attack || _bossState == BossState.Telegraph);
        
        base.TakeDamage(damage);

        if (hasSuperArmor)
        {
            GD.Print("[ChanTinh] Super Armor active! Boss ignored hit stun.");
            return;
        }
        
        // FIX LỖI ĐƠ: Reset lại máy trạng thái nếu bị trúng đòn
        if (Health > 0)
        {
            GD.Print("[ChanTinh] Hit stun! Interrupting actions.");
            
            if (wasBusy && GD.Randf() > 0.4f)
            {
                _bossState = BossState.Telegraph;
                _stateTimer = 0.4f; // Phản công 
            }
            else
            {
                _bossState = BossState.Cooldown;
                _stateTimer = 0.5f; 
            }
            
            // Một chút delay để đảm bảo thoát khỏi trạng thái Hurt của BaseEnemy rồi mới Play lại Idle
            var timer = GetTree().CreateTimer(0.4f);
            timer.Timeout += () => {
                if (!IsDead && !IsHurt) AnimSprite.Play("idle");
            };
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead || IsHurt)
        {
            base._PhysicsProcess(delta);
            return;
        }

        float dt = (float)delta;
        
        // LUÔN LUÔN giảm timer để các bẫy Safety Timeout hoạt động được
        _stateTimer -= dt;

        switch (_bossState)
        {
            case BossState.Idle:
                ProcessIdleState();
                break;
            case BossState.Chase:
                ProcessChaseState();
                break;
            case BossState.Telegraph:
                ProcessTelegraphState();
                break;
            case BossState.Attack:
                ProcessAttackState();
                break;
            case BossState.Cooldown:
                ProcessCooldownState();
                break;
        }

        ApplyGravityAndMove(dt);
    }

    private void ProcessIdleState()
    {
        AnimSprite.Play("idle");
        Velocity = new Vector2(0, Velocity.Y);

        if (FindTargetPlayer())
        {
            _bossState = BossState.Chase;
        }
    }

    private void ProcessChaseState()
    {
        if (!IsInstanceValid(TargetPlayer) || TargetPlayer.IsQueuedForDeletion())
        {
            _bossState = BossState.Idle;
            return;
        }

        float dist = GlobalPosition.DistanceTo(TargetPlayer.GlobalPosition);
        float dir = TargetPlayer.GlobalPosition.X > GlobalPosition.X ? 1f : -1f;

        // Nếu người chơi chạy quá xa, quay về Idle
        if (dist > DetectRange * 1.2f)
        {
            _bossState = BossState.Idle;
            TargetPlayer = null;
            return;
        }

        if (dist <= AttackRange)
        {
            Velocity = new Vector2(0, Velocity.Y);
            StartTelegraph();
        }
        else
        {
            Velocity = new Vector2(dir * MoveSpeed, Velocity.Y);
            AnimSprite.Play("run");
            SetFacingDirection(dir < 0);
        }
    }

    private void StartTelegraph()
    {
        _bossState = BossState.Telegraph;
        _stateTimer = (float)GD.RandRange(0.3, 0.5); // Giảm chuẩn bị rườm rà
        
        // Random chuẩn bị (đã thêm vào SpriteHelper.cs)
        string prepAnim = GD.Randf() > 0.5 ? "attack_prepare_2" : "attack_ready";
        AnimSprite.Play(prepAnim);
        
        if (IsInstanceValid(TargetPlayer))
            SetFacingDirection(TargetPlayer.GlobalPosition.X < GlobalPosition.X);

        GD.Print($"[ChanTinh] Telegraphing... ({prepAnim})");
    }

    private void ProcessTelegraphState()
    {
        Velocity = new Vector2(0, Velocity.Y);
        if (_stateTimer <= 0)
        {
            ExecuteAttack();
        }
    }

    private void ExecuteAttack()
    {
        _bossState = BossState.Attack;
        _hasHitTarget = false;

        // Ưu tiên các chiêu thức mới (70% tỉ lệ ra) để người dùng dễ kiểm tra
        if (GD.Randf() < 0.7f)
        {
            string[] newAttacks = { "attack_chem", "attack_ngang", "attack_tren" };
            _queuedAttack = newAttacks[(int)GD.Randi() % newAttacks.Length];
        }
        else
        {
            _queuedAttack = _attacks[(int)GD.Randi() % _attacks.Length];
        }
        
        AnimSprite.Play(_queuedAttack);
        GD.Print($"[ChanTinh] Executing Attack: {_queuedAttack}");

        // VFX và Rung màn hình khi xuất chiêu nặng
        CreateAttackVFX();
        bool isHeavyAttackMove = _queuedAttack == "attack_smash" || _queuedAttack == "attack_lightning" 
            || _queuedAttack == "attack_fire" || _queuedAttack == "attack_energy"
            || _queuedAttack == "attack_tren" || _queuedAttack == "attack_chem";

        if (isHeavyAttackMove)
        {
            TriggerCameraShake(0.4f, 25f);
        }
    }

    private void ProcessAttackState()
    {
        Velocity = new Vector2(0, Velocity.Y);
        
        // Safety timeout
        if (_stateTimer < -4.0f)
        {
            _bossState = BossState.Cooldown;
            _stateTimer = AttackCooldown;
            return;
        }

        // DELAY HIT: Đợi đến frame 1-2 mới gây sát thương để người chơi kịp thấy đòn tấn công
        bool shouldCheckHit = false;
        if (_queuedAttack.Contains("chem") || _queuedAttack.Contains("ngang") || _queuedAttack.Contains("tren"))
        {
            shouldCheckHit = AnimSprite.Frame >= 1;
        }
        else
        {
            shouldCheckHit = AnimSprite.Frame >= 2;
        }

        if (!_hasHitTarget && shouldCheckHit)
        {
            CheckHit();
        }

        // KẾT THÚC ĐÒN ĐÁNH: Khi chạy hết frame của anim thì đổi sang Cooldown ngay
        int lastFrame = AnimSprite.SpriteFrames.GetFrameCount(AnimSprite.Animation) - 1;
        if (AnimSprite.Frame >= lastFrame)
        {
            _bossState = BossState.Cooldown;
            _stateTimer = AttackCooldown;
            GD.Print($"[ChanTinh] Attack finished, cooling down for {AttackCooldown}s");
        }
    }

    private void ProcessCooldownState()
    {
        Velocity = new Vector2(0, Velocity.Y);
        if (AnimSprite.Animation != "idle") AnimSprite.Play("idle");

        if (_stateTimer <= 0)
        {
            _bossState = BossState.Chase;
        }
    }

    private void CheckHit()
    {
        if (_hasHitTarget || !IsInstanceValid(TargetPlayer)) return;

        float dist = GlobalPosition.DistanceTo(TargetPlayer.GlobalPosition);
        bool facingLeft = AnimSprite.FlipH;
        bool playerLeft = TargetPlayer.GlobalPosition.X < GlobalPosition.X;

        // Chỉ trúng đòn nếu đứng đúng hướng mặt của Boss
        if (dist <= AttackRange && facingLeft == playerLeft)
        {
            TargetPlayer.TakeDamage(AttackDamage);
            _hasHitTarget = true;
            GD.Print("[ChanTinh] Boss hit player!");
        }
    }

    protected override void OnAnimationFinished()
    {
        if (_bossState == BossState.Attack)
        {
            _bossState = BossState.Cooldown;
            _stateTimer = AttackCooldown;
            GD.Print("[ChanTinh] Attack finished, cooling down.");
        }
        else
        {
            base.OnAnimationFinished();
        }
    }

    private void TriggerCameraShake(float duration, float intensity)
    {
        var cam = GetTree().GetFirstNodeInGroup("MainCamera") as FollowCamera;
        if (cam != null) cam.Shake(duration, intensity);
    }

    private bool FindTargetPlayer()
    {
        var players = GetTree().GetNodesInGroup("player");
        if (players.Count > 0)
        {
            TargetPlayer = players[0] as Player;
            return true;
        }
        return false;
    }

    private void ApplyGravityAndMove(float dt)
    {
        Vector2 vel = Velocity;
        if (!IsOnFloor()) vel.Y += Gravity * dt;
        Velocity = vel;
        MoveAndSlide();
    }

    private void UpdateCollisionShapes()
    {
        var bodyNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (bodyNode != null && bodyNode.Shape is RectangleShape2D rect)
        {
            var newShape = (RectangleShape2D)rect.Duplicate();
            newShape.Size = new Vector2(110, 230);
            bodyNode.Shape = newShape;
        }
    }

    protected override async void Die()
    {
        if (IsDead) return;
        IsDead = true;
        _bossState = BossState.Idle;
        
        GD.Print("[ChanTinh] Boss defeated! Playing dramatic death sequence.");
        
        // 1. HITSTOP & SLOW MOTION (Cinematic feel)
        Engine.TimeScale = 0.15f; // Dừng hình nhẹ 0.15s
        TriggerCameraShake(0.6f, 25f); // Giảm rung xuống (từ 1.5s/60f) để nhìn rõ quá trình chết
        
        await Task.Delay(150); // Delay thực tế để người chơi cảm nhận cú chót
        Engine.TimeScale = 0.4f; // Chuyển sang Slow-motion mượt mà (40% tốc độ)
        
        // Cảnh báo: Phải dùng Task.Delay tính theo thời gian thực (vì TimeScale đang thấp)
        
        // 2. Chuỗi hiệu ứng nổ liên hoàn
        for (int i = 0; i < 6; i++)
        {
            CreateExplosionVFX();
            // Chờ một chút giữa các vụ nổ (dùng Milliseconds thực tế)
            await Task.Delay(250);
            if (!IsInstanceValid(this)) return;
        }

        // 3. Play animation chết chậm (Để nhìn rõ ảnh người dùng tự cắt)
        AnimSprite.SpeedScale = 0.6f; 
        AnimSprite.Play("die");
        
        // ĐỢI ĐẾN FRAME CUỐI (B_chet6 tương đương frame 6)
        while (IsInstanceValid(this) && AnimSprite.Animation == "die" && AnimSprite.Frame < 6)
        {
            await Task.Delay(100);
        }
        
        // DỪNG Ở FRAME CUỐI để người chơi nhìn rõ cảnh Boss gục
        if (IsInstanceValid(this) && AnimSprite.Animation == "die")
        {
            AnimSprite.Stop();
            AnimSprite.Frame = 6;
            GD.Print("[ChanTinh] Holding final death frame.");
        }

        // 4. RƠI RƯƠNG BÁU (Thay vì rơi chìa khóa trực tiếp)
        SpawnChest();

        // 5. Đợi một chút trước khi biến mất (TimeScale đang là 0.4f nên 3s thực tế = 1.2s game)
        await Task.Delay(3000); 
        
        if (IsInstanceValid(this)) 
        {
            // Hiệu ứng mờ dần (Fade out)
            var fadeTw = CreateTween();
            fadeTw.TweenProperty(this, "modulate:a", 0f, 0.8f);
            await ToSignal(fadeTw, "finished");
            
            // Reset lại tốc độ game TRƯỚC khi xóa boss
            Engine.TimeScale = 1.0f;
            QueueFree();
        }
    }

    private void SpawnChest()
    {
        if (ChestScene == null) ChestScene = GD.Load<PackedScene>("res://Scenes/NPCs/TreasureChest.tscn");
        if (ChestScene != null)
        {
            var chest = ChestScene.Instantiate<TreasureChest>();
            // Thiết lập rương cho Boss: Không cần diệt quái nữa (đã giết Boss rồi)
            chest.RequireAllEnemiesDefeated = false;
            
            GetParent().AddChild(chest);
            chest.GlobalPosition = GlobalPosition + new Vector2(0, -50);
            
            // Hiệu ứng cái rương bay ra từ người boss
            var tween = chest.CreateTween();
            Vector2 targetPos = chest.GlobalPosition + new Vector2(GD.Randf() > 0.5f ? 120 : -120, 40);
            tween.TweenProperty(chest, "global_position:y", chest.GlobalPosition.Y - 100, 0.5f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Chain().TweenProperty(chest, "global_position", targetPos, 0.5f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
            
            GD.Print("[ChanTinh] Treasure Chest spawned!");
        }
    }

    private void CreateExplosionVFX()
    {
        // Giả lập hiệu ứng nổ bằng cách tạo VFX tại các vị trí ngẫu nhiên quanh Boss
        var pos = GlobalPosition + new Vector2((float)GD.RandRange(-100, 100), (float)GD.RandRange(-200, 0));
        var vfx = new Sprite2D();
        vfx.Texture = GD.Load<Texture2D>("res://Assets/Sprites/VFX/explosion.png"); // Giả định path này tồn tại hoặc dùng fallback
        if (vfx.Texture == null) return;
        
        GetParent().AddChild(vfx);
        vfx.GlobalPosition = pos;
        vfx.Scale = new Vector2(2f, 2f);
        
        var tween = vfx.CreateTween();
        tween.TweenProperty(vfx, "modulate:a", 0, 0.5f);
        tween.TweenCallback(Callable.From(vfx.QueueFree));
    }

    private void SpawnKey()
    {
        if (KeyScene == null) KeyScene = GD.Load<PackedScene>("res://Scenes/Items/BossKey.tscn");
        if (KeyScene != null)
        {
            var key = KeyScene.Instantiate<Node2D>();
            key.GlobalPosition = GlobalPosition;
            GetParent().AddChild(key);
            var tween = key.CreateTween();
            tween.TweenProperty(key, "position:y", key.Position.Y - 80, 0.6f).SetTrans(Tween.TransitionType.Back);
        }
    }
}