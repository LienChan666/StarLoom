using Starloom.Data;

namespace Starloom.Automation;

internal static class WorkflowStartValidator
{
    internal static bool CanStartCollectable(out string errorMessage)
    {
        if (C.PreferredCollectableShop != null)
        {
            errorMessage = string.Empty;
            return true;
        }
        errorMessage = "A collectable shop must be configured before starting.";
        return false;
    }

    internal static bool CanStartArtisanList(bool artisanListManaged, out string errorMessage)
    {
        if (!P.Artisan.IsAvailable())
        {
            errorMessage = "Artisan is not available.";
            return false;
        }

        if (C.ArtisanListId <= 0)
        {
            errorMessage = "Artisan list id is required.";
            return false;
        }

        if (artisanListManaged && P.Artisan.IsListRunning())
        {
            errorMessage = string.Empty;
            return true;
        }

        if (P.Artisan.IsListRunning() || P.Artisan.GetEnduranceStatus() || P.Artisan.IsBusy())
        {
            errorMessage = "Artisan is busy with another task.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
