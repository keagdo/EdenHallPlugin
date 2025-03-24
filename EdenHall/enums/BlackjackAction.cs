public enum BlackjackAction
{
    Hit,          // Take another card
    Stand,        // End turn and keep current hand
    DoubleDown,   // Double the bet, take one more card, then stand
    Split,        // Split into two hands if both cards match
    Insurance,    // Side bet if dealer has an Ace
    Surrender     // Give up and get back half the bet (if allowed)
}
