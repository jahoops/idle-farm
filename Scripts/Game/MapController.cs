using Godot;

public partial class MapController : Node2D
{
	private TileMapLayer _soilLayer;
	private TileMapLayer _cropLayer;
	private TileMapLayer _highlightLayer;

	public override void _Ready()
	{
		_soilLayer = GetNode<TileMapLayer>("SoilLayer");
		_cropLayer = GetNode<TileMapLayer>("CropLayer");

		GD.Print("MapController ready");
	}

	public void DrawPlot(Vector2I position, PlotState plot)
	{
		GD.Print($"DrawPlot ({position.X},{position.Y}): {plot.Status}");

		if (plot.Status == PlotStatus.Empty)
		{
			_cropLayer?.EraseCell(position);
		}
		else if (plot.CropType == CropType.CropB)
		{
			if (plot.Status == PlotStatus.Planted)
			{
				_cropLayer?.SetCell(position, 1, new Vector2I(41, 23));
			}
			else if (plot.Status == PlotStatus.Growing)
			{
				_cropLayer?.SetCell(position, 1, new Vector2I(41, 24));
			}
			else if (plot.Status == PlotStatus.Ready)
			{
				_cropLayer?.SetCell(position, 1, new Vector2I(42, 24));
			}
		}
		else
		{
			if (plot.Status == PlotStatus.Planted)
			{
				_cropLayer?.SetCell(position, 1, new Vector2I(44, 23)); // planted visual
			}
			else if (plot.Status == PlotStatus.Growing)
			{
				_cropLayer?.SetCell(position, 1, new Vector2I(44, 24)); // growing visual
			}
			else if (plot.Status == PlotStatus.Ready)
			{
				_cropLayer?.SetCell(position, 1, new Vector2I(43, 24)); // ready visual
			}
		}

		_cropLayer?.UpdateInternals();
	}

	public Vector2I GetClickedCell(Vector2 globalMousePosition)
	{
		return _soilLayer.LocalToMap(_soilLayer.ToLocal(globalMousePosition));
	}
}
