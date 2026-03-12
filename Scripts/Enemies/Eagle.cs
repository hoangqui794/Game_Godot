using Godot;
using System;
using System.Collections.Generic;

public partial class Eagle : BaseEnemy
{
    // Eagle-specific properties
    [Export] public float FlyHeight = 100.0f;
    [Export] public float DiveSpeed = 380.0f;  // Tốc độ lao xuống cực nhanh
    [Export] public float FloatAmplitude = 20.0f;
    [Export] public float FloatFrequency = 2.0f;

    private float _floatTimer = 0;
    private float _baseY;
    private bool _isDiving = false;

    public override void _Ready()
    {
        // Set eagle-specific stats before base _Ready
        // ── Đại Bàng: Kẻ thù tầm trung, nguy hiểm ở tầm xa ──
        // Player cần 5 đòn thường (25×5=125 > 120) để hạ
        // đại bàng cần 6 đòn láo xuống (18×6=108) để kill player
        MaxHealth    = 120;      // Cứng gấp 3 lần rắn, kill được bằng skill mới là xứng
        AttackDamage = 18;       // 18% MaxHP mỗi nhát láo xuống
        MoveSpeed    = 110.0f;   // Bay nhanh hơn rắn
        ScoreValue   = 300;      // Xứng với mức độ nguy hiểm
        PatrolDistance = 220.0f; // Bay xa hơn
        DetectRange    = 320.0f; // Mắt sắc, nhìn rất xa
        AttackRange    = 90.0f;  // Tầm láo rộng
        AttackCooldown = 1.6f;   // Tấn công đồn dập hơn (gảm từ 2.0s)
        
        // Đẩy Thanh Máu vọt thẳng lên trời để vượt mảng Sprite của đầu Đại Bàng
        HealthBarOffset = new Vector2(-20, -75);

        base._Ready();
        _baseY = GlobalPosition.Y;
    }

    protected override void CreatePlaceholderSprites()
    {
        AnimSprite.SpriteFrames = SpriteHelper.CreateEagleSpriteFrames();
        AnimSprite.Play("walk");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
        {
            // Fall when dead
            Velocity = new Vector2(0, Velocity.Y + 500 * (float)delta);
            MoveAndSlide();
            return;
        }

        Vector2 velocity = Velocity;

        // Eagle doesn't use gravity - it flies!
        _floatTimer += (float)delta;

        switch (CurrentState)
        {
            case EnemyState.Patrol:
                if (TargetPlayer != null && !TargetPlayer.IsQueuedForDeletion())
                {
                    CurrentState = EnemyState.Chase;
                    break;
                }

                // Horizontal patrol
                velocity.X = PatrolDirection * MoveSpeed;

                // Floating motion
                float floatY = _baseY + Mathf.Sin(_floatTimer * FloatFrequency) * FloatAmplitude;
                velocity.Y = (floatY - GlobalPosition.Y) * 5.0f;

                // Check for walls (Bounce properly using Normal to avoid sticking)
                if (IsOnWall())
                {
                    float wallNormalX = GetWallNormal().X;
                    if (Math.Abs(wallNormalX) > 0.1f)
                        PatrolDirection = wallNormalX > 0 ? 1 : -1;
                    else
                        PatrolDirection *= -1;
                    velocity.X = PatrolDirection * MoveSpeed;
                }

                // Check patrol bounds
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
                    // Khi đang chờ cooldown thì lượn cao, chuẩn bị đánh thì là đà sà xuống
                    float chaseHeight = CanAttackPlayer ? 80.0f : 160.0f; 
                    
                    // Thêm dao động ngang để nó lượn lờ vòng quanh trên đầu, tránh đậu yên tĩnh
                    float offsetX = Mathf.Sin(_floatTimer * 3.0f) * 100.0f;

                    Vector2 hoverPos = TargetPlayer.GlobalPosition + new Vector2(offsetX, -chaseHeight);
                    Vector2 dirToHover = (hoverPos - GlobalPosition).Normalized();

                    // Di chuyển mượt tới điểm lượn
                    velocity = dirToHover * MoveSpeed * (CanAttackPlayer ? 2.5f : 1.2f); // Áp sát gắt hơn khi đã sẵn sàng đánh

                    // Lượn phập phồng
                    velocity.Y += Mathf.Sin(_floatTimer * FloatFrequency) * FloatAmplitude;

                    float distX = Math.Abs(GlobalPosition.X - TargetPlayer.GlobalPosition.X);
                    if (distX > 5.0f)
                    {
                        SetFacingDirection((TargetPlayer.GlobalPosition.X - GlobalPosition.X) < 0);
                    }

                    // Tấn công lao xuống nếu đủ gần và đang ở trên cao nhìn xuống
                    if (distX <= AttackRange + 20 && CanAttackPlayer && GlobalPosition.Y < TargetPlayer.GlobalPosition.Y - 30)
                    {
                        CurrentState = EnemyState.Attack;
                        _isDiving = true;
                        CreateAttackVFX();
                    }
                }
                else
                {
                    CurrentState = EnemyState.Patrol;
                }
                AnimSprite.Play("walk");
                break;

            case EnemyState.Attack:
                if (_isDiving && TargetPlayer != null)
                {
                    // Dive attack directly at player
                    Vector2 diveDir = (TargetPlayer.GlobalPosition - GlobalPosition).Normalized();
                    velocity = diveDir * DiveSpeed;
                    AnimSprite.Play("attack");
                    
                    float distX = Math.Abs(GlobalPosition.X - TargetPlayer.GlobalPosition.X);
                    if (distX > 5.0f) 
                        SetFacingDirection(diveDir.X < 0);

                    // If eagle has reached player's height or lower, pull up immediately
                    if (GlobalPosition.Y >= TargetPlayer.GlobalPosition.Y - 20 || IsOnFloor())
                    {
                        _isDiving = false;
                        CanAttackPlayer = false;
                        AttackCooldownTimer.Start();
                        
                        // Không bay tuốt lên trời để tẩu thoát nữa, quay lại vòng lặp Chase áp sát ngay!
                        CurrentState = EnemyState.Chase;
                    }
                }
                else
                {
                    // Nếu kẹt state tấn công thì tự sửa lỗi quay về chase
                    CurrentState = EnemyState.Chase;
                }
                break;

            case EnemyState.Hurt:
                // Rơi nhẹ do mất trọng tâm khi bị chém, thay vì đứng im lơ lửng
                velocity.X = 0;
                velocity.Y = 50f; 
                _isDiving = false; 
                AnimSprite.Play("hurt");
                break;

            case EnemyState.Dead:
                velocity.X = 0;
                velocity.Y += 500 * (float)delta; // Fall when dead
                break;
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}
