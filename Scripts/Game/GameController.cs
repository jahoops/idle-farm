using System.Text.Json;
using Godot;

public partial class GameController : Node
{
	private const string SavePath = "user://save.json";
	private const int GoalCurrency = 1000;
	private GameState? _gameState;
	private Label? _currencyLabel;
	private Label? _messageLabel;
	private Button? _upgradeHarvestButton;
	private Button? _upgradeSpeedButton;
	private Button? _cropToggleButton;
	private Button? _harvestAllButton;
	private Button? _plantAllButton;
	private CropType _selectedCrop = CropType.CropB;
	private float _messageTimer = 0f;
	private bool _goalReached = false;

	public override async void _Ready()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		_messageLabel = GetNode<Label>("../MessageLabel");
		_currencyLabel = GetNode<Label>("../CurrencyLabel");
		_upgradeHarvestButton = GetNode<Button>("../UpgradeHarvestButton");
		_upgradeHarvestButton.Pressed += OnUpgradeHarvestPressed;
		_upgradeSpeedButton = GetNode<Button>("../UpgradeSpeedButton");
		_upgradeSpeedButton.Pressed += OnUpgradeSpeedPressed;
		_cropToggleButton = GetNode<Button>("../CropToggleButton");
		_cropToggleButton.Pressed += OnCropTogglePressed;
		_harvestAllButton = GetNode<Button>("../HarvestAllButton");
		_harvestAllButton.Pressed += OnHarvestAllPressed;
		_plantAllButton = GetNode<Button>("../PlantAllButton");
		_plantAllButton.Pressed += OnPlantAllPressed;

		GD.Print("GameController ready");
		LoadGame();
		RefreshUI();
		if (_cropToggleButton != null)
		{
			_cropToggleButton.Text = _selectedCrop == CropType.CropA
				? "Crop: A (Fast, Low Value)"
				: "Crop: B (Slow, High Value)";
		}
		GD.Print($"Currency: {_gameState?.Currency}");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButtonEvent
			&& mouseButtonEvent.Pressed
			&& mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			if (_gameState == null)
			{
				return;
			}

			var mapController = GetNode<MapController>("../Map");
			var clickedCell = mapController.GetClickedCell(GetViewport().GetMousePosition());

			if (!_gameState.Plots.ContainsKey(clickedCell))
			{
				GD.Print("No plot at clicked cell");
				return;
			}

			if (_gameState.Plots[clickedCell].Status == PlotStatus.Empty)
			{
				double plantedTime = Time.GetUnixTimeFromSystem();
				bool planted = _gameState.PlantPlot(clickedCell, plantedTime, _selectedCrop);

				if (planted)
				{
					GD.Print($"Planted at ({clickedCell.X},{clickedCell.Y})");
					mapController.DrawPlot(clickedCell, _gameState.Plots[clickedCell]);
					UpdateCurrencyUI();
					SaveGame();
				}
				else
				{
					GD.Print("Not enough currency");
					ShowMessage("Not enough currency");
				}
			}

			if (_gameState.Plots[clickedCell].Status == PlotStatus.Ready)
			{
				var plot = _gameState.Plots[clickedCell];
				var value = _gameState.GetHarvestValue(plot.CropType);
				if (_gameState.HarvestPlot(clickedCell, 25))
				{
					mapController.DrawPlot(clickedCell, plot);
					GD.Print($"Harvested at ({clickedCell.X},{clickedCell.Y})");
					ShowMessage($"Harvested {plot.CropType} +{value}");
					UpdateCurrencyUI();
					SaveGame();
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_messageTimer > 0f)
		{
			_messageTimer -= (float)delta;
			if (_messageTimer <= 0f && _messageLabel != null)
			{
				_messageLabel.Text = "";
			}
		}

		if (_gameState == null)
		{
			return;
		}

		var mapController = GetNode<MapController>("../Map");
		var mousePosition = GetViewport().GetMousePosition();
		double currentTime = Time.GetUnixTimeFromSystem();

		foreach (var plotEntry in _gameState.Plots)
		{
			var plot = plotEntry.Value;
			double growthDuration = _gameState.GetGrowthDuration(plot.CropType);
			double halfGrowthDuration = growthDuration / 2;
			double elapsed = currentTime - plot.PlantedTime;

			if (plot.Status == PlotStatus.Planted)
			{
				if (elapsed >= halfGrowthDuration)
				{
					plot.Status = PlotStatus.Growing;
					mapController.DrawPlot(plotEntry.Key, plot);
				}
			}
			else if (plot.Status == PlotStatus.Growing)
			{
				if (elapsed >= growthDuration)
				{
					plot.Status = PlotStatus.Ready;
					mapController.DrawPlot(plotEntry.Key, plot);
				}
			}
		}

		CheckGoal();
	}

	private void LoadGame()
	{
		if (FileAccess.FileExists(SavePath))
		{
			using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
			var json = file.GetAsText();
			var parseResult = Json.ParseString(json);

			_gameState = new GameState();

			if (parseResult.VariantType == Variant.Type.Dictionary)
			{
				var data = (Godot.Collections.Dictionary)parseResult;

				if (data.ContainsKey("Currency"))
				{
					_gameState.Currency = (int)data["Currency"];
				}

				if (data.ContainsKey("HarvestValue"))
				{
					var harvestValueProperty = typeof(GameState).GetProperty("HarvestValue");
					harvestValueProperty?.SetValue(_gameState, (int)data["HarvestValue"]);
				}

				if (data.ContainsKey("GrowthDuration"))
				{
					var growthDurationProperty = typeof(GameState).GetProperty("GrowthDuration");
					growthDurationProperty?.SetValue(_gameState, (float)(double)data["GrowthDuration"]);
				}

				if (data.ContainsKey("Plots"))
				{
					var plotsVariant = data["Plots"];
					if (plotsVariant.VariantType == Variant.Type.Dictionary)
					{
						var plotsData = (Godot.Collections.Dictionary)plotsVariant;
						var loadedPlots = new System.Collections.Generic.Dictionary<Vector2I, PlotState>();

						foreach (var keyVariant in plotsData.Keys)
						{
							var keyString = keyVariant.AsString();
							var parts = keyString.Split(',');
							if (parts.Length != 2)
							{
								continue;
							}

							if (!int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
							{
								continue;
							}

							var plotDataVariant = plotsData[keyVariant];
							if (plotDataVariant.VariantType != Variant.Type.Dictionary)
							{
								continue;
							}

							var plotData = (Godot.Collections.Dictionary)plotDataVariant;
							var plot = new PlotState();

							if (plotData.ContainsKey("Status"))
							{
								plot.Status = (PlotStatus)(int)plotData["Status"];
							}

							if (plotData.ContainsKey("PlantedTime"))
							{
								plot.PlantedTime = (double)plotData["PlantedTime"];
							}

							if (plotData.ContainsKey("CropType"))
							{
								plot.CropType = (CropType)(int)plotData["CropType"];
							}

							loadedPlots[new Vector2I(x, y)] = plot;
						}

						if (loadedPlots.Count > 0)
						{
							_gameState.Plots = loadedPlots;
						}
					}
				}
			}
		}
		else
		{
			_gameState = new GameState();
		}

		foreach (var cell in _gameState.Plots.Keys)
		{
			RefreshPlotStateFromTime(cell);
		}

		var mapController = GetNode<MapController>("../Map");
		foreach (var plotEntry in _gameState.Plots)
		{
			mapController.DrawPlot(plotEntry.Key, plotEntry.Value);
		}

		GD.Print($"Loaded Currency: {_gameState.Currency}");
		GD.Print($"Loaded Plots: {_gameState.Plots.Count}");
		ShowMessage("Game loaded");

		RefreshUI();
	}

	private void UpdateCurrencyUI()
	{
		if (_gameState == null || _currencyLabel == null)
		{
			return;
		}

		_currencyLabel.Text = $"Currency: {_gameState.Currency} / {GoalCurrency}";
	}

	private void SaveGame()
	{
		if (_gameState == null)
		{
			return;
		}

		var saveData = new SaveData
		{
			Currency = _gameState.Currency,
			Plots = _gameState.Plots,
			HarvestValue = _gameState.HarvestValue,
			GrowthDuration = _gameState.GrowthDuration
		};

		var plotsData = new Godot.Collections.Dictionary();
		foreach (var plotEntry in saveData.Plots)
		{
			plotsData[$"{plotEntry.Key.X},{plotEntry.Key.Y}"] = new Godot.Collections.Dictionary
			{
				{ "Status", (int)plotEntry.Value.Status },
				{ "PlantedTime", plotEntry.Value.PlantedTime },
				{ "CropType", (int)plotEntry.Value.CropType }
			};
		}

		var data = new Godot.Collections.Dictionary
		{
			{ "Currency", saveData.Currency },
			{ "HarvestValue", saveData.HarvestValue },
			{ "GrowthDuration", saveData.GrowthDuration },
			{ "Plots", plotsData }
		};

		var json = Json.Stringify(data);
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		file.StoreString(json);
	}

	private void RefreshPlotStateFromTime(Vector2I cell)
	{
		if (_gameState == null)
		{
			return;
		}

		if (!_gameState.Plots.TryGetValue(cell, out var plot))
		{
			return;
		}

		if (plot.Status == PlotStatus.Empty || plot.Status == PlotStatus.Ready)
		{
			return;
		}

		double currentTime = Time.GetUnixTimeFromSystem();
		double elapsed = currentTime - plot.PlantedTime;
		double growthDuration = _gameState.GetGrowthDuration(plot.CropType);
		double halfGrowthDuration = growthDuration / 2;

		if (elapsed >= growthDuration)
		{
			plot.Status = PlotStatus.Ready;
		}
		else if (elapsed >= halfGrowthDuration)
		{
			plot.Status = PlotStatus.Growing;
		}
		else
		{
			plot.Status = PlotStatus.Planted;
		}
	}

	private void ShowMessage(string message)
	{
		if (_messageLabel != null)
		{
			_messageLabel.Text = message;
		}

		_messageTimer = 2.0f;
	}

	private void OnUpgradeHarvestPressed()
	{
		if (_gameState == null)
		{
			return;
		}

		if (_gameState.UpgradeHarvestValue())
		{
			SaveGame();
			ShowMessage($"Harvest upgraded to {_gameState.HarvestValue}");
			RefreshUI();
		}
		else
		{
			ShowMessage("Not enough currency");
		}
	}

	private void OnUpgradeSpeedPressed()
	{
		if (_gameState == null)
		{
			return;
		}

		if (_gameState.UpgradeGrowthSpeed())
		{
			SaveGame();
			ShowMessage($"Growth time: {_gameState.GrowthDuration:0.0}s");
			RefreshUI();
		}
		else
		{
			ShowMessage("Not enough currency");
		}
	}

	private void OnCropTogglePressed()
	{
		if (_selectedCrop == CropType.CropA)
		{
			_selectedCrop = CropType.CropB;
		}
		else
		{
			_selectedCrop = CropType.CropA;
		}

		if (_cropToggleButton != null)
		{
			_cropToggleButton.Text = _selectedCrop == CropType.CropA
				? "Crop: A (Fast, Low Value)"
				: "Crop: B (Slow, High Value)";
		}
	}

	private void OnHarvestAllPressed()
	{
		if (_gameState == null)
		{
			return;
		}

		var mapController = GetNode<MapController>("../Map");
		int count = 0;
		int total = 0;

		foreach (var plotEntry in _gameState.Plots)
		{
			var plot = plotEntry.Value;
			if (plot.Status == PlotStatus.Ready)
			{
				var value = _gameState.GetHarvestValue(plot.CropType);
				if (_gameState.HarvestPlot(plotEntry.Key, 25))
				{
					mapController.DrawPlot(plotEntry.Key, plot);
					count++;
					total += value;
				}
			}
		}

		if (count > 0)
		{
			RefreshUI();
			SaveGame();
			ShowMessage($"Harvested {count} plots +{total}");
		}
		else
		{
			ShowMessage("Nothing ready");
		}
	}

	private void OnPlantAllPressed()
	{
		if (_gameState == null)
		{
			return;
		}

		var mapController = GetNode<MapController>("../Map");
		int count = 0;

		foreach (var plotEntry in _gameState.Plots)
		{
			if (plotEntry.Value.Status == PlotStatus.Empty)
			{
				var planted = _gameState.PlantPlot(plotEntry.Key, Time.GetUnixTimeFromSystem(), _selectedCrop);
				if (planted)
				{
					mapController.DrawPlot(plotEntry.Key, plotEntry.Value);
					count++;
				}
				else
				{
					break;
				}
			}
		}

		if (count > 0)
		{
			RefreshUI();
			SaveGame();
			ShowMessage($"Planted {count} plots");
		}
		else
		{
			ShowMessage("Nothing planted");
		}
	}

	private void RefreshUI()
	{
		UpdateCurrencyUI();
		UpdateUpgradeButtonUI();
		UpdateSpeedButtonUI();
	}

	private void UpdateUpgradeButtonUI()
	{
		if (_gameState == null || _upgradeHarvestButton == null)
		{
			return;
		}

		_upgradeHarvestButton.Text = $"Upgrade Harvest (+5) Cost: 50 | Current: {_gameState.HarvestValue}";
	}

	private void UpdateSpeedButtonUI()
	{
		if (_gameState == null || _upgradeSpeedButton == null)
		{
			return;
		}

		_upgradeSpeedButton.Text = $"Faster Growth (-0.2s) Cost: 50 | Current: {_gameState.GrowthDuration:0.0}s";
	}

	private void CheckGoal()
	{
		if (_goalReached)
		{
			return;
		}

		if (_gameState != null && _gameState.Currency >= GoalCurrency)
		{
			_goalReached = true;
			ShowMessage("Farm Established");
		}
	}
}
