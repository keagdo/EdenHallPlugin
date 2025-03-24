using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Threading.Channels;

namespace EdenHall.Helpers;

internal class ChatManager : IDisposable
{
    private readonly Channel<string> chatBoxMessages = Channel.CreateUnbounded<string>();

    public ChatManager()
    {
        Svc.Framework.Update += FrameworkUpdate;
    }

    private unsafe delegate void ProcessChatBoxDelegate(UIModule* uiModule, IntPtr message, IntPtr unused, byte a4);

    public void Dispose()
    {
        Svc.Framework.Update -= FrameworkUpdate;
        chatBoxMessages.Writer.Complete();
    }

    public void PrintMessage(string message, XivChatType ChatType)
        => Svc.Chat.Print(new XivChatEntry()
        {
            Type = ChatType,
            Message = $"{message}",
        });

    public async void SendMessage(string message) => await chatBoxMessages.Writer.WriteAsync(message);

    /// <summary>
    /// Clear the queue of messages to send to the chatbox.
    /// </summary>
    public void Clear()
    {
        var reader = chatBoxMessages.Reader;
        while (reader.Count > 0 && reader.TryRead(out var _))
            continue;
    }

    private void FrameworkUpdate(IFramework framework)
    {
        if (chatBoxMessages.Reader.TryRead(out var message))
            Chat.Instance.SendMessage(message);
    }
}
