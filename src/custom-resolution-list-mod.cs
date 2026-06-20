using MelonLoader;

namespace CustomResolutionList;

public sealed class CustomResolutionListMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        CustomResolutionLog.Configure(
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message));

        new HarmonyLib.Harmony("custom-resolution-list").PatchAll();

        CustomResolutionController.OnModInitialized();
        CustomResolutionLog.Message("Custom Resolution List initialized.");
    }

    public override void OnUpdate()
    {
        CustomResolutionController.OnUpdate();
    }
}