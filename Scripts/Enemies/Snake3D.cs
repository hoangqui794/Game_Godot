using Godot;

public partial class Snake3D : BaseEnemy3D
{
    public override void _Ready()
    {
        MaxHealth = 100;
        AttackDamage = 15;
        MoveSpeed = 2.5f; // Adjust for 3D scale
        ScoreValue = 150;
        PatrolDistance = 4.0f;
        DetectRange = 8.0f;
        AttackRange = 1.2f;
        AttackCooldown = 1.5f;

        base._Ready();
    }

    protected override void CreatePlaceholderSprites()
    {
        // Snakes use their specific SpriteFrames
        try {
            AnimSprite.SpriteFrames = SpriteHelper.CreateSnakeSpriteFrames();
            AnimSprite.Play("walk");
        } catch (System.Exception e) {
            GD.PrintErr("Snake3D: Failed to create SpriteFrames: " + e.Message);
        }
    }

    protected override void OnAnimationFinished()
    {
        base.OnAnimationFinished();
        if (AnimSprite.Animation == "attack")
        {
            // Simplified VFX for 3D version
            // In 2D we used many polygons, maybe for 3D we can just play a sound
        }
    }
}
