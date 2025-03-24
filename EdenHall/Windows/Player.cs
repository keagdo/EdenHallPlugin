using System.Collections.Generic;

namespace EdenHall.Windows;

public class Player
{
    public string Name;
    public string Nick;
    public List<int> Cards = new();
    public bool Playing = true;
    public bool Hit = false;
    public bool Stand = false;
    public bool DoubleDown = false;
    public bool Split = false;
    public int BetAmount = 0;
    public bool IsEditingNick = false;
    public int Index;
    public bool Dealer;
    public Queue<string> ChatLog { get; } = new Queue<string>();
    public string Result;
    public int Balance = 0;
    public bool CashOut = false;
    public bool Busted = false;
    public bool HasSplit = false;
    public List<int> SplitHand = new();

    public Player(string playerName, int i, bool dealer = false)
    {
        Name = playerName;
        Nick = playerName;
        Index = i;
        Dealer = dealer;
    }
    public void AddChatMessage(string message)
    {
        if (ChatLog.Count >= 50)
        {
            ChatLog.Dequeue(); // Remove the oldest message
        }
        ChatLog.Enqueue(message); // Add the new message
    }
}