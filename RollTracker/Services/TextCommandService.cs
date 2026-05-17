using System;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace RollTracker.Services;

internal sealed unsafe class TextCommandService
{
    public void Execute(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var uiModule = UIModule.Instance();
        if (uiModule is null)
        {
            throw new InvalidOperationException("UIModule is not available.");
        }

        var shellModule = uiModule->GetRaptureShellModule();
        if (shellModule is null)
        {
            throw new InvalidOperationException("RaptureShellModule is not available.");
        }

        var utf8Command = Utf8String.FromString(command);
        try
        {
            shellModule->ExecuteCommandInner(utf8Command, uiModule);
        }
        finally
        {
            utf8Command->Dtor(true);
        }
    }
}
