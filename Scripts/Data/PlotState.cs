public enum PlotStatus
{
	Empty,
	Planted,
	Growing,
	Ready
}

public enum CropType
{
	None,
	CropA,
	CropB
}

public class PlotState
{
	public PlotStatus Status { get; set; }
	public double PlantedTime { get; set; }
	public CropType CropType { get; set; }

	public PlotState()
	{
		Status = PlotStatus.Empty;
		PlantedTime = -1;
		CropType = CropType.None;
	}
}
