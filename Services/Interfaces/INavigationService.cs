namespace StarLoom.Services.Interfaces;

public interface INavigationService
{
    NavigationStatus State { get; }
    string? ErrorMessage { get; }

    void NavigateTo(NavigationTarget target);
    void Stop();
    void Update();
}
