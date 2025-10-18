namespace GNW2.GameManager
{
    public enum GameState
    {
        WaitingForPlayers,  // Waiting for minimum players to join
        RoundStarting,      // Round is starting, show UI
        WaitingForSelections, // Players are making their selections
        Evaluating,         // Evaluating the round results
        ShowingResults,     // Showing win/loss/draw results
        RoundEnding         // Round ending, preparing for next round
    }
}
