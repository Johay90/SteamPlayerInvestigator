using Steam.Models.SteamCommunity;
using SteamWebAPI2.Interfaces;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using SteamWebAPI2.Utilities;
using System.Diagnostics;
using System.Linq;
using System;
using System.Windows.Markup;

namespace SteamPlayerInvestigator.Classes
{
    public class SteamDataService
    {
        private readonly string steamAPIKey;
        private readonly Database _db;
        private readonly StatusMessageService _statusMessageService; // TODO add more status message inside here
        private const int RateLimitProtectionDelay = 1000;

        /// <summary>
        /// Constructor for the SteamDataService class
        /// </summary>
        /// <param name="steamAPIKey">
        /// The API key for the Steam Web API
        /// </param>
        /// <param name="statusMessageService">
        /// The status message service for the application
        /// </param>
        public SteamDataService(string steamAPIKey, StatusMessageService statusMessageService = null)
        {
            this.steamAPIKey = steamAPIKey;
            _db = new Database("Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\JMumm\\OneDrive\\Documents\\Uni\\Y3\\Final_hons\\Project\\SteamPlayerInvestigator\\SteamPlayerInvestigator\\Database1.mdf;Integrated Security=True", this);
            // if fails try this..
            //_db = new Database("Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=C:\\USERS\\JMUMM\\ONEDRIVE\\DOCUMENTS\\UNI\\Y3\\FINAL_HONS\\PROJECT\\STEAMPLAYERINVESTIGATOR\\STEAMPLAYERINVESTIGATOR\\DATABASE1.MDF;Integrated Security=True", this);

            _statusMessageService = statusMessageService;
        }

        public async Task<PlayerSummaryModel> GetPrimaryAccountAsync(ulong steamID)
        {

            (SteamWebInterfaceFactory webInterfaceFactory, SteamUser steamInterface) = CreateSteamInterface();

            PlayerSummaryModel player;

            if (_db.UserExists(steamID))
            {
                if (_db.UserNeedsUpdate(steamID))
                {
                    player = await GetPlayerSummaryAsync(steamInterface, steamID);
                    _db.RemoveFriends(steamID);
                    await _db.UpdateUserData(player);
                    IReadOnlyCollection<PlayerSummaryModel> friendsList = await GetFriendData(steamInterface, steamID);
                    await AddFriendDataToDb(friendsList, steamID);
                    await AddFriendOfFriendData(steamInterface, steamID);
                }
                else
                {
                    player = await GetPlayerSummaryAsync(steamInterface, steamID);
                }
            }
            else
            {
                player = await GetPlayerSummaryAsync(steamInterface, steamID);
                await _db.InsertDataAsync(player);
                IReadOnlyCollection<PlayerSummaryModel> friendsList = await GetFriendData(steamInterface, steamID);
                await AddFriendDataToDb(friendsList, steamID);
                await AddFriendOfFriendData(steamInterface, steamID);
            }

            return player;
        }

        public async Task<List<WeightedPlayer>> CalculateWeightedScoresAsync(ulong steamID)
        {
            PlayerSummaryModel primaryAccount = await GetPrimaryAccountAsync(steamID);
            List<PlayerSummaryModel> players = await _db.AvailableAccounts(primaryAccount.SteamId);
            List<WeightedPlayer> weightedPlayers = await CalculateWeightedPlayers(players, primaryAccount);

            return weightedPlayers;
        }

        public async Task<bool> IsVacBannedAsync(ulong steamID)
        {
            try
            {
                SteamWebInterfaceFactory webInterfaceFactory = new SteamWebInterfaceFactory(steamAPIKey);
                SteamUser steamInterface = webInterfaceFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());

                await Task.Delay(RateLimitProtectionDelay);

                ISteamWebResponse<IReadOnlyCollection<PlayerBansModel>> vacBanResponse =
                    await steamInterface.GetPlayerBansAsync(steamID);
                return vacBanResponse.Data.First().NumberOfVACBans > 0;
            }
            catch (TaskCanceledException e)
            {
                Debug.WriteLine("Skipping current friend, thrown an error. Likely private data?");
                return false;
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Skipping current friend, thrown an error. Likely private data?");
                return false;
            }
        }

        /// <summary>
        ///    Creates the Steam interfaces required to make requests to the Steam Web API.
        /// </summary>
        /// <returns>
        ///  A tuple containing the <see cref="SteamWebInterfaceFactory"/> and <see cref="SteamUser"/> interfaces.
        /// </returns>
        private (SteamWebInterfaceFactory webInterfaceFactory, SteamUser steamInterface) CreateSteamInterface()
        {
            SteamWebInterfaceFactory webInterfaceFactory = new SteamWebInterfaceFactory(steamAPIKey);
            SteamUser steamInterface = webInterfaceFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());
            return (webInterfaceFactory, steamInterface);
        }

        private async Task<PlayerSummaryModel> GetPlayerSummaryAsync(ISteamUser steamInterface, ulong steamID)
        {
            try
            {
                ISteamWebResponse<PlayerSummaryModel> playerSummaryResponse =
                    await steamInterface.GetPlayerSummaryAsync(steamID);
                if (playerSummaryResponse == null)
                {
                    Debug.WriteLine($"Could not retrieve player summary for SteamID: {steamID}");
                    return null;
                }

                return playerSummaryResponse.Data;
            }
            catch (TaskCanceledException e)
            {
                Debug.WriteLine(
                    $"Request to get player summary for SteamID: {steamID} was cancelled. Error: {e.Message}");
                return null;
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine($"Request to get player summary for SteamID: {steamID} failed. Error: {e.Message}");
                return null;
            }
        }

        private async Task<IReadOnlyCollection<PlayerSummaryModel>> GetFriendData(ISteamUser steamInterface,
            ulong steamID)
        {
            IReadOnlyCollection<FriendModel> friendsList = await GetFriendListAsync(steamInterface, steamID);
            List<PlayerSummaryModel> playerSummaries = new List<PlayerSummaryModel>();
            if (friendsList != null)
            {
                List<Task<PlayerSummaryModel>> playerTasks = new List<Task<PlayerSummaryModel>>();
                foreach (FriendModel friend in friendsList)
                {
                    playerTasks.Add(GetBulkPlayerInfo(steamInterface, friend.SteamId));
                    _statusMessageService.StatusMessage = "Getting friend data. SteamID: " + friend.SteamId;
                    Debug.WriteLine("Getting friend data " + friend.SteamId);
                    await Task.Delay(RateLimitProtectionDelay);
                }

                PlayerSummaryModel[] playerSummaryArray = await Task.WhenAll(playerTasks);
                playerSummaries = playerSummaryArray.Where(x => x != null).ToList();
            }
            else
            {
                Debug.WriteLine("Error, skipping current friend list. Likely a private profile.");
            }

            return playerSummaries;
        }

        private async Task<PlayerSummaryModel> GetBulkPlayerInfo(ISteamUser steamInterface, ulong steamID)
        {
            try
            {
                ISteamWebResponse<PlayerSummaryModel> playerSummaryResponse = await steamInterface.GetPlayerSummaryAsync(steamID);
                return playerSummaryResponse?.Data;
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Skipping current friend, thrown an error " + e.Message);
                return null;
            }
        }

        private async Task AddFriendDataToDb(IReadOnlyCollection<PlayerSummaryModel> playerSummaries, ulong steamID)
        {
            foreach (PlayerSummaryModel playerSummary in playerSummaries)
            {
                await _db.InsertDataAsync(playerSummary);
                await _db.InsertFriendsAsync(steamID, playerSummary.SteamId);
                _statusMessageService.StatusMessage = "Added fof data to database. steamID: " + playerSummary.SteamId;
                Debug.WriteLine("Added fof data to database. steamID: " + playerSummary.SteamId);
            }
        }

        private async Task AddFriendOfFriendData(ISteamUser steamInterface, ulong steamID)
        {
            IReadOnlyCollection<FriendModel> friendsList = await GetFriendListAsync(steamInterface, steamID);

            if (friendsList != null)
            {
                List<Task<PlayerSummaryModel>> playerTasks = new List<Task<PlayerSummaryModel>>();
                foreach (FriendModel friend in friendsList)
                {
                    await Task.Delay(RateLimitProtectionDelay);
                    _statusMessageService.StatusMessage = "Getting player info for processing - steamID: " + friend.SteamId;
                    Debug.WriteLine("Getting player info for processing - steamID: " + friend.SteamId);
                    playerTasks.Add(GetBulkPlayerInfo(steamInterface, friend.SteamId));
                }

                IReadOnlyCollection<PlayerSummaryModel> players = await Task.WhenAll(playerTasks);

                foreach (PlayerSummaryModel currPlayer in players)
                {
                    if (currPlayer.ProfileVisibility == ProfileVisibility.Public)
                    {
                        IReadOnlyCollection<FriendModel> friendOfFriendList =
                            await GetFriendListAsync(steamInterface, currPlayer.SteamId);
                        
                        if (friendOfFriendList != null)
                        {
                            foreach (FriendModel friendOfFriend in friendOfFriendList)
                            {
                                await Task.Delay(RateLimitProtectionDelay);
                                _statusMessageService.StatusMessage = "Getting player info for processing (fof list) - steamID: " + friendOfFriend.SteamId;
                                Debug.WriteLine("Getting player info for processing (fof list) - steamID: " +
                                                friendOfFriend.SteamId);
                                playerTasks.Add(GetBulkPlayerInfo(steamInterface, friendOfFriend.SteamId));
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Skipping current friend of friend list. Likely a private profile.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Skipping current friend of friend list. Likely a private profile.");
                    }
                }

                IReadOnlyCollection<PlayerSummaryModel> playerSummaries = await Task.WhenAll(playerTasks);
                await AddFriendDataToDb(playerSummaries, steamID);
            }
            else
            {
                Debug.WriteLine("Error, skipping current friend list. Likely a private profile.");
            }
        }

        private async Task<IReadOnlyCollection<FriendModel>> GetFriendListAsync(ISteamUser steamInterface, ulong steamID)
        {
            try
            {
                ISteamWebResponse<IReadOnlyCollection<FriendModel>> friendListResponse =
                    await steamInterface.GetFriendsListAsync(steamID);
                if (friendListResponse == null)
                {
                    Debug.WriteLine($"Could not retrieve friend list for SteamID: {steamID}");
                    return null;
                }

                return friendListResponse.Data;
            }
            catch (TaskCanceledException e)
            {
                Debug.WriteLine($"Request to get friend list for SteamID: {steamID} was cancelled. Error: {e.Message}");
                return null;
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine($"Request to get friend list for SteamID: {steamID} failed. Error: {e.Message}");
                return null;
            }
        }

        private async Task<List<WeightedPlayer>> CalculateWeightedPlayers(List<PlayerSummaryModel> players,
            PlayerSummaryModel primaryAccount)
        {
            var weightedPlayers = new List<WeightedPlayer>();

            foreach (var player in players)
            {
                if (player == primaryAccount) continue;

                int score = CalculateScore(player, primaryAccount);
                string mostPlayedGame = null;
                if (score >= 3)
                {
                    mostPlayedGame = await GetMostPlayedGame(player.SteamId);
                    score += await GetMostPlayedGameScore(mostPlayedGame, primaryAccount);
                }

                if (weightedPlayers.All(x => x.Player.SteamId != player.SteamId))
                {
                    weightedPlayers.Add(new WeightedPlayer(player, score, primaryAccount.Nickname, mostPlayedGame));
                }
            }

            return weightedPlayers.OrderByDescending(x => x.Score).ToList();
        }

        private int CalculateScore(PlayerSummaryModel player, PlayerSummaryModel primaryAccount)
        {
            int score = 0;
            if (player.CityCode == primaryAccount.CityCode && player.CityCode != 0) score++;
            if (player.CountryCode == primaryAccount.CountryCode) score++;
            if (player.StateCode == primaryAccount.StateCode) score++;
            if (player.PrimaryGroupId == primaryAccount.PrimaryGroupId) score++;

            if (player.RealName != null && primaryAccount.RealName != null)
            {
                if (player.RealName == primaryAccount.RealName)
                {
                    score += 3;
                }
                else if (StringSimilarity(player.RealName, primaryAccount.RealName) < 3)
                {
                    score += 2;
                }
            }

            if (player.Nickname == primaryAccount.Nickname)
            {
                score += 3;
            }
            else if (StringSimilarity(player.Nickname, primaryAccount.Nickname) < 3)
            {
                score += 2;
            }

            return score;
        }

        private async Task<int> GetMostPlayedGameScore(string mostPlayedGame, PlayerSummaryModel primaryAccount)
        {
            return mostPlayedGame == await GetMostPlayedGame(primaryAccount.SteamId) ? 1 : 0;
        }
        
        private async Task<string> GetMostPlayedGame(ulong steamID)
        {
            var steamWebInterfaceFactory = new SteamWebInterfaceFactory(steamAPIKey);
            var steamPlayerInterface = steamWebInterfaceFactory.CreateSteamWebInterface<PlayerService>();

            var ownedGames = await steamPlayerInterface.GetOwnedGamesAsync(steamID);
            var gamesData = ownedGames.Data;

            var mostPlayedGame = string.Empty;
            if (gamesData.OwnedGames.Any())
            {
                mostPlayedGame = gamesData.OwnedGames
                    .OrderByDescending(x => x.PlaytimeForever)
                    .First()
                    .Name;
            }

            return mostPlayedGame;
        }

        private static int StringSimilarity(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                return !string.IsNullOrEmpty(t) ? t.Length : 0;
            }

            if (string.IsNullOrEmpty(t))
            {
                return !string.IsNullOrEmpty(s) ? s.Length : 0;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            for (int i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (int j = 0; j <= m; d[0, j] = j++)
            {
            }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }
}