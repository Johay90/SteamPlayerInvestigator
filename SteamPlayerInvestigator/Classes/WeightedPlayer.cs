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

        public WeightedPlayer(PlayerSummaryModel player, int score)
        {
            Player = player;
            Score = score;
        }
    }
}
