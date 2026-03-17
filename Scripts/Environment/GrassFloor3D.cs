using Godot;

/// <summary>
/// Tự động sinh lưới cỏ hexagon phủ kín mặt đất.
/// Gắn script này vào Node3D, set GrassScene = grass.glb, 
/// rồi chỉ cần đặt Width/Depth để định kích thước sàn.
/// </summary>
public partial class GrassFloor3D : Node3D
{
    [Export] public PackedScene GrassScene;
    [Export] public float TileSize = 2.0f;       // Scale mỗi tile
    [Export] public float TileSpacingX = 1.85f;   // Khoảng cách X giữa 2 tile
    [Export] public float TileSpacingZ = 1.6f;    // Khoảng cách Z giữa 2 hàng
    [Export] public float HexOffsetX = 0.925f;    // Offset X cho hàng lẻ (hex tessellation)
    [Export] public int ColumnsX = 20;            // Số cột theo trục X
    [Export] public int RowsZ = 5;                // Số hàng theo trục Z
    [Export] public float StartX = 0f;            // Vị trí X bắt đầu
    [Export] public float CenterZ = 0f;           // Tâm Z

    public override void _Ready()
    {
        if (GrassScene == null)
        {
            // Tự load nếu chưa set
            GrassScene = GD.Load<PackedScene>("res://Assets/Envaironment/Models/GLB format/grass.glb");
        }

        if (GrassScene == null)
        {
            GD.PrintErr("GrassFloor3D: Không tìm thấy grass.glb!");
            return;
        }

        GenerateGrassGrid();
    }

    private void GenerateGrassGrid()
    {
        float totalDepth = RowsZ * TileSpacingZ;
        float startZ = CenterZ - totalDepth / 2f;

        for (int row = 0; row < RowsZ; row++)
        {
            float z = startZ + row * TileSpacingZ;
            float xOffset = (row % 2 == 1) ? HexOffsetX : 0f;

            for (int col = 0; col < ColumnsX; col++)
            {
                float x = StartX + col * TileSpacingX + xOffset;

                var tile = GrassScene.Instantiate<Node3D>();
                tile.Transform = new Transform3D(
                    new Basis(Vector3.Right * TileSize, Vector3.Up * TileSize, Vector3.Back * TileSize),
                    new Vector3(x, 0, z)
                );
                AddChild(tile);
            }
        }

        GD.Print($"GrassFloor3D: Tạo {ColumnsX * RowsZ} ô cỏ ({ColumnsX}x{RowsZ})");
    }
}
