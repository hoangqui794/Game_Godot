using Godot;
using System;
using System.Threading.Tasks;

public partial class ChanTinh : BaseEnemy
{
    [Export] public PackedScene KeyScene;
    [Export] public PackedScene ChestScene;
    [Export] public PackedScene MinionSnakeScene;
    [Export] public PackedScene MinionEagleScene;

    private enum BossState
    {
        Idle,
        Chase,
        Telegraph,
        Attack,
        Cooldown,
        Summoning,
        Dead
    }

    private BossState _bossState = BossState.Idle;
    private float _stateTimer = 0f;
    private string _queuedAttack = "";
    private bool _hasHitTarget = false;

    // Summoning flags
    private bool _summoned70 = false;
    private bool _summoned30 = false;
    private CpuParticles2D _summonParticles;
    private Color _originalModulate = Colors.White;

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
        MaxHealth = 1500;        // Tăng từ 1000 → boss cuối phải trâu chó hơn
        AttackDamage = 35;       // Tăng từ 30 (23% MaxHP)
        MoveSpeed = 70f;         // Tăng nhẹ từ 65f
        ScoreValue = 5000;

        DetectRange = 900f;
        AttackRange = 180f;
        AttackCooldown = 1.0f;   // Giảm từ 1.2s xuống 1.0s (tấn công dồn dập hơn)

        // Health Bar offset cho nửa màn hình
        HealthBarOffset = new Vector2(-40, -220);

        base._Ready();

        // PRELOAD để tránh giật lag (Fix lỗi đơ 3s)
        if (MinionSnakeScene == null) MinionSnakeScene = GD.Load<PackedScene>("res://Scenes/Enemies/Snake.tscn");
        if (MinionEagleScene == null) MinionEagleScene = GD.Load<PackedScene>("res://Scenes/Enemies/Eagle.tscn");

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
        // SUPER ARMOR & INVULNERABILITY
        if (_bossState == BossState.Summoning)
        {
            GD.Print("[ChanTinh] INVULNERABLE! Boss is summoning minions.");
            return; // Không nhận sát thương khi đang gồng triệu hồi
        }

        bool isHeavyAttack = _queuedAttack == "attack_fire" || _queuedAttack == "attack_energy" ||
                             _queuedAttack == "attack_smash" || _queuedAttack == "attack_lightning" ||
                             _queuedAttack == "attack_tren" || _queuedAttack == "attack_chem";

        // Super Armor: Boss không bị khựng nếu đang ra chiêu nặng hoặc máu còn > 50%
        bool hasSuperArmor = (_bossState == BossState.Attack && isHeavyAttack) || (Health > MaxHealth * 0.5f);

        // Check for summoning thresholds BEFORE base.TakeDamage to prevent hit stun
        float healthPct = (float)Health / MaxHealth;
        bool triggeredSummon = false;
        if (!_summoned70 && healthPct <= 0.7f)
        {
            triggeredSummon = true;
        }
        else if (!_summoned30 && healthPct <= 0.3f)
        {
            triggeredSummon = true;
        }

        if (triggeredSummon)
        {
            base.TakeDamage(damage);
            IsHurt = false; // Bỏ qua trạng thái bị thương (đỡ đòn) để gồng triệu hồi ngay
            StartSummoning();
            return;
        }

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
            timer.Timeout += () =>
            {
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
            case BossState.Summoning:
                ProcessSummoningState(dt);
                break;
        }

        ApplyGravityAndMove(dt);
    }

    public void ResetBoss(Vector2 resetPos)
    {
        GlobalPosition = resetPos;
        _bossState = BossState.Idle;
        _stateTimer = 0.5f;
        _hasHitTarget = false;
        _queuedAttack = "";
        Velocity = Vector2.Zero;
        if (AnimSprite != null) AnimSprite.Play("idle");
        GD.Print("[ChanTinh] Boss reset to position and Idle state.");
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
            int idx = (int)(GD.Randi() % (uint)newAttacks.Length);
            _queuedAttack = newAttacks[idx];
        }
        else
        {
            int idx = (int)(GD.Randi() % (uint)_attacks.Length);
            _queuedAttack = _attacks[idx];
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

    private void StartSummoning()
    {
        _bossState = BossState.Summoning;
        _stateTimer = 1.5f;

        float healthPct = (float)Health / MaxHealth;
        if (healthPct <= 0.35f) _summoned30 = true;
        else _summoned70 = true;

        Velocity = Vector2.Zero;

        if (AnimSprite.SpriteFrames.HasAnimation("summon")) AnimSprite.Play("summon");
        else AnimSprite.Play("idle");

        _originalModulate = Modulate;

        // 1. Hiệu ứng Boss ám tím/đen (Nhưng vẫn phải nhìn rõ được Boss)
        var bossTween = CreateTween();
        // Giữ cường độ sáng ở mức 0.6-0.8 để Boss không bị đen xì
        bossTween.TweenProperty(this, "modulate", new Color(0.7f, 0.5f, 0.9f, 1.0f), 0.4f);

        // 2. Tạo hiệu ứng Aura Đen (Cải thiện hiệu năng & loại bỏ ô vuông mờ)
        if (_summonParticles == null)
        {
            _summonParticles = new CpuParticles2D();
            _summonParticles.Amount = 30;
            _summonParticles.Lifetime = 1.0f;
            _summonParticles.Explosiveness = 0f;
            _summonParticles.Spread = 40f;
            _summonParticles.Gravity = new Vector2(0, -80);
            _summonParticles.Direction = new Vector2(0, -1);
            _summonParticles.InitialVelocityMin = 40f;
            _summonParticles.InitialVelocityMax = 90f;
            _summonParticles.ScaleAmountMin = 0.5f;
            _summonParticles.ScaleAmountMax = 1.5f;

            // Dùng texture hình tròn mờ để xóa sổ "ô vuông mờ"
            _summonParticles.Texture = CreateDotTexture();

            var ramp = new Gradient();
            ramp.AddPoint(0f, new Color(0.1f, 0f, 0.2f, 0f));
            ramp.AddPoint(0.3f, new Color(0.15f, 0.05f, 0.35f, 0.4f));
            ramp.AddPoint(0.7f, new Color(0.1f, 0f, 0.2f, 0.2f));
            ramp.AddPoint(1.0f, new Color(0, 0, 0, 0));
            _summonParticles.ColorRamp = ramp;

            AddChild(_summonParticles);
            _summonParticles.Position = new Vector2(0, 50);
        }

        _summonParticles.Emitting = true;

        TriggerCameraShake(1.5f, 20f);
        GD.Print("[ChanTinh] START SUMMONING! Black Aura Charging...");
    }

    private void ProcessSummoningState(float dt)
    {
        Velocity = new Vector2(0, Velocity.Y);

        // Nhấp nháy tà khí tím (Tăng độ sáng để nhìn rõ Boss)
        float pulse = (float)Math.Sin(Time.GetTicksMsec() * 0.03f) * 0.2f + 0.8f;
        Modulate = new Color(pulse * 0.8f, pulse * 0.6f, pulse * 1.1f, 1.0f);

        if (_stateTimer <= 0)
        {
            PerformSummon();

            if (_summonParticles != null) _summonParticles.Emitting = false;

            var restoreTween = CreateTween();
            restoreTween.TweenProperty(this, "modulate", _originalModulate, 0.3f);

            _bossState = BossState.Cooldown;
            _stateTimer = 0.8f;
        }
    }

    private void PerformSummon()
    {
        GD.Print("[ChanTinh] PERFORM SUMMON! Minions appearing.");

        // Xác định số lượng dựa trên mốc máu
        float healthPct = (float)Health / MaxHealth;
        int snakeCount = (healthPct <= 0.4f) ? 2 : 2;
        int eagleCount = (healthPct <= 0.4f) ? 2 : 1;

        // Vị trí Arena: cam.LimitLeft = 2350; cam.LimitRight = 3502;
        float arenaMinX = 2400f;
        float arenaMaxX = 3450f;

        for (int i = 0; i < snakeCount; i++)
        {
            if (MinionSnakeScene != null)
            {
                var snake = MinionSnakeScene.Instantiate<CharacterBody2D>();
                GetParent().AddChild(snake);
                float randomX = (float)GD.RandRange(arenaMinX, arenaMaxX);
                snake.GlobalPosition = new Vector2(randomX, 550);
                CreateSpawnVFX(snake.GlobalPosition);
            }
        }

        for (int i = 0; i < eagleCount; i++)
        {
            if (MinionEagleScene != null)
            {
                var eagle = MinionEagleScene.Instantiate<CharacterBody2D>();
                GetParent().AddChild(eagle);
                float randomX = (float)GD.RandRange(arenaMinX, arenaMaxX);
                float randomY = (float)GD.RandRange(200, 450);
                eagle.GlobalPosition = new Vector2(randomX, randomY);
                CreateSpawnVFX(eagle.GlobalPosition);
            }
        }

        TriggerCameraShake(0.5f, 30f);
    }

    private void CreateSpawnVFX(Vector2 pos)
    {
        var explosion = new CpuParticles2D();
        explosion.GlobalPosition = pos;
        explosion.Emitting = true;
        explosion.OneShot = true;
        explosion.Amount = 20;
        explosion.Lifetime = 0.4f;
        explosion.Explosiveness = 0.9f;
        explosion.Spread = 180f;
        explosion.Gravity = Vector2.Zero;
        explosion.InitialVelocityMin = 100f;
        explosion.InitialVelocityMax = 200f;
        explosion.ScaleAmountMin = 5f;
        explosion.ScaleAmountMax = 10f;

        var colorRamp = new Gradient();
        colorRamp.AddPoint(0.0f, Colors.Cyan);
        colorRamp.AddPoint(1.0f, new Color(0, 0, 1, 0));
        explosion.ColorRamp = colorRamp;

        GetParent().AddChild(explosion);
        var timer = GetTree().CreateTimer(0.6f);
        timer.Timeout += () => { if (IsInstanceValid(explosion)) explosion.QueueFree(); };
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
        _bossState = BossState.Dead;

        // RESET màu sắc về mặc định để nhìn rõ animation chết (Fix lỗi bị mờ/đen)
        Modulate = Colors.White;

        GD.Print("[ChanTinh] DIED! Starting epic death sequence...");

        // --- SỬA LỖI: Tắt ngay lập tức các vùng va chạm để không gây sát thương khi đã chết ---
        if (HasNode("CollisionShape2D"))
            GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
        if (HasNode("HitArea/CollisionShape2D"))
            GetNode<CollisionShape2D>("HitArea/CollisionShape2D").SetDeferred("disabled", true);
        if (HasNode("DetectArea/CollisionShape2D"))
            GetNode<CollisionShape2D>("DetectArea/CollisionShape2D").SetDeferred("disabled", true);

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

        // 5. Đợi một chút trước khi biến mất (Giảm xuống 1s như yêu cầu)
        await Task.Delay(1000);

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

            // Hiệu ứng cái rương bay ra từ người boss và rơi SÁT MẶT ĐẤT (Y=615)
            var tween = chest.CreateTween();
            Vector2 targetPos = new Vector2(chest.GlobalPosition.X + (GD.Randf() > 0.5f ? 120 : -120), 615);
            tween.TweenProperty(chest, "global_position:y", chest.GlobalPosition.Y - 100, 0.5f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Chain().TweenProperty(chest, "global_position", targetPos, 0.5f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);

            GD.Print("[ChanTinh] Treasure Chest spawned!");
        }
    }

    private void CreateExplosionVFX()
    {
        // 1. Vị trí ngẫu nhiên quanh Boss
        var pos = GlobalPosition + new Vector2((float)GD.RandRange(-150, 150), (float)GD.RandRange(-300, 50));

        // 2. Sử dụng CPUParticles2D thay vì Sprite2D để tránh lỗi thiếu file (explosion.png)
        // và tạo hiệu ứng nổ lung linh, hoành tráng hơn cho Boss cuối.
        var explosion = new CpuParticles2D();
        explosion.GlobalPosition = pos;
        explosion.Emitting = true;
        explosion.OneShot = true;
        explosion.Amount = 35;
        explosion.Lifetime = 0.5f;
        explosion.Explosiveness = 0.95f;

        // Cấu hình hướng nổ tỏa tròn
        explosion.Spread = 180f;
        explosion.Gravity = Vector2.Zero;
        explosion.InitialVelocityMin = 150f;
        explosion.InitialVelocityMax = 350f;
        explosion.DampingMin = 100f;
        explosion.DampingMax = 200f;

        // Kích thước hạt (Hạt nổ to dần rồi biến mất)
        explosion.ScaleAmountMin = 8f;
        explosion.ScaleAmountMax = 18f;

        // Gradient màu nổ: Trắng -> Vàng sáng -> Cam rực -> Đỏ -> Xám đen (khói)
        var colorRamp = new Gradient();
        colorRamp.AddPoint(0.0f, Colors.White);
        colorRamp.AddPoint(0.2f, Colors.Yellow);
        colorRamp.AddPoint(0.4f, Colors.OrangeRed);
        colorRamp.AddPoint(0.7f, Colors.Red);
        colorRamp.AddPoint(1.0f, new Color(0.1f, 0.1f, 0.1f, 0f));
        explosion.ColorRamp = colorRamp;

        // Thêm vào scene (gắn vào parent để không bị di chuyển theo boss nếu boss đang chết)
        if (GetParent() != null)
        {
            GetParent().AddChild(explosion);

            // Tự hủy sau khi hoàn thành lifetime + buffer
            var timer = GetTree().CreateTimer(explosion.Lifetime + 0.2f);
            timer.Timeout += () => { if (IsInstanceValid(explosion)) explosion.QueueFree(); };
        }
        else
        {
            explosion.QueueFree();
        }
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

    private Texture2D CreateDotTexture()
    {
        int size = 64;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dx = x - (size / 2f - 0.5f);
                float dy = y - (size / 2f - 0.5f);
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float radius = size / 2.2f;
                if (dist < radius)
                {
                    float alpha = (float)Math.Pow(1.0f - (dist / radius), 2.0); // Soft falloff
                    img.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else img.SetPixel(x, y, new Color(1, 1, 1, 0));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}