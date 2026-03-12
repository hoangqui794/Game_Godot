using Godot;
using System;
using System.Collections.Generic;

public partial class BaseEnemy : CharacterBody2D
{
    // Stats
    [Export] public int MaxHealth = 50;
    [Export] public int AttackDamage = 10;
    [Export] public float MoveSpeed = 80.0f;
    [Export] public float Gravity = 980.0f;
    [Export] public int ScoreValue = 100;

    // Patrol
    [Export] public float PatrolDistance = 150.0f;
    [Export] public float DetectRange = 250.0f;
    [Export] public float AttackRange = 40.0f;
    [Export] public float AttackCooldown = 1.0f;

    // State
    protected int Health;
    public bool IsDead = false;
    protected bool IsHurt = false;
    protected bool CanAttackPlayer = true;

    // Patrol
    protected Vector2 StartPosition;
    protected int PatrolDirection = 1;

    // Components
    protected AnimatedSprite2D AnimSprite;
    protected Area2D DetectArea;
    protected Area2D AttackArea;
    protected Timer AttackCooldownTimer;
    protected Timer HurtTimer;
    protected Timer DeathTimer;

    // UI
    [Export] public Vector2 HealthBarOffset = new Vector2(-20, -60); // Vị trí thanh máu trên đầu
    protected Node2D _healthBarNode;
    protected ColorRect _healthBarFill;

    // Player reference
    protected Player TargetPlayer;

    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack,
        Hurt,
        Dead
    }

    protected EnemyState CurrentState = EnemyState.Patrol;

    public override void _Ready()
    {
        Health = MaxHealth;
        StartPosition = GlobalPosition;
        AddToGroup("enemies");

        // Get components
        AnimSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Auto-create sprites if none assigned
        if (AnimSprite.SpriteFrames == null)
        {
            CreatePlaceholderSprites();
        }

        // Create attack cooldown timer
        AttackCooldownTimer = new Timer();
        AttackCooldownTimer.WaitTime = AttackCooldown;
        AttackCooldownTimer.OneShot = true;
        AttackCooldownTimer.Timeout += () => { CanAttackPlayer = true; };
        AddChild(AttackCooldownTimer);

        // Create hurt timer
        HurtTimer = new Timer();
        HurtTimer.WaitTime = 0.3f;
        HurtTimer.OneShot = true;
        HurtTimer.Timeout += () => { IsHurt = false; CurrentState = EnemyState.Patrol; };
        AddChild(HurtTimer);

        // Create death timer
        DeathTimer = new Timer();
        DeathTimer.WaitTime = 1.0f;
        DeathTimer.OneShot = true;
        DeathTimer.Timeout += OnDeathTimerTimeout;
        AddChild(DeathTimer);

        // Setup detect area if it exists
        if (HasNode("DetectArea"))
        {
            DetectArea = GetNode<Area2D>("DetectArea");
            DetectArea.BodyEntered += OnDetectAreaBodyEntered;
            DetectArea.BodyExited += OnDetectAreaBodyExited;
        }

        // Setup attack area if it exists  
        if (HasNode("HitArea"))
        {
            AttackArea = GetNode<Area2D>("HitArea");
            AttackArea.BodyEntered += OnHitAreaBodyEntered;
        }

        AnimSprite.AnimationFinished += OnAnimationFinished;

        // Khởi tạo Thanh Máu Xịn cho tất cả loại Quái!
        SetupHealthBar();
    }

    protected void SetupHealthBar()
    {
        // Wrapper node
        _healthBarNode = new Node2D();
        _healthBarNode.Position = HealthBarOffset; // Vị trí neo lơ lửng trên đầu quái

        // 1. Viền đen ngoài cùng (Black Border)
        ColorRect border = new ColorRect();
        border.Color = new Color(0.05f, 0.05f, 0.05f, 1f); // Đen kịt điểm xuyết
        border.Size = new Vector2(40, 8); // Kích thước tổng
        border.Position = new Vector2(0, 0);
        border.MouseFilter = Control.MouseFilterEnum.Ignore;

        // 2. Nền xám tối (Dark background for missing health)
        ColorRect bg = new ColorRect();
        bg.Color = new Color(0.2f, 0.2f, 0.2f, 1f); // Xám tro
        bg.Size = new Vector2(38, 6); // Nhỏ hơn viền 2 pixel (thụt vô 1 pixel mỗi cạnh)
        bg.Position = new Vector2(1, 1);
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;

        // 3. Thanh lõi Xanh Lục (Health Fill)
        _healthBarFill = new ColorRect();
        _healthBarFill.Color = new Color("4caf50"); // Xanh lục tươi chuẩn RPG
        _healthBarFill.Size = new Vector2(38, 6);
        _healthBarFill.Position = new Vector2(1, 1);
        _healthBarFill.MouseFilter = Control.MouseFilterEnum.Ignore;

        border.AddChild(bg);
        _healthBarNode.AddChild(border);
        _healthBarNode.AddChild(_healthBarFill);

        AddChild(_healthBarNode);
    }

    /// <summary>
    /// Override in subclasses for custom sprites. Default creates colored rectangles.
    /// </summary>
    protected virtual void CreatePlaceholderSprites()
    {
        var tex = SpriteHelper.CreateColoredRect(32, 32, Colors.Red);
        var animations = new Dictionary<string, Texture2D[]>
        {
            { "walk", new Texture2D[] { tex } },
            { "attack", new Texture2D[] { tex } },
            { "hurt", new Texture2D[] { tex } },
            { "die", new Texture2D[] { tex } }
        };
        AnimSprite.SpriteFrames = SpriteHelper.BuildSpriteFrames(animations);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;

        Vector2 velocity = Velocity;

        // Apply gravity
        if (!IsOnFloor())
        {
            velocity.Y += Gravity * (float)delta;
        }
        else
        {
            // --- FIX KẸT TRÊN ĐẦU PLAYER ---
            // Kiểm tra xem quái có đang đứng đè lên Player không
            var floorCollision = GetLastSlideCollision();
            if (floorCollision != null && floorCollision.GetCollider() is Player player)
            {
                // Nếu đang đứng trên đầu Player, trượt quái sang một bên để không bị "dính"
                float pushDir = GlobalPosition.X > player.GlobalPosition.X ? 1.0f : -1.0f;
                velocity.X += pushDir * 500f * (float)delta; 
            }
        }

        switch (CurrentState)
        {
            case EnemyState.Patrol:
                velocity.X = PatrolDirection * MoveSpeed;

                // Check for walls an toàn trước (Chống kẹt dốc nhỏ lồi lõm dùng GetWallNormal)
                if (IsOnWall())
                {
                    float wallNormalX = GetWallNormal().X;
                    if (Math.Abs(wallNormalX) > 0.1f)
                    {
                        PatrolDirection = wallNormalX > 0 ? 1 : -1;
                    }
                    else
                    {
                        PatrolDirection *= -1;
                    }
                    velocity.X = PatrolDirection * MoveSpeed; // Cập nhật ngay lập tức để không lùi vào tường ở next frame
                }

                // Check patrol bounds: Khóa chân không cho ra khỏi vùng
                float distFromStart = GlobalPosition.X - StartPosition.X;
                if (distFromStart >= PatrolDistance && PatrolDirection > 0)
                {
                    PatrolDirection = -1;
                    velocity.X = PatrolDirection * MoveSpeed;
                }
                else if (distFromStart <= -PatrolDistance && PatrolDirection < 0)
                {
                    PatrolDirection = 1;
                    velocity.X = PatrolDirection * MoveSpeed;
                }

                SetFacingDirection(PatrolDirection < 0);
                AnimSprite.Play("walk");
                break;

            case EnemyState.Chase:
                if (TargetPlayer != null && !TargetPlayer.IsQueuedForDeletion())
                {
                    float distX = Math.Abs(TargetPlayer.GlobalPosition.X - GlobalPosition.X);
                    // Luôn luôn nhận diện hướng nhìn về người chơi
                    float dirToPlayer = TargetPlayer.GlobalPosition.X > GlobalPosition.X ? 1f : -1f;

                    // Chỉ lật mặt nếu khoảng cách an toàn (tránh jitter 60 nhịp/giây khi rúc vào nhau)
                    if (distX > 5.0f)
                    {
                        SetFacingDirection(dirToPlayer < 0);
                    }

                    float dist = GlobalPosition.DistanceTo(TargetPlayer.GlobalPosition);

                    if (dist <= AttackRange)
                    {
                        velocity.X = 0; // Đứng yên để đánh
                        if (CanAttackPlayer)
                        {
                            CurrentState = EnemyState.Attack;
                            CreateAttackVFX();

                            // Xử lý lỗi đánh hụt khi người chơi đứng yên: Quét lại HitArea
                            if (AttackArea != null)
                            {
                                var checkHitTimer = GetTree().CreateTimer(0.1f);
                                checkHitTimer.Timeout += () =>
                                {
                                    if (!IsInstanceValid(this) || IsQueuedForDeletion() || !IsInstanceValid(AttackArea)) return;
                                    var bodies = AttackArea.GetOverlappingBodies();
                                    foreach (var body in bodies)
                                        OnHitAreaBodyEntered(body);
                                };
                            }
                        }
                        else
                        {
                            // Né việc vừa Chase vừa chém khi Cooldown -> Lùi vô Idle
                            if (AnimSprite.SpriteFrames != null && AnimSprite.SpriteFrames.HasAnimation("idle"))
                                AnimSprite.Play("idle");
                            else
                                AnimSprite.Play("walk");
                        }
                    }
                    else
                    {
                        velocity.X = dirToPlayer * MoveSpeed * 1.5f;
                        AnimSprite.Play("walk");
                    }
                }
                else
                {
                    CurrentState = EnemyState.Patrol;
                }
                break;

            case EnemyState.Attack:
                velocity.X = 0;
                // Khi đứng đánh lúc nào cũng khóa Mục tiêu để phang đối thủ (xoay liên tục theo người chơi)
                if (TargetPlayer != null && !TargetPlayer.IsQueuedForDeletion())
                {
                    float dirToPlayer = TargetPlayer.GlobalPosition.X > GlobalPosition.X ? 1f : -1f;
                    if (Math.Abs(TargetPlayer.GlobalPosition.X - GlobalPosition.X) > 5.0f)
                        SetFacingDirection(dirToPlayer < 0);
                }

                if (!IsHurt)
                {
                    AnimSprite.Play("attack");
                }
                break;

            case EnemyState.Hurt:
                velocity.X = 0;
                AnimSprite.Play("hurt");
                break;

            case EnemyState.Dead:
                velocity.X = 0;
                break;
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    public virtual void TakeDamage(int damage)
    {
        if (IsDead) return;

        Health -= damage;
        GD.Print($"Enemy took {damage} damage, health now: {Health}");
        IsHurt = true;
        CurrentState = EnemyState.Hurt;
        HurtTimer.Start();

        // Cập nhật Size thanh máu Pixel-Perfect
        if (_healthBarFill != null && IsInstanceValid(_healthBarFill))
        {
            float percent = Math.Max(0f, (float)Health / MaxHealth);
            _healthBarFill.Size = new Vector2(38f * percent, 6f);
        }

        // Flash effect
        AnimSprite.Modulate = new Color(1, 0.3f, 0.3f);
        var tween = CreateTween();
        tween.TweenProperty(AnimSprite, "modulate", Colors.White, 0.3f);

        if (Health <= 0)
        {
            GD.Print("Enemy health <=0, calling Die()");
            Die();
        }
    }

    protected virtual void Die()
    {
        // Khi chết thu hồi Xóa bỏ thanh Máu
        if (_healthBarNode != null && IsInstanceValid(_healthBarNode))
        {
            _healthBarNode.QueueFree();
            _healthBarNode = null;
        }

        IsDead = true;
        CurrentState = EnemyState.Dead;
        AnimSprite.Play("die");
        GameManager.Instance.AddScore(ScoreValue);

        // Hồi máu cho player khi giết quái
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null && !player.IsQueuedForDeletion())
        {
            GD.Print("Enemy died, healing player");
            int healAmount = (int)(GameManager.Instance.MaxPlayerHealth * 0.25f);
            player.Heal(healAmount);
        }

        // Disable collisions
        GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
        if (HasNode("DetectArea/CollisionShape2D"))
            GetNode<CollisionShape2D>("DetectArea/CollisionShape2D").SetDeferred("disabled", true);
        if (HasNode("HitArea/CollisionShape2D"))
            GetNode<CollisionShape2D>("HitArea/CollisionShape2D").SetDeferred("disabled", true);
        DeathTimer.Start();
    }

    private void OnDeathTimerTimeout()
    {
        QueueFree();
    }

    protected virtual void OnAnimationFinished()
    {
        if (AnimSprite.Animation == "attack")
        {
            if (TargetPlayer != null)
            {
                CurrentState = EnemyState.Chase;
            }
            else
            {
                CurrentState = EnemyState.Patrol;
            }
            CanAttackPlayer = false;
            AttackCooldownTimer.Start();
        }
    }

    private void OnDetectAreaBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            TargetPlayer = player;
            if (CurrentState == EnemyState.Patrol)
            {
                CurrentState = EnemyState.Chase;
            }
        }
    }

    private void OnDetectAreaBodyExited(Node2D body)
    {
        if (body is Player)
        {
            TargetPlayer = null;
            CurrentState = EnemyState.Patrol;
        }
    }

    private void OnHitAreaBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            player.TakeDamage(AttackDamage);
        }
    }

    protected virtual void SetFacingDirection(bool faceLeft)
    {
        AnimSprite.FlipH = faceLeft;

        // Lưu ý: Lật HitArea theo hướng mặt để quái chém chính xác về phía mặt
        if (AttackArea != null)
        {
            var attackPos = AttackArea.Position;
            float sign = faceLeft ? -1f : 1f;
            attackPos.X = sign * Math.Abs(attackPos.X);
            AttackArea.Position = attackPos;
        }
    }

    protected virtual void CreateAttackVFX()
    {
        // Container cho toàn bộ hiệu ứng chém
        var slashNode = new Node2D();
        float faceSign = AnimSprite.FlipH ? -1f : 1f;

        // Căn chỉnh ra trước mặt quái
        slashNode.Position = new Vector2(faceSign * 40, -30);
        AddChild(slashNode);

        // Tạo 3 luồng chém móng vuốt (3 Claws)
        for (int i = 0; i < 3; i++)
        {
            var claw = new ColorRect();
            claw.Size = new Vector2(6, 70); // Dài và sắc
            claw.PivotOffset = new Vector2(3, 35);

            // Xếp 3 vết cào nằm cách nhau và nghiêng chéo
            claw.Position = new Vector2(-15 + (i * 15), -35);
            claw.Rotation = faceSign * 0.4f + (i * 0.1f); // Xoè nhẹ ra như bàn tay cào

            slashNode.AddChild(claw);

            // Animation từng móng vuốt
            var tw = CreateTween();
            tw.SetParallel(true);

            // Móng vuốt trồi lên từ nhỏ xíu và chém phập xuống
            claw.Scale = new Vector2(0.1f, 0.1f);
            tw.TweenProperty(claw, "scale", new Vector2(1.2f, 1.5f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

            // Chớp màu từ Vàng Trắng rực rỡ nảy sang Đỏ Máu nhạt dần
            claw.Color = Colors.Yellow;
            tw.TweenProperty(claw, "color", new Color(1.0f, 0.0f, 0.0f, 0.9f), 0.1f);

            // Mờ dần tiêu biến
            tw.TweenProperty(claw, "modulate:a", 0f, 0.2f).SetDelay(0.1f);
        }

        // Tạo hiệu ứng vụn văng tung tóe (Tia chém / Máu văng)
        var sparks = new CpuParticles2D();
        sparks.Emitting = true;
        sparks.OneShot = true;
        sparks.Amount = 20;
        sparks.Lifetime = 0.4f;
        sparks.Explosiveness = 0.95f;
        sparks.EmissionShape = CpuParticles2D.EmissionShapeEnum.Point;

        // Hướng bắn văng theo chiều chém
        sparks.Direction = new Vector2(faceSign, -0.5f);
        sparks.Spread = 45f;
        sparks.Gravity = new Vector2(0, 300); // Rơi mạnh
        sparks.InitialVelocityMin = 150f;
        sparks.InitialVelocityMax = 300f;

        // Hạt nhỏ li ti chớp đỏ vang
        sparks.ScaleAmountMin = 2f;
        sparks.ScaleAmountMax = 5f;
        sparks.Color = new Godot.Color(1f, 0.2f, 0.1f, 0.8f);
        slashNode.AddChild(sparks);

        // Lắc nhẹ không gian quanh vệt chém tạo lực tác động vật lý
        var shakeTw = CreateTween();
        shakeTw.TweenProperty(slashNode, "position", slashNode.Position + new Vector2(faceSign * 10, 5), 0.05f);
        shakeTw.TweenProperty(slashNode, "position", slashNode.Position, 0.1f);

        // Hủy Node sau khi xong xuôi
        var cleanupTween = CreateTween();
        cleanupTween.TweenInterval(0.6f);
        cleanupTween.TweenCallback(Callable.From(() => { if (IsInstanceValid(slashNode)) slashNode.QueueFree(); }));
    }
}
