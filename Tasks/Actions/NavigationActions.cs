using Starloom.Services;

namespace Starloom.Tasks.Actions;

internal static class NavigationActions
{
    internal static void Enqueue(NavigationTarget target, string stepName, string failureMessage)
    {
        P.TM.Enqueue(() =>
        {
            P.Navigation.NavigateTo(target);
            return true;
        }, $"{stepName}.Start");

        P.TM.Enqueue(() =>
        {
            P.Navigation.Poll();

            if (P.Navigation.HasFailed)
            {
                DuoLog.Error(P.Navigation.ErrorMessage ?? failureMessage);
                return null;
            }

            if (P.Navigation.IsComplete)
            {
                P.Navigation.Stop();
                return true;
            }

            return false;
        }, int.MaxValue, true, $"{stepName}.Wait");
    }
}
