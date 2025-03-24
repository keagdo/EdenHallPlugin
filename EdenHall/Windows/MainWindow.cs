using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using EdenHall.Helpers;
using ECommons.GameHelpers;
using Dalamud.Utility;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation.NeoTaskManager;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Memory;
using static ECommons.GenericHelpers;
using ECommons.Throttlers;
using ECommons.Configuration;
using ECommons.Automation.UIInput;
using System.Threading.Tasks;
using System.Threading;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System.Runtime.CompilerServices;
using ECommons.Automation;

namespace EdenHall.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin P;
    List<Player> partyList = new List<Player>{};
    ChatManager chatManager = new ChatManager();
    Player dealer;
    public Configuration C { get; init; }
    string TradeText => Svc.Data.GetExcelSheet<Addon>().GetRow(102223).Text.ExtractText();
    public bool hasAnnouncedNewGame = false;
    public bool hasAcceptedBets = false;
    public bool hasDealtCards = false;
    public bool hasRevealedDealerFirstCard = false;
    public bool isRevealingDealerFirstCard = false;
    public bool hasRevealedDealerSecondCard = false;
    public bool hasResolvedGame = false;
    public bool isDealingCards = false;
    public bool isAcceptingBets = false;
    public bool isRevealingDealerSecondCard = false;
    public bool isDrawingDealerHits = false;
    public bool isDeterminingWinners = false;
    public bool hasDetermineWinners = false;
    public bool hasAwardedPayouts = false;
    public bool isAwardingPayouts = false;
    public int gilAmount = 0;
    public bool isProcessingMove = false;
    public List<Player> playerList = new List<Player>{};
    public bool needToAcceptTrade = false;
    public bool needToSendTrade = false;
    bool testBool = false;
    bool allowedTrade = false;
    bool noTradeSpam = true;
    enum GamePhase { NewGame, Betting, Dealing, PlayerTurn, DealerTurn, Resolution, Error };
    GamePhase lastPhase = GamePhase.NewGame;
    GamePhase currentPhase = GamePhase.NewGame;
    public MainWindow(Plugin plugin) : base("Main Window##id1")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            // MinimumSize = new Vector2(375, 330),
            // MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        P = plugin;
        C = EzConfig.Init<Configuration>();
        Plugin.Chat.ChatMessage += OnChatMessage;
        Svc.Framework.Update += FrameworkUpdate;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        string senderName = sender.TextValue.Substring(1); // Remove the first character
        int atIndex = senderName.IndexOf('@');  
        if (atIndex != -1)  
        {  
            senderName = senderName.Substring(0, atIndex); // Remove everything after '@'
        }
        string messageText = message.TextValue;
        if (type == XivChatType.Alliance || type == XivChatType.Party || type == XivChatType.TellIncoming)
        {
            Plugin.Log.Information($"[{type}] {senderName}: {messageText}");
            var player = partyList.FirstOrDefault(p => p.Name == senderName);
            if (player != null)
            {
                // Plugin.Log.Info($"Adding {messageText} to {player}");
                player.AddChatMessage($"{messageText}");
            }
        }

        // If you want to suppress the message from showing in the in-game chat:
        // isHandled = true;
    }
    public void Dispose() {         
        Plugin.Chat.ChatMessage -= OnChatMessage;
        Svc.Framework.Update -= FrameworkUpdate;
    }
    private unsafe void FrameworkUpdate(object framework)
    {
        if (needToAcceptTrade)
        {
            AcceptTradesForMinGil();
        }
        if (needToSendTrade)
        {
            SendTradesToGiveGil();
        }
    }
    private unsafe bool GetAddon(string name)
    {
        return TryGetAddonByName<AtkUnitBase>(name, out var addon) && IsAddonReady(addon);
    }
    private unsafe bool GetAddon(string name, out AtkUnitBase* addonVar)
    {
        bool value = TryGetAddonByName<AtkUnitBase>(name, out var addon) && IsAddonReady(addon);
        addonVar = addon; // Assign the value to the output parameter
        return value;
    }
    private unsafe void SendTradesToGiveGil()
    {
        bool setGil = false;
        if(Svc.Condition[ConditionFlag.TradeOpen])
        {
            if (GetAddon("Trade", out var tradeAddon))
            {
                if (GetAddon("SelectYesno", out var yesnoAddon))
                {
                    Plugin.Log.Info("Looking to confirm trade!");
                    ClickButton(yesnoAddon, 11);
                    gilAmount = 0;
                }
                else
                {
                    var gilSentString = MemoryHelper.ReadSeString(&tradeAddon->UldManager.NodeList[24]->GetAsAtkComponentButton()->ButtonTextNode->GetAsAtkTextNode()->NodeText).ToString();
                    uint.TryParse(gilSentString, out var gilSentInt); //Incoming Gil as an (unsigned) int
                    Plugin.Log.Info($"Gil set to {gilSentInt}");
                    setGil = gilSentInt == gilAmount;

                    if(!setGil)
                    {
                        TrySetGil(tradeAddon, gilAmount);
                    }
                    
                    if (setGil) 
                    {
                        var myCheck = tradeAddon->UldManager.NodeList[32]->GetAsAtkComponentNode()->Component->UldManager.NodeList[0]->GetAsAtkImageNode(); //Find Trade Checkmark
                        allowedTrade = myCheck->AtkResNode.Color.A == 0x00; //Allow myself to trade only if i am not trading already
                        if (allowedTrade) 
                        {
                            Plugin.Log.Info("Looking to accept trade!");
                            allowedTrade = false;
                            ClickButton(tradeAddon, 3);
                        }
                    }
                }
            }
        }
    }
    private unsafe void TrySetGil(AtkUnitBase* tradeAddon, int gilAmount)
    {
        if(GetAddon("InputNumeric", out var inputNumericAddon))
        {
            Callback.Fire(inputNumericAddon,true,gilAmount);
        }
        else
        {
            ClickButton(tradeAddon, 24);
        }
    }
    private unsafe void AcceptTradesForMinGil()
    {
        if(Svc.Condition[ConditionFlag.TradeOpen])
        {
            if (GetAddon("Trade", out var tradeAddon))
            {
                if (GetAddon("SelectYesno", out var yesnoAddon))
                {
                    Plugin.Log.Info("Looking to confirm trade!");
                    ClickButton(yesnoAddon, 11);
                }
                else
                {
                    var otherCheck = tradeAddon->UldManager.NodeList[31]->GetAsAtkComponentNode()->Component->UldManager.NodeList[0]->GetAsAtkImageNode(); //Find Trade Checkmark
                    var otherReady = otherCheck->AtkResNode.Color.A == 0xFF; //If the alpha is 255 (0xFF), other player is ready.
                    var otherName = MemoryHelper.ReadSeString(&tradeAddon->UldManager.NodeList[20]->GetAsAtkTextNode()->NodeText).ToString();


                    var gilOffered = MemoryHelper.ReadSeString(&tradeAddon->UldManager.NodeList[6]->GetAsAtkTextNode()->NodeText).ToString(); //Incoming Gil as a string
                    uint.TryParse(gilOffered, out var gil); //Incoming Gil as an (unsigned) int
                    if (gil >= C.MinGil)
                    {
                        otherReady = true; //Make myself ready to trade???
                    }
                    if (otherReady) 
                    {
                        var myCheck = tradeAddon->UldManager.NodeList[32]->GetAsAtkComponentNode()->Component->UldManager.NodeList[0]->GetAsAtkImageNode(); //Find Trade Checkmark
                        allowedTrade = myCheck->AtkResNode.Color.A == 0x00; //Allow myself to trade only if i am not trading already
                        if (allowedTrade) 
                        {
                            Plugin.Log.Info("Looking to accept trade!");
                            allowedTrade = false;
                            ClickButton(tradeAddon, 3);
                            var player = playerList.FirstOrDefault(p => p.Name == otherName);
                            if (player != null)
                            {
                                player.BetAmount = (int)gil;
                                Plugin.Log.Information($"Assigning Player {gil} Gil");
                            }
                            else
                            {
                                lastPhase = currentPhase;
                                currentPhase = GamePhase.Error;
                                Chat("/echo Error! Pausing script because incoming gil not logged! <se.6>");
                            }
                        }
                    }
                }
            }
        }
    }
    private bool isProcessingButton = false; // Prevent re-entry
    private unsafe void ClickButton(AtkUnitBase* addon, int nodeIndex)
    {
        if (isProcessingButton)
            return; // Exit if trade is already processing or not allowed

        isProcessingButton = true; // Mark as processing to prevent duplicate calls

        AtkComponentButton* button = addon->UldManager.NodeList[nodeIndex]->GetAsAtkComponentButton();
        if (button == null)
        {
            Plugin.Log.Warning("Button not found.");
            isProcessingButton = false; // Reset if failed
            return;
        }

        var target = addon->CurrentDropDownOwnerNode;
        ClickHelperExtensions.ClickAddonButton(*button, addon);
        Plugin.Log.Information("Clicking Button");

        // Block further executions for exactly 1 second
        Task.Run(() =>
        {
            Thread.Sleep(1000); // Blocking delay to prevent rapid retriggers
            isProcessingButton = false;
        });
    }
    public void UpdatePartyList()
    {
        var currentPartyMembers = Svc.Party.Select(m => m.Name.ToString()).ToList();
        var localPlayerId = Plugin.ClientState.LocalPlayer?.GameObjectId;
        
        // Synchronize: Add new players
        for (int i = 0; i < Svc.Party.Count(); i++)
        {
            var member = Svc.Party[i];
            var playerName = member.Name.ToString();
            // Check if the player is already in the list
            var existingPlayer = partyList.FirstOrDefault(p => p.Name == playerName);
            if (existingPlayer == null)
            {
                // Add new player
                bool isDealer = member.ObjectId == localPlayerId;
                partyList.Add(new Player(playerName, i, isDealer));
            }
            else
            {
                // Update Dealer flag if the player is the local player
                existingPlayer.Dealer = member.ObjectId == localPlayerId;
            }
        }
        // Synchronize: Remove players no longer in the party
        partyList.RemoveAll(p => !currentPartyMembers.Contains(p.Name));
        partyList = partyList.OrderByDescending(p => p.Dealer).ToList();
    }
    private void SetButtonColor(bool isActive, out bool colorPushed)
    {
        colorPushed = false; // Default: no color change

        if (isActive)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.8f, 0.2f, 1.0f)); // Green for active
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.9f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.1f, 0.7f, 0.1f, 1.0f));

            colorPushed = true; // Colors were pushed
        }
    }
    private void ResetButtonColor(bool colorPushed)
    {
        if (colorPushed)
        {
            ImGui.PopStyleColor(3);
        }
    }
    public override void Draw()
    {
        ProcessChatQueue();
        if (ImGui.Button($"{testBool}"))
        {
            testBool = !testBool;
            chatManager.SendMessage($"/echo Toggle Test bool. Is now: {testBool}");
        }
        if (ImGui.Button($"Error Resolution"))
        {
            chatManager.SendMessage($"/echo Reseting phase!");
            currentPhase = lastPhase;
        }

        if (ImGui.Button("Start Trade"))
        {
            DealInitialCards();
        }
        
        UpdatePartyList();
        playerList = partyList.Where(x=> !x.Dealer).ToList();
        if (playerList.Count==0) return;

        // Game loop
        if (testBool)
        {
            switch (currentPhase) 
            {
                case GamePhase.NewGame:
                    if (!hasAnnouncedNewGame) {
                        AnnounceNewGame();
                        hasAnnouncedNewGame = true;
                    } else {
                        currentPhase = GamePhase.Betting;
                        hasAnnouncedNewGame = false; // Reset for next round
                    }
                    break;

                case GamePhase.Betting:
                    if (!hasAcceptedBets) {
                        AcceptBets();
                    } else {
                        currentPhase = GamePhase.Dealing;
                        hasAcceptedBets = false;
                    }
                    break;

                case GamePhase.Dealing:
                    if (!hasDealtCards) {
                        DealInitialCards();
                    }
                    else if (!hasRevealedDealerFirstCard) {
                        RevealDealerFirstCard();
                    } else 
                    {
                        currentPhase = GamePhase.PlayerTurn;
                        hasDealtCards = false;
                        hasRevealedDealerFirstCard = false;
                    }
                    break;

                case GamePhase.PlayerTurn:
                    if (AllPlayersFinished()) {
                        currentPhase = GamePhase.DealerTurn;
                    } else {
                        ProcessPlayerMove();
                    }
                    break;

                case GamePhase.DealerTurn:
                    if (!hasRevealedDealerSecondCard) {
                        RevealDealerSecondCard();
                        hasRevealedDealerSecondCard = true;
                    }
                    else if (ShouldDealerHit()) {
                        DealerHits(); // Will keep hitting until condition is false
                    } else {
                        currentPhase = GamePhase.Resolution;
                        hasRevealedDealerSecondCard = false;
                    }
                    break;

                case GamePhase.Resolution:
                    if (!hasDetermineWinners) {
                        DetermineWinners();
                    }
                    else if (!hasAwardedPayouts) {
                        AwardPayouts();   
                    } else {
                        currentPhase = GamePhase.NewGame;
                    }
                    break;
            }
        }
        DrawTable();
    }

    private Queue<string> chatQueue = new Queue<string>();
    private bool isChatting = false;
    private float chatCooldown = 1.00f; // Adjust delay as needed (in seconds)
    private DateTime lastChatTime = DateTime.MinValue;
    public void Chat(string message)
    {
        chatQueue.Enqueue(message);
    }

    public async Task AwaitChatQueue()
    {
        while (chatQueue.Count > 0)
        {
            await Task.Delay(500);
        }
    }
    public async Task DrawCards(Player player, int cards)
    {
        for (int i = 0; i < cards; i++)
        {
            Chat("/dice party 13");
        }

        // Wait for chat logs to update
        await AwaitChatQueue();
        Plugin.Log.Information("Done Waiting on ChatLog");
        // Get the last two dice rolls from the dealer's chat log
        List<int> rolls = GetLastDiceRolls(dealer.ChatLog, cards);

        // Convert dice values to Blackjack values and add to player's hand
        foreach (int roll in rolls)
        {
            player.Cards.Add(ConvertToBlackjackValue(roll));
        }
    }

    public async Task DrawCardToSplitHand(Player player, int cards)
    {
        for (int i = 0; i < cards; i++)
        {
            Chat("/dice party 13");
        }

        // Wait for chat logs to update
        await AwaitChatQueue();
        Plugin.Log.Information("Done Waiting on ChatLog");
        // Get the last two dice rolls from the dealer's chat log
        List<int> rolls = GetLastDiceRolls(dealer.ChatLog, cards);

        // Convert dice values to Blackjack values and add to player's hand
        foreach (int roll in rolls)
        {
            player.SplitHand.Add(ConvertToBlackjackValue(roll));
        }
    }
    private List<int> GetLastDiceRolls(Queue<string> chatLog, int numRolls)
    {
        List<int> rolls = new List<int>();
        List<string> chatList = chatLog.ToList();
        for (int i = chatList.Count - 1; i >= 0 && rolls.Count < numRolls; i--)
        {
            string message = chatList[i];
            Plugin.Log.Info($"Checking message! {message}");
            if (message.Contains("Random! (1-13)"))
            {
                Plugin.Log.Info($"Message passed!");
                string[] parts = message.Split(' ');
                if (int.TryParse(parts[^1], out int roll))
                {
                    rolls.Add(roll);
                }
                Plugin.Log.Info($"Roll was {roll}");
            }
        }
        rolls.Reverse(); // Ensure the order is correct
        return rolls;
    }
    private int ConvertToBlackjackValue(int roll)
    {
        if (roll == 1) return 11; // Ace
        if (roll >= 11) return 10; // Face cards
        return roll; // Other values remain the same
    }
    private void ProcessChatQueue()
    {
        if (chatQueue.Count > 0 && (DateTime.Now - lastChatTime).TotalSeconds >= chatCooldown)
        {
            string message = chatQueue.Dequeue();
            chatManager.SendMessage(message);
            lastChatTime = DateTime.Now; // Update the last chat time
        }
    }
    private void AnnounceNewGame()
    {
        Chat("/p Starting new game!");
    }
    private async Task AcceptBets()
    {
        if (!isAcceptingBets)
        {
            Chat("/p Sending out trades for bets! Please wait your turn.");
            isAcceptingBets = true;
            foreach (Player player in playerList)
            {
                Plugin.Log.Information($"Running Trade for {player.Name}");
                if (player.BetAmount == 0 && !GetAddon("Trade")) 
                {
                    Chat($"/trade <{player.Index + 1}>"); // Send trade command to player
                    needToAcceptTrade = true; // Start the trade process
                }
                while (GetAddon("Trade") && player.Playing)
                {
                    await Task.Delay(500);
                    Plugin.Log.Information($"waiting on {player.Name} to input and accept Gil");
                }
                if (!GetAddon("Trade") && player.BetAmount == 0)
                {
                    player.Playing = false;
                }
            }
            isAcceptingBets = false;
        }

        // Check if all players have placed their bets
        if (playerList.All(p => p.BetAmount > 0 || !p.Playing)) 
        {
            needToAcceptTrade = false; // Betting is done, no more trades needed
            hasAcceptedBets = true;    // Move to the next phase
        }
    }
    private async void DealInitialCards()
    {
        if (!isDealingCards)
        {
            isDealingCards = true;
            foreach (Player player in playerList)
            {
                Chat($"/p Now Drawing for {player.Nick}");
                await DrawCards(player, 2);
            }
            isDealingCards = false;
            hasDealtCards = true;
        }
    }
    private async void RevealDealerFirstCard()
    {
        if (!isRevealingDealerFirstCard)
        {
            isRevealingDealerFirstCard = true;
            Chat($"/p Revealing the dealer's first hand!");
            await DrawCards(dealer, 1);
            hasRevealedDealerFirstCard = true;
            isRevealingDealerFirstCard = false;
        }
    }

    private bool AllPlayersFinished()
    {
        return playerList.All(player => player.Stand || !player.Playing);
    }

    private async void ProcessPlayerMove()
    {
        if (isProcessingMove) return;
        isProcessingMove = true;

        foreach (var player in playerList)
        {
            while (!player.Stand && GetHandValue(player.Cards) < 21)
            {
                int handTotal = GetHandValue(player.Cards);
                Chat($"{player.Nick}, you have {handTotal}. Your choices: Stand, Hit{(player.Cards.Count == 2 ? ", Double Down" : "")}{(CanSplit(player) ? ", Split" : "")}.");
                player.ChatLog.Clear();
                string decision = await WaitForPlayerDecision(player);
                if (decision == null) continue; // No decision made, keep waiting.

                switch (decision.ToLower())
                {
                    case "stand":
                        player.Stand = true;
                        Chat($"{player.Nick} stands with {handTotal}.");
                        break;

                    case "hit":
                        Chat($"{player.Nick} hits.");
                        await DrawCards(player, 1);
                        if (GetHandValue(player.Cards) > 21)
                        {
                            Chat($"{player.Nick} busts with {GetHandValue(player.Cards)}.");
                            player.Busted = true;
                            player.Stand = true;
                        }
                        break;

                    case "double":
                        if (player.Cards.Count == 2)
                        {
                            if (player.Balance >= player.BetAmount)
                            {
                                player.Balance -= player.BetAmount;
                                player.BetAmount *= 2;
                                Chat($"{player.Nick} doubles down!");
                                await DrawCards(player, 1);
                                player.Stand = true; // Auto-stand after doubling down.
                            }
                            else
                            {
                                Chat($"{player.Nick}, you don't have enough balance to double down. Please trade me.");
                                // TODO: Accept trade to cover the double down cost.
                            }
                        }
                        break;

                    case "split":
                        if (CanSplit(player))
                        {
                            Chat($"{player.Nick} splits their hand.");
                            await SplitHand(player);
                        }
                        break;

                    default:
                        Chat($"{player.Nick}, I didn't understand that. Try again.");
                        break;
                }
                await AwaitChatQueue();
            }
        }

        isProcessingMove = false;
    }

    private bool CanSplit(Player player)
    {
        return player.Cards.Count == 2 && player.Cards[0] == player.Cards[1];
    }

    private async Task SplitHand(Player player)
    {
        if (!CanSplit(player)) return;

        player.HasSplit = true; // Add a flag to track split status
        player.SplitHand = new List<int> { player.Cards[1] };
        player.Cards.RemoveAt(1);

        await DrawCards(player, 1);
        await DrawCardToSplitHand(player, 1);
    }
    private async Task<string> WaitForPlayerDecision(Player player)
    {
        while (true)
        {
            await Task.Delay(500); // Prevent spamming the loop
            
            while (player.ChatLog.Count > 0)
            {
                string chatMessage = player.ChatLog.Dequeue().ToLower();
                
            if (chatMessage.Equals("stand", StringComparison.OrdinalIgnoreCase) || chatMessage.Equals("s", StringComparison.OrdinalIgnoreCase)) 
                return "stand";
            if (chatMessage.Equals("hit", StringComparison.OrdinalIgnoreCase) || chatMessage.Equals("h", StringComparison.OrdinalIgnoreCase)) 
                return "hit";
            if (chatMessage.Equals("double", StringComparison.OrdinalIgnoreCase) || chatMessage.Equals("d", StringComparison.OrdinalIgnoreCase) || chatMessage.Contains("dd", StringComparison.OrdinalIgnoreCase)) 
                return "double";
            if (chatMessage.Equals("split", StringComparison.OrdinalIgnoreCase) || chatMessage.Equals("p", StringComparison.OrdinalIgnoreCase)) 
                return "split";
            }
        }
    }
    private async void RevealDealerSecondCard()
    {
        if (!isRevealingDealerSecondCard)    
        {
            isRevealingDealerSecondCard = true;
            Chat($"/p Revealing the dealer's second card!");
            await DrawCards(dealer, 1);
            hasRevealedDealerSecondCard = true;
            isRevealingDealerSecondCard = false;
        }
    }

    public bool ShouldDealerHit()
    {
        int dealerTotal = GetDealerHandTotal(dealer.Cards, out bool hasSoft17);
        bool hitOnSoft17 = C.DealerHitOnSoft;
        int standThreshold = C.DealerStandThreshold;
        if (dealerTotal < standThreshold)
        {
            return true;
        }
        if (dealerTotal == 17)
        {
            return hasSoft17 && hitOnSoft17;
        }
        return false;
    }

    private int GetDealerHandTotal(List<int> cards, out bool hasSoft17)
    {
        int total = 0;
        int aceCount = 0;
        
        foreach (int card in cards)
        {
            if (card == 11)
            {
                aceCount++;
            }
            total += card;
        }

        while (total > 21 && aceCount > 0)
        {
            total -= 10;
            aceCount--;
        }

        hasSoft17 = total == 17 && aceCount > 0;

        return total;
    }
    private async Task DealerHits()
    {
        if (!isDrawingDealerHits)
        {
            isDrawingDealerHits = true;
            await DrawCards(dealer, 1);
            isDrawingDealerHits = false;
        }
    }
    private async Task DetermineWinners()
    {
        if (!isDeterminingWinners)
        {
            isDeterminingWinners = true;
            int dealerTotal = GetHandValue(dealer.Cards);
            bool dealerBlackjack = IsNaturalBlackjack(dealer.Cards);
            List<string> results = new List<string>();

            foreach (Player player in playerList)
            {
                int playerTotal = GetHandValue(player.Cards);
                bool playerBlackjack = IsNaturalBlackjack(player.Cards);
                string resultMessage;

                if (playerTotal > 21)
                {
                    // Player busts, automatic loss
                    player.Result = "Lost";
                    resultMessage = $"{player.Nick} busts with {playerTotal}! Dealer wins.";
                }
                else if (dealerTotal > 21)
                {
                    // Dealer busts, player wins
                    player.Result = playerBlackjack ? "Special Win" : "Win";
                    resultMessage = playerBlackjack
                        ? $"{player.Nick} wins with a NATURAL BLACKJACK!"
                        : $"{player.Nick} wins! Dealer busts with {dealerTotal}.";
                }
                else if (playerTotal == dealerTotal)
                {
                    // Handle Blackjack tie cases
                    if (playerBlackjack && !dealerBlackjack)
                    {
                        player.Result = "Special Win";
                        resultMessage = $"{player.Nick} wins with a NATURAL BLACKJACK!";
                    }
                    else if (!playerBlackjack && dealerBlackjack)
                    {
                        player.Result = "Lost";
                        resultMessage = $"{player.Nick} loses! Dealer has a natural Blackjack.";
                    }
                    else
                    {
                        player.Result = "Push";
                        resultMessage = $"{player.Nick} pushes with {playerTotal}.";
                    }
                }
                else if (playerTotal > dealerTotal)
                {
                    // Player wins by having a higher total
                    player.Result = playerBlackjack ? "Special Win" : "Win";
                    resultMessage = playerBlackjack
                        ? $"{player.Nick} wins with a NATURAL BLACKJACK!"
                        : $"{player.Nick} wins with {playerTotal} vs Dealer's {dealerTotal}!";
                }
                else
                {
                    // Dealer wins
                    player.Result = "Lost";
                    resultMessage = $"{player.Nick} loses with {playerTotal} vs Dealer's {dealerTotal}.";
                }

                if (player.Result == "Win")
                {
                    results.Add(player.Nick + " ");
                }
                Chat(resultMessage);
            }

            // Summarize results
            string summaryMessage = "Winners:" + results;
            Chat(summaryMessage);
            await AwaitChatQueue();

            isDeterminingWinners = false;
            hasDetermineWinners = true;
        }
    }
    private int GetHandValue(List<int> cards)
    {
        int total = 0;
        int aceCount = 0;

        foreach (int card in cards)
        {
            if (card == 11) aceCount++; // Aces counted separately
            total += card;
        }

        // Adjust Aces if needed (from 11 → 1 to avoid bust)
        while (total > 21 && aceCount > 0)
        {
            total -= 10;
            aceCount--;
        }

        return total;
    }
    private bool IsNaturalBlackjack(List<int> cards)
    {
        return cards.Count == 2 && GetHandValue(cards) == 21;
    }
    private void AwardPayouts()
    {
        if (!isAwardingPayouts)
        {
            isAwardingPayouts = true;
            foreach (var player in playerList)
            {
                switch (player.Result)
                {
                    case "Win":
                        player.Balance += player.BetAmount; // 2x payout
                        break;

                    case "Special Win":
                        player.Balance += (int)(player.BetAmount * 1.5); // 2.5x payout
                        break;

                    case "Push":
                        // Bet is kept, no changes to balance
                        break;

                    case "Lost":
                        player.BetAmount = 0; // Reset lost bet
                        break;
                }
            }
            foreach (Player player in playerList)
            {
                if (player.CashOut)
                {
                    while (player.Balance > 0 && !GetAddon("Trade"))
                    {
                        gilAmount = Math.Min(player.Balance,1000000);
                        player.Balance -= gilAmount;
                        Chat($"/trade {player.Index+1}");
                        needToSendTrade = true;
                    }
                }
            }
            hasAwardedPayouts = true;
            isAwardingPayouts = false;
        }
    }
    public void DrawTable()
    {
        if (ImGui.BeginTable("PartyTable", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            // Setup Column Headers
            ImGui.TableSetupColumn("Player Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Bet Amount", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Hit", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Stand", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Double Down", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Split", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthStretch);
            // ImGui.TableSetupColumn("Dealer Action", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableHeadersRow();
            // ---- Dealer Row ----
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            dealer = partyList[0];

            if (dealer.IsEditingNick)
            {
                ImGui.SetKeyboardFocusHere();
                // ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X/2 - ImGui.GetStyle().ItemSpacing.X);
                if (ImGui.InputText($"##nickname_input_{dealer.Name}", ref dealer.Nick, 32, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    dealer.IsEditingNick = false; // Exit edit mode when Enter is pressed
                }
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    dealer.IsEditingNick = false; // Exit edit mode if clicking elsewhere
                }
                // ImGui.PopItemWidth();
            }
            else // Show text, allow single-click to edit
            {
                if (ImGui.Selectable(dealer.Nick ?? dealer.Name, false)) // No double-click flag needed
                {
                    dealer.IsEditingNick = true; // Enter edit mode on single-click
                }
            }

            ImGui.TableNextColumn(); // Bet Amount (Empty for dealer)
            ImGui.Text("-");

            ImGui.TableNextColumn(); // Hit (Dealer doesn't hit like a player)
            ImGui.Text("-");

            ImGui.TableNextColumn(); // Stand
            ImGui.Text("-");

            ImGui.TableNextColumn(); // Double Down
            ImGui.Text("-");

            ImGui.TableNextColumn(); // Split
            ImGui.Text("-");

            ImGui.TableNextColumn(); // Cards
            if (dealer != null && dealer.Cards.Count > 0)
            {
                ImGui.Text(string.Join(", ", dealer.Cards));
            }
            else
            {
                ImGui.Text("No Cards");
            }

            // ImGui.Separator(); // Adds a visual break between dealer and Player


            foreach (var player in partyList)
            {
                if (player.Dealer == true) continue;
                ImGui.TableNextRow();

                // Player Name (With Right-Click Nickname Popup)
                ImGui.TableNextColumn();
                // float columnWidth = ImGui.GetColumnWidth();
                if (player.IsEditingNick)
                {
                    ImGui.SetKeyboardFocusHere();
                    // ImGui.PushItemWidth(columnWidth);
                    if (ImGui.InputText($"##nickname_input_{player.Name}", ref player.Nick, 32, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        player.IsEditingNick = false; // Exit edit mode when Enter is pressed
                    }
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        player.IsEditingNick = false; // Exit edit mode if clicking elsewhere
                    }
                    // ImGui.PopItemWidth();
                }
                else // Show text, allow single-click to edit
                {
                    // ImGui.PushItemWidth(columnWidth);
                    if (ImGui.Selectable(player.Nick ?? player.Name, false)) // No double-click flag needed
                    {
                        player.IsEditingNick = true; // Enter edit mode on single-click
                    }
                    // ImGui.PopItemWidth();
                }

                // Bet Amount Input
                ImGui.TableNextColumn();
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X/2);
                ImGui.InputInt($"##bet_{player.Name}", ref player.BetAmount, 0, 0, ImGuiInputTextFlags.CharsDecimal);
                ImGui.PopItemWidth();

                // Hit Button (Color Toggle)
                ImGui.TableNextColumn();
                bool colorPushed = false;
                SetButtonColor(player.Hit, out colorPushed);
                if (ImGui.Button("Hit##" + player.Name, new System.Numerics.Vector2(50, 25)))
                {
                    player.Hit = !player.Hit;
                }
                ResetButtonColor(colorPushed);

                // Stand Button (Color Toggle)
                ImGui.TableNextColumn();
                SetButtonColor(player.Stand, out colorPushed);
                if (ImGui.Button("Stand##" + player.Name, new System.Numerics.Vector2(50, 25)))
                {
                    player.Stand = !player.Stand;
                }
                ResetButtonColor(colorPushed);

                // Double Down Button (Color Toggle)
                ImGui.TableNextColumn();
                SetButtonColor(player.DoubleDown, out colorPushed);
                if (ImGui.Button("Double##" + player.Name, new System.Numerics.Vector2(90, 25)))
                {
                    player.DoubleDown = !player.DoubleDown;
                }
                ResetButtonColor(colorPushed);

                // Split Button (Color Toggle)
                ImGui.TableNextColumn();

                SetButtonColor(player.Split, out colorPushed);
                if (ImGui.Button("Split##" + player.Name, new System.Numerics.Vector2(50, 25)))
                {
                    player.Split = !player.Split;
                }
                ResetButtonColor(colorPushed);

                // Cards Column
                ImGui.TableNextColumn();
                string cardsText = player.Cards.Count > 0 ? string.Join(", ", player.Cards) : "No Cards";
                ImGui.TextColored(new System.Numerics.Vector4(1.0f, 1.0f, 0.5f, 1.0f), cardsText); // Gold color for cards

                // Dealer Action Button
                ImGui.TableNextColumn();
                if (ImGui.Button($"Process##{player.Name}", new System.Numerics.Vector2(100, 25)))
                {
                    // Future function for dealer action (e.g., resolve player's turn)
                    Console.WriteLine($"Processing action for {player.Name}");
                    chatManager.SendMessage($"/trade <{player.Index+1}>");
                }
            }

            ImGui.EndTable();
        }
    }
}