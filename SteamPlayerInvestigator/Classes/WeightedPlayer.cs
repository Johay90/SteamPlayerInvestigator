using System;
using System.Collections.Generic;
using System.Text;
using Steam.Models.SteamCommunity;

namespace SteamPlayerInvestigator.Classes
{
    public class WeightedPlayer
    {
        public PlayerSummaryModel Player { get; set; }
        public int Score { get; set; }
        public string AccountConectedTo { get; set; }
        public string MostPlayedGame { get; set; }

        public WeightedPlayer(PlayerSummaryModel player, int score, string accountConectedTo, string mostPlayedGame)
        {
            Player = player;
            Score = score;
            AccountConectedTo = accountConectedTo;
            MostPlayedGame = mostPlayedGame;
        }
    }
}
