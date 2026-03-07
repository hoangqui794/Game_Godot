using Godot;

/// <summary>
/// Vùng chết - đặt dưới đáy hố sâu.
/// Khi Player rơi vào → chết ngay lập tức.
/// </summary>
public partial class KillZone : Area2D
{
    public override void _Ready()
    {
        // Kết nối signal khi body vào vùng
        BodyEntered += OnBodyEntered;

        // Đảm bảo collision mask bao gồm Player (layer 1) và Enemies (layer 4)
        CollisionLayer = 0;
        CollisionMask = 1 | 4; // Detect Player and Enemies
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            // Giết player ngay lập tức
            player.TakeDamage(9999);
        }
        else if (body is BaseEnemy enemy)
        {
            // Giết quái ngay lập tức nếu rơi xuống hố
            enemy.TakeDamage(9999);
        }
    }
}
