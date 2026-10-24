using System.Collections.Generic;
using Godot;

public class SaveData
{
	public int Currency;
	public Dictionary<Vector2I, PlotState> Plots;
	public int HarvestValue;
	public float GrowthDuration;
}
