using Dalamud.Game.Command;
using Dalamud.Plugin;
using ECommons;

namespace StarLoom;

public sealed class StarLoom : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;

    public StarLoom(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);

        Svc.Commands.AddHandler("/starloom", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the StarLoom window",
        });

        Svc.Framework.Update += OnUpdate;
    }

    private void OnCommand(string command, string arguments)
    {
    }

    private void OnUpdate(IFramework framework)
    {
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.Commands.RemoveHandler("/starloom");
        ECommonsMain.Dispose();
    }
}
