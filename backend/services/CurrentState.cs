namespace DashboardBackend.Services;

public class CurrentStateService
{
    public string CurrentState { get; private set; } = "idle";

    public void SetState(string state)
    {
        CurrentState = state;
    }
}