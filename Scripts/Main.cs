using Godot;

public partial class Main : Node2D
{
	private Node _gameController;

	public override void _Ready()
	{
		GD.Print("Main scene loaded.");

		_gameController = GetNode("GameController");
	}
}
