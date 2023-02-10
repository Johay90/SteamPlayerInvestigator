using System;
using System.Collections.Generic;
using System.Windows;
using System.Diagnostics;
using System.Linq;
using SteamWebAPI2.Utilities;
using SteamWebAPI2.Interfaces;
using System.Net.Http;
using Steam.Models.SteamCommunity;
using SteamPlayerInvestigator.Classes;
using System.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Numerics;

namespace SteamPlayerInvestigator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set
            {
                _statusMessage = value;
                OnPropertyChanged("StatusMessage");
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        private const string SteamApiKey = Credentials.ApiKey;

        private int increment = 0;

        //private readonly App _app = (App)Application.Current;

        private static readonly SteamWebInterfaceFactory
            WebInterfaceFactory = new SteamWebInterfaceFactory(SteamApiKey);

        private readonly Database _db;

        private static List<WeightedPlayer> WeightedPlayers = new List<WeightedPlayer>();

        public MainWindow()
        {
            InitializeComponent();
            _db = CreateDatabase();
            DataContext = this;
        }

        private Database CreateDatabase()
        {
            return new Database(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True", this);
        }

        private async Task<string> MostPlayedGame(ulong steamID)
        {
            PlayerService steamPlayerInterface = WebInterfaceFactory.CreateSteamWebInterface<PlayerService>();
            // get top played games
            ISteamWebResponse<OwnedGamesResultModel>
                ownedGames = await steamPlayerInterface.GetOwnedGamesAsync(steamID);
            OwnedGamesResultModel gamesData = ownedGames.Data;

            string mostPlayedGame = string.Empty;
            if (gamesData.OwnedGames.Any())
            {
                mostPlayedGame = gamesData.OwnedGames
                    .OrderByDescending(x => x.PlaytimeForever)
                    .First()
                    .Name;
            }

            return mostPlayedGame;
        }

        //  Levenshtein Distance Algorithm for comparing names
        private int StringSimilarity(string s, string t)
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

        private async Task<List<WeightedPlayer>> CalculateWeightedScores(List<PlayerSummaryModel> players,
            PlayerSummaryModel primaryAccount)
        {

            foreach (PlayerSummaryModel player in players)
            {
                if (player == primaryAccount) continue;

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

                string mostPlayedGame = null;
                if (score >= 3)
                {
                    mostPlayedGame = await MostPlayedGame(player.SteamId);
                    if (mostPlayedGame == await MostPlayedGame(primaryAccount.SteamId))
                    {
                        score++;
                    }
                }

                if (WeightedPlayers.All(x => x.Player.SteamId != player.SteamId))
                {
                    WeightedPlayers.Add(new WeightedPlayer(player, score, primaryAccount.Nickname, mostPlayedGame));
                }
            }

            WeightedPlayers = WeightedPlayers.OrderByDescending(x => x.Score).ToList();
            return WeightedPlayers;
        }

        private void RemoveFriends(ulong steamid)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            // get ids from friends for removing 
            con.Open();
            SqlCommand cmd = new SqlCommand("SELECT * FROM friends WHERE steamid = " + steamid, con);
            SqlDataReader reader = cmd.ExecuteReader();
            List<long> friends = new List<long>();
            while (reader.Read())
            {
                friends.Add((long)reader["friendswith"]);
            }

            con.Close();

            // remove from users table
            con.Open();
            foreach (long friend in friends)
            {
                cmd = new SqlCommand("DELETE FROM users WHERE steamid = " + friend, con);
                cmd.ExecuteNonQuery();
            }

            con.Close();

            // remove from friends table
            con.Open();
            SqlCommand del = new SqlCommand("DELETE FROM friends WHERE steamid = @steamid", con);
            del.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamid);
            del.ExecuteNonQuery();
            con.Close();

        }

        private bool UserExists(ulong steamID)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            con.Open();
            SqlCommand cmd = new SqlCommand("SELECT * FROM users WHERE steamid = @SteamID", con);
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);
            SqlDataReader reader = cmd.ExecuteReader();
            return reader.HasRows;
        }

        private bool UserNeedsUpdate(ulong steamID)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText =
                "SELECT * FROM users WHERE SteamID = @SteamID AND date_added < DATEADD(day, -7, GETDATE())";
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);
            cmd.Connection = con;
            con.Open();
            SqlDataReader reader = cmd.ExecuteReader();
            if (reader.HasRows)
            {
                con.Close();
                return true;
            }

            con.Close();
            return false;
        }

        private async Task UpdateUserData(PlayerSummaryModel player)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText =
                "UPDATE users SET steamid = @steamid, personaname = @personaname, profileurl = @profileurl, avatar = @avatar, avatarmedium = @avatarmedium, avatarfull = @avatarfull, personastate = @personastate, realname = @realname, primaryclanid = @primaryclanid, timecreated = @timecreated, loccountrycode = @loccountrycode, locstatecode = @locstatecode, loccityid = @loccityid, date_added = @date_added, banstatus = @banstatus WHERE steamid = @steamid";
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(player.SteamId);
            cmd.Parameters.Add("@profilestate", SqlDbType.Int).Value = Convert.ToInt32(player.ProfileState);
            cmd.Parameters.Add("@loccityid", SqlDbType.Int).Value = Convert.ToInt32(player.CityCode);
            cmd.Parameters.AddWithValue("@date_added", DateTime.Now);
            // check for valid datetime value
            cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value =
                player.AccountCreatedDate <= DateTime.MinValue ? DateTime.MinValue : player.AccountCreatedDate;
            cmd.Parameters.AddWithValue("@communityvisibilitystate", player.ProfileVisibility);
            cmd.Parameters.AddWithValue("@personastate", player.UserStatus);
            cmd.Parameters.AddWithValue("@personaname", player.Nickname);
            cmd.Parameters.AddWithValue("@profileurl", player.ProfileUrl);
            cmd.Parameters.AddWithValue("@avatar", player.AvatarUrl);
            cmd.Parameters.AddWithValue("@avatarmedium", player.AvatarMediumUrl);
            cmd.Parameters.AddWithValue("@avatarfull", player.AvatarFullUrl);

            // some of these can be null
            if (player.RealName == null)
            {
                cmd.Parameters.AddWithValue("@realname", DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@realname", player.RealName);
            }

            if (player.PrimaryGroupId == null)
            {
                cmd.Parameters.AddWithValue("@primaryclanid", DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@primaryclanid", player.PrimaryGroupId);
            }

            if (player.CountryCode == null)
            {
                cmd.Parameters.AddWithValue("@loccountrycode", DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@loccountrycode", player.CountryCode);
            }

            if (player.StateCode == null)
            {
                cmd.Parameters.AddWithValue("@locstatecode", DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@locstatecode", player.StateCode);
            }

            if (await CheckVacBan(player.SteamId))
            {
                cmd.Parameters.AddWithValue("@banstatus", 1);
            }
            else
            {
                cmd.Parameters.AddWithValue("@banstatus", 0);
            }

            cmd.Connection = con;
            con.Open();
            cmd.ExecuteNonQuery();
            con.Close();
        }

        private void InsertFriends(ulong steamID, ulong friendID)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText =
                "INSERT dbo.friends (steamid, friendswith) VALUES (@steamid, @friendswith)";

            // ints (don't need null checks)
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);
            cmd.Parameters.Add("@friendswith", SqlDbType.BigInt).Value = Convert.ToInt64(friendID);

            cmd.Connection = con;
            con.Open();
            cmd.ExecuteNonQuery();
            con.Close();
        }

        private async Task<PlayerSummaryModel> getPlayerInfo(ISteamUser steamInterface, ulong steamID)
        {
            ISteamWebResponse<PlayerSummaryModel> playerSummaryResponse = await steamInterface.GetPlayerSummaryAsync(steamID);
            return playerSummaryResponse?.Data;
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

        public async Task<bool> CheckVacBan(ulong steamID)
        {
            string steamAPIKey = "7E7C3A26841681369678AE28CDF62901";
            // factory to be used to generate various web interfaces
            SteamWebInterfaceFactory webInterfaceFactory = new SteamWebInterfaceFactory(steamAPIKey);

            // this will map to the ISteamUser endpoint
            SteamUser steamInterface = webInterfaceFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());

            try
            {
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

        private async Task<List<PlayerSummaryModel>> AvailableAccounts(ulong steamID)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            //cmd.CommandText ="SELECT * FROM dbo.users INNER JOIN dbo.friends ON dbo.users.steamid = dbo.friends.friendswith WHERE dbo.users.banstatus = 1";
            cmd.CommandText =
                "SELECT * FROM dbo.users INNER JOIN dbo.friends ON dbo.friends.friendswith = dbo.users.steamid WHERE dbo.users.banstatus = 1 AND dbo.friends.steamid = @steamid";
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);

            // put statement into PlayerSummaryModel datareader list
            List<PlayerSummaryModel> players = new List<PlayerSummaryModel>();
            cmd.Connection = con;
            con.Open();
            SqlDataReader reader = await cmd.ExecuteReaderAsync();
            // add score to PlayerSummaryModel
            while (reader.Read())
            {

                PlayerSummaryModel currPlayer = new PlayerSummaryModel
                {
                    SteamId = Convert.ToUInt64(reader["steamid"]),
                    ProfileUrl = reader["profileurl"].ToString(),
                    AvatarUrl = reader["avatar"].ToString(),
                    AvatarMediumUrl = reader["avatarmedium"].ToString(),
                    AvatarFullUrl = reader["avatarfull"].ToString(),
                    RealName = reader["realname"].ToString(),
                    PrimaryGroupId = reader["primaryclanid"].ToString(),
                    AccountCreatedDate = Convert.ToDateTime(reader["timecreated"]),
                    UserStatus = (UserStatus)Convert.ToInt32(reader["personastate"]),
                    ProfileVisibility = (ProfileVisibility)Convert.ToInt32(reader["communityvisibilitystate"]),
                    Nickname = reader["personaname"].ToString(),
                    ProfileState = (uint)Convert.ToInt32(reader["profilestate"]),
                    CountryCode = reader["loccountrycode"].ToString(),
                    StateCode = reader["locstatecode"].ToString(),
                    CityCode = Convert.ToUInt32(reader["loccityid"].ToString()),

                };
                if (currPlayer.SteamId != steamID)
                {
                    players.Add(currPlayer);
                }
            }

            con.Close();
            return players;
        }

        private async Task<IReadOnlyCollection<FriendModel>> GetFriendList(ISteamUser steamInterface, ulong steamID)
        {
            try
            {
                ISteamWebResponse<IReadOnlyCollection<FriendModel>> friendListResponse = await steamInterface.GetFriendsListAsync(steamID);
                return friendListResponse?.Data;
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Skipping current friend, thrown an error. Likely private data?");
                return null;
            }
        }

        private async Task AddFriendData(ISteamUser steamInterface, ulong steamID)
        {
            IReadOnlyCollection<FriendModel> friendsList = await GetFriendList(steamInterface, steamID);

            if (friendsList != null)
            {
                List<Task<PlayerSummaryModel>> playerTasks = new List<Task<PlayerSummaryModel>>();
                foreach (FriendModel friend in friendsList)
                {
                    playerTasks.Add(GetBulkPlayerInfo(steamInterface, friend.SteamId));
                    increment++;
                    StatusMessage = "Getting friend data " + "(" + increment + ") SteamID: " + friend.SteamId;
                    Debug.WriteLine("Getting friend data " + friend.SteamId);
                    await Task.Delay(1000); // Wait for 1 second
                }

                PlayerSummaryModel[] playerSummaries = await Task.WhenAll(playerTasks);
                increment = 0;
                // TODO Hold this data until we can confirm fof list is complete
                foreach (PlayerSummaryModel currPlayer in playerSummaries)
                {
                    if (currPlayer != null)
                    {
                        await _db.InsertDataAsync(currPlayer);
                        InsertFriends(steamID, currPlayer.SteamId);
                        increment++;
                        StatusMessage = "Added friend data. " + "(" + increment + ") SteamID: " + currPlayer.SteamId;
                        Debug.WriteLine("Added friend data. steamID: " + currPlayer.SteamId);
                    }
                    else
                    {
                        Debug.WriteLine("Skipping current player, likely a bad profile");
                    }
                }
            }
            else
            {
                Debug.WriteLine("Error, skipping current friend list. Likely a private profile.");
            }
        }

        private async Task AddFriendOfFriendData(ISteamUser steamInterface, ulong steamID)
        {
            IReadOnlyCollection<FriendModel> friendsList = await GetFriendList(steamInterface, steamID);

            if (friendsList != null)
            {
                List<Task<PlayerSummaryModel>> playerTasks = new List<Task<PlayerSummaryModel>>();
                List<Task<IReadOnlyCollection<FriendModel>>> friendListTasks =
                    new List<Task<IReadOnlyCollection<FriendModel>>>();
                increment = 0;
                foreach (FriendModel friend in friendsList)
                {
                    Debug.WriteLine("Getting player info for processing - steamID: " + friend.SteamId);
                    increment++;
                    StatusMessage = "Getting player info for processing " + "(" + increment + ") SteamID: " + friend.SteamId;
                    playerTasks.Add(GetBulkPlayerInfo(steamInterface, friend.SteamId));
                    await Task.Delay(1000); // Wait for 1 second
                }

                IReadOnlyCollection<PlayerSummaryModel> players = await Task.WhenAll(playerTasks);
                increment = 0;

                foreach (PlayerSummaryModel currPlayer in players)
                {
                    if (currPlayer is { ProfileVisibility: ProfileVisibility.Public })
                    {
                        Debug.WriteLine("Getting friend list of - steamID: " + currPlayer.SteamId);
                        increment++;
                        StatusMessage = "Getting friend list from steam user " + "(" + increment + ") SteamID: " + currPlayer.SteamId;
                        friendListTasks.Add(GetFriendList(steamInterface, currPlayer.SteamId));
                        await Task.Delay(1000); // Wait for 1 second
                    }
                }

                IReadOnlyCollection<IReadOnlyCollection<FriendModel>> friendOfFriendLists =
                    await Task.WhenAll(friendListTasks);

                increment = 0;
                foreach (IReadOnlyCollection<FriendModel> friendOfFriendList in friendOfFriendLists)
                {
                    if (friendOfFriendList != null)
                    {
                        foreach (FriendModel friendOfFriend in friendOfFriendList)
                        {
                            increment++;
                            StatusMessage = "Getting player info for processing (fof list) " + "(" + increment + ") SteamID: " + friendOfFriend.SteamId;
                            Debug.WriteLine("Getting player info for processing (fof list) - steamID: " + friendOfFriend.SteamId);
                            playerTasks.Add(GetBulkPlayerInfo(steamInterface, friendOfFriend.SteamId));
                            await Task.Delay(1000); // Wait for 1 second
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Skipping current friend of friend list. Likely a private profile.");
                    }
                }

                PlayerSummaryModel[] playerSummaries = await Task.WhenAll(playerTasks);
                increment = 0;

                await Task.WhenAll(
                    playerSummaries.Select(async currPlayer =>
                    {
                        if (currPlayer != null)
                        {
                            await _db.InsertDataAsync(currPlayer);
                            InsertFriends(steamID, currPlayer.SteamId);
                            increment++;
                            StatusMessage = "Added fof data to database " + "(" + increment + ") SteamID: " + currPlayer.SteamId;
                            Debug.WriteLine("Added fof data to database. steamID: " + currPlayer.SteamId);
                            await Task.Delay(1000); // Wait for 1 second
                        }
                    })
                );
            }
            else
            {
                Debug.WriteLine("Error, skipping current friend list. Likely a private profile.");
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            // TODO Move away from static/single instance variables/methods here so that we can reuse button_click
            // for example AvailableAccounts might be a problem
            // data context for debugging listbox is also a problem re: instancing
            // Can prolly just empty WeightedPlayers since it's global scope, but maybe find a better solution

            WeightedPlayers.Clear();

            // need to convert steamiD to ulong for steamInterface
            ulong steamID = Convert.ToUInt64(TextBoxSteamID.Text);
            string steamAPIKey = Credentials.ApiKey;

            // factory to be used to generate various web interfaces
            SteamWebInterfaceFactory webInterfaceFactory = new SteamWebInterfaceFactory(steamAPIKey);

            // this will map to the ISteamUser endpoint
            SteamUser steamInterface = webInterfaceFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());

            PlayerSummaryModel player;

            if (UserExists(steamID)) 
            {
                if (UserNeedsUpdate(steamID))
                {
                    StatusMessage = "User needs update";
                    player = await getPlayerInfo(steamInterface, steamID);
                    RemoveFriends(steamID);
                    await UpdateUserData(player);
                    Debug.WriteLine("updated steamID: " + player.SteamId);
                    await AddFriendData(steamInterface, steamID);
                    await AddFriendOfFriendData(steamInterface, steamID);
                }
                else
                {
                    player = await getPlayerInfo(steamInterface, steamID);
                    Debug.WriteLine("User is up to date");
                    StatusMessage = "User is up to date";
                }
            }
            else
            {
                player = await getPlayerInfo(steamInterface, steamID);
                StatusMessage = "User doesn't exist, inserting. SteamID: " + player.SteamId;
                await _db.InsertDataAsync(player);
                Debug.WriteLine("User doesn't exist, inserting. SteamID: " + player.SteamId);
                await AddFriendData(steamInterface, steamID);
                await AddFriendOfFriendData(steamInterface, steamID);
            }

            PlayerSummaryModel primaryAccount = player;
            List<PlayerSummaryModel> players = await AvailableAccounts(player.SteamId);
            List<WeightedPlayer> weightedPlayers = await CalculateWeightedScores(players, primaryAccount);

            // get player with top score
            PlayerSummaryModel topPlayer = weightedPlayers.Aggregate((l, r) => l.Score > r.Score ? l : r).Player;
            // get top score value
            int topScore = weightedPlayers.Aggregate((l, r) => l.Score > r.Score ? l : r).Score;

            int maxScore = 10;
            // output window
            output outputWindow = new output();
            outputWindow.Show();
            if (primaryAccount != null) outputWindow.labelPrimaryAccount.Content = primaryAccount.Nickname + "(" + primaryAccount.SteamId + ")";
            outputWindow.labelSecondaryAccount.Content = topPlayer.Nickname + "(" + topPlayer.SteamId + ")";
            
            double perc = (double)topScore / maxScore * 100;
            outputWindow.labelPercMsg.Content = "This secondary account scored a total of " + perc + "%";


        }
        
        private void debugButton_Click(object sender, RoutedEventArgs e)
        {
            DebugWindow debugWindow = new DebugWindow();
            debugWindow.DataContext = WeightedPlayers;
            debugWindow.Show();
        }
    }
    }
