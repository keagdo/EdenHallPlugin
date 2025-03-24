public enum BlackjackPhase
{
    Betting,       // Players place their bets
    Dealing,       // Initial two cards are dealt
    PlayerTurn,    // Players take actions (hit, stand, etc.)
    DealerTurn,    // Dealer plays their hand
    Payout,        // Winners are determined, bets are settled
    RoundEnd       // Round resets for the next game
}