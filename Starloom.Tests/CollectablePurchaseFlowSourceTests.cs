using Xunit;

namespace Starloom.Tests;

public sealed class CollectablePurchaseFlowSourceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void WorkflowOrchestrator_Treats_House_And_Inn_As_Valid_Internal_Start_Locations()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot, "Automation", "WorkflowOrchestrator.cs"));

        Assert.Contains("HousingReturnPointService.IsInsideHouse() || HousingReturnPointService.IsInsideInn()", source);
    }

    [Fact]
    public void ConfiguredWorkflow_Must_Finalize_When_All_Purchase_Targets_Are_Satisfied()
    {
        var orchestratorSource = ReadSource(Path.Combine("Automation", "WorkflowOrchestrator.cs"));
        var dispatcherSource = ReadSource(Path.Combine("Automation", "WorkflowTaskDispatcher.cs"));
        var dispatchLoopBody = ReadMethodBody(dispatcherSource, "internal void DispatchLoopTurnInAndPurchase()")!;

        Assert.Contains("HasPendingPurchaseWorkRemaining()", orchestratorSource);
        Assert.Contains("WorkflowState.FinalizingCompletion", orchestratorSource);
        Assert.Contains("P.PurchaseResolver.HasPending()", dispatchLoopBody);
        Assert.DoesNotContain("C.BuyAfterEachTurnIn", dispatchLoopBody);
        Assert.DoesNotContain("EnqueuePostAction();\n        EnqueueStartArtisanList();", dispatcherSource.Replace("\r\n", "\n"));
    }

    [Fact]
    public void PurchaseOnly_And_ConfiguredWorkflow_Finalization_Must_Reuse_The_Same_Completion_Path()
    {
        var dispatcherSource = ReadSource(Path.Combine("Automation", "WorkflowTaskDispatcher.cs"));
        var purchaseOnlyBody = ReadMethodBody(dispatcherSource, "internal void DispatchPurchaseOnly()")!;
        var finalCompletionBody = ReadMethodBody(dispatcherSource, "internal void DispatchFinalCompletion()")!;

        Assert.Contains("DispatchFinalCompletion();", purchaseOnlyBody);
        Assert.Contains("EnqueuePostAction();", finalCompletionBody);
        Assert.DoesNotContain("EnqueueStartArtisanList();", finalCompletionBody);
    }

    [Fact]
    public void Localization_Must_Not_Keep_Legacy_Auto_Buy_Keys()
    {
        var zhSource = ReadSource(Path.Combine("Resources", "Localization", "zh.json"));
        var enSource = ReadSource(Path.Combine("Resources", "Localization", "en.json"));

        Assert.DoesNotContain("settings.purchase.auto_buy", zhSource);
        Assert.DoesNotContain("settings.purchase.auto_buy_toggle", zhSource);
        Assert.DoesNotContain("settings.purchase.auto_buy", enSource);
        Assert.DoesNotContain("settings.purchase.auto_buy_toggle", enSource);
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot, relativePath));
    }

    private static string? ReadMethodBody(string source, string methodSignature)
    {
        var signatureIndex = source.IndexOf(methodSignature, StringComparison.Ordinal);
        if (signatureIndex < 0)
            return null;

        var bodyStart = source.IndexOf('{', signatureIndex);
        if (bodyStart < 0)
            return null;

        var depth = 0;
        for (var i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(bodyStart, i - bodyStart + 1);
            }
        }

        return null;
    }
}
