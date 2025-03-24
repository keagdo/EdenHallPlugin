using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using EdenHall.Windows;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ECommons;
using ECommons.Configuration;
using ECommons.SimpleGui;
using ECommons.ImGuiMethods;

namespace EdenHall;
#nullable disable
public class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IObjectTable Objects { get; private set; }
    [PluginService] public static IChatGui Chat { get; private set; }
    [PluginService] public static IFramework Framework { get; private set; }

    private const string CommandName = "/ebj";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("EdenHall");
    private MainWindow MainWindow { get; init; }
    public Configuration C { get; private set; }
    internal TaskManager TaskManager;
    internal Plugin P;
    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);
        P = this;
        C = EzConfig.Init<Configuration>();
        EzConfigGui.Init(Draw);
        // Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        TaskManager = new(){};
        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        // ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        // WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        Log.Information($"=== Started {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        // ConfigWindow.Dispose();
        MainWindow.Dispose();
        ECommonsMain.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleMainUI() => MainWindow.Toggle();

    void Draw()
    {
        ImGuiEx.SliderIntAsFloat("Delay before accepting, s", ref C.Accept_Trade_Delay, 0, 10000);
        ImGuiNET.ImGui.SliderInt("Gil Min, >=", ref C.MinGil, 50000, 1000000);
    }
}
