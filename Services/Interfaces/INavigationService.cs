using Starloom.Services;

namespace Starloom.Services.Interfaces;

public interface INavigationService
{
    NavigationService.NavigationState State { get; }
    string? ErrorMessage { get; }

    void NavigateTo(NavigationTarget target);
    void Stop();
    void Update();
}
