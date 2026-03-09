using FFXIVClientStructs.FFXIV.Component.GUI;
using StarLoom.Services.Interfaces;
using System;
using static ECommons.GenericHelpers;

namespace StarLoom.Jobs;

public abstract unsafe class ShopJobBase : AutomationJobBase
{
    protected bool NavigationStarted { get; set; }

    protected void UpdateNavigationToShop<TState>(ShopInteractionContext interactionContext, TState nextState, Action<TState> transition)
        where TState : struct
    {
        if (Context == null)
            return;

        if (!NavigationStarted)
        {
            Context.Navigation.NavigateTo(interactionContext.Target);
            NavigationStarted = true;
            return;
        }

        if (Context.Navigation.State == NavigationStatus.Arrived)
        {
            NavigationStarted = false;
            transition(nextState);
            LastActionAt = DateTime.MinValue;
            return;
        }

        if (Context.Navigation.State == NavigationStatus.Failed)
            FailJob(Context.Navigation.ErrorMessage ?? interactionContext.NavigationFailureMessage);
    }

    protected void UpdateShopWindow<TState>(
        bool isReady,
        TimeSpan timeout,
        ShopInteractionContext interactionContext,
        TState nextState,
        Action<TState> transition,
        Func<bool>? tryOpenWindow = null)
        where TState : struct
    {
        if (Context == null)
            return;

        if (isReady)
        {
            transition(nextState);
            LastActionAt = DateTime.MinValue;
            return;
        }

        if ((DateTime.UtcNow - TransitionedAt) > timeout)
        {
            FailJob(interactionContext.WindowTimeoutMessage);
            return;
        }

        if (tryOpenWindow != null
            && TryGetAddonByName<AtkUnitBase>("SelectIconString", out var selectAddon)
            && IsAddonReady(selectAddon)
            && tryOpenWindow())
        {
            LastActionAt = DateTime.UtcNow;
            return;
        }

        if ((DateTime.UtcNow - LastActionAt) < TimeSpan.FromSeconds(1))
            return;

        if (Context.NpcInteraction.TryInteract(interactionContext.NpcId))
            LastActionAt = DateTime.UtcNow;
    }
}
