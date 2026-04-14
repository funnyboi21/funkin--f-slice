public partial class StateTree : Node
{
	private Node _currentState;

	public void TransitionTo(Node newState)
	{
		_currentState?.Call("OnExit");
		_currentState = newState;
		_currentState?.Call("OnEnter");
	}

	public override void _Process(double delta)
	{
		_currentState?.Call("OnUpdate", delta);
	}
}
