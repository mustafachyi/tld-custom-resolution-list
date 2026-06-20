using System;

namespace CustomResolutionList;

internal static class CustomResolutionLog
{
    private static Action<string> MessageWriter = _ => { };
    private static Action<string> WarningWriter = _ => { };

    public static void Configure(Action<string> messageWriter, Action<string> warningWriter)
    {
        MessageWriter = messageWriter ?? MessageWriter;
        WarningWriter = warningWriter ?? WarningWriter;
    }

    public static void Message(string message)
    {
        MessageWriter(message);
    }

    public static void Warning(string message)
    {
        WarningWriter(message);
    }
}