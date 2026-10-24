using System.Collections.Generic;
using Godot;

public class GameState
{
	public int Currency { get; set; }
	public Dictionary<Vector2I, PlotState> Plots { get; set; }
	public int PlantCost { get; } = 10;
	public int HarvestValue { get; set; } = 15;
	public float GrowthDuration { get; set; } = 2.0f;

	public GameState()
	{
		Currency = 30;
		Plots = new Dictionary<Vector2I, PlotState>();
		for (int y = 0; y <= 2; y++)
		{
			for (int x = 0; x <= 2; x++)
			{
				Plots[new Vector2I(x, y)] = new PlotState();
			}
		}
	}

	public bool PlantPlot(Vector2I position, double plantedTime, CropType cropType = CropType.CropB)
	{
		if (!Plots.TryGetValue(position, out var plot))
		{
			return false;
		}

		if (plot.Status != PlotStatus.Empty)
		{
			return false;
		}

		if (Currency < PlantCost)
		{
			return false;
		}

		Currency -= PlantCost;
		plot.Status = PlotStatus.Planted;
		plot.PlantedTime = plantedTime;
		plot.CropType = cropType;
		return true;
	}

	public bool UpdatePlotToReady(Vector2I position, double currentTime, double growDuration)
	{
		if (!Plots.TryGetValue(position, out var plot))
		{
			return false;
		}

		if (plot.Status != PlotStatus.Planted)
		{
			return false;
		}

		if (currentTime - plot.PlantedTime < growDuration)
		{
			return false;
		}

		plot.Status = PlotStatus.Ready;
		return true;
	}

	public bool HarvestPlot(Vector2I position, int harvestValue)
	{
		if (!Plots.TryGetValue(position, out var plot))
		{
			return false;
		}

		if (plot.Status != PlotStatus.Ready)
		{
			return false;
		}

		var harvestedCropType = plot.CropType;
		plot.Status = PlotStatus.Empty;
		plot.PlantedTime = 0;
		plot.CropType = CropType.None;
		Currency += GetHarvestValue(harvestedCropType);
		return true;
	}

	public bool UpgradeHarvestValue()
	{
		const int upgradeCost = 50;
		if (Currency < upgradeCost)
		{
			return false;
		}

		Currency -= upgradeCost;
		HarvestValue += 5;
		return true;
	}

	public bool UpgradeGrowthSpeed()
	{
		const int upgradeCost = 50;
		if (Currency < upgradeCost)
		{
			return false;
		}

		Currency -= upgradeCost;
		GrowthDuration -= 0.2f;
		if (GrowthDuration < 0.5f)
		{
			GrowthDuration = 0.5f;
		}

		return true;
	}

	public int GetHarvestValue(CropType cropType)
	{
		if (cropType == CropType.CropB)
		{
			return HarvestValue * 2;
		}

		return HarvestValue;
	}

	public float GetGrowthDuration(CropType cropType)
	{
		if (cropType == CropType.CropB)
		{
			return GrowthDuration * 1.5f;
		}

		return GrowthDuration;
	}
}
