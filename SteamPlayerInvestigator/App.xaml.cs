using Steam.Models.SteamCommunity;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Utilities;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace SteamPlayerInvestigator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        // TODO refactor these methods - Can be transferred into their own classes, and class methods. 
        // They don't need to be static after reactor.
        // will need to do this for unit testing purposes (plus cleaner code..).

        //  Levenshtein Distance Algorithm for comparing names
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

        public static Dictionary<PlayerSummaryModel, int> CalculateWeightedScores(List<PlayerSummaryModel> players, PlayerSummaryModel primaryAccount)
        {
            // coudn't add property to PlayerSummaryModel, so I'm using an Dictionary for score
            Dictionary<PlayerSummaryModel, int> weightedPlayers = new Dictionary<PlayerSummaryModel, int>();

            foreach (PlayerSummaryModel player in players)
            {
                int score = 0;
                if (player == primaryAccount)
                {
                    // primary user, we can skip
                }
                else
                {
                    // TODO need to check or null, or 0s (Basically check for blank records)
                    if (player.CityCode == primaryAccount.CityCode && player.CityCode != 0)
                    {
                        score += 1;
                    }
                    if (player.CountryCode == primaryAccount.CountryCode)
                    {
                        score += 1;
                    }
                    if (player.StateCode == primaryAccount.StateCode)
                    {
                        score += 1;
                    }
                    if (player.PrimaryGroupId == primaryAccount.PrimaryGroupId)
                    {
                        score += 1;
                    }
                    if (player.RealName != null && primaryAccount.RealName != null)
                    {
                        if (player.RealName == primaryAccount.RealName)
                        {
                            score += 1;
                        }
                        else
                        {
                            if (StringSimilarity(player.RealName, primaryAccount.RealName) < 3)
                            {
                                score += 1;
                            }
                        }
                    }
                    if (player.Nickname == primaryAccount.Nickname)
                    {
                        score += 1;
                    }
                    else
                    {
                        if (StringSimilarity(player.Nickname, primaryAccount.Nickname) < 3)
                        {
                            score += 1;
                        }
                    }
                }
                weightedPlayers.Add(player, score);
            }
            return weightedPlayers;
        }

        public static void RemoveFriends(ulong steamid)
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

        public static bool UserExists(ulong steamID)
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

        public static bool UserNeedsUpdate(ulong steamID)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT * FROM users WHERE SteamID = @SteamID AND date_added < DATEADD(day, -7, GETDATE())";
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

        public static async Task UpdateUserData(PlayerSummaryModel player)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "UPDATE users SET steamid = @steamid, personaname = @personaname, profileurl = @profileurl, avatar = @avatar, avatarmedium = @avatarmedium, avatarfull = @avatarfull, personastate = @personastate, realname = @realname, primaryclanid = @primaryclanid, timecreated = @timecreated, loccountrycode = @loccountrycode, locstatecode = @locstatecode, loccityid = @loccityid, date_added = @date_added, banstatus = @banstatus WHERE steamid = @steamid";
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(player.SteamId);
            cmd.Parameters.Add("@profilestate", SqlDbType.Int).Value = Convert.ToInt32(player.ProfileState);
            cmd.Parameters.Add("@loccityid", SqlDbType.Int).Value = Convert.ToInt32(player.CityCode);
            cmd.Parameters.AddWithValue("@date_added", DateTime.Now);
            // check for valid datetime value
            cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value = player.AccountCreatedDate <= DateTime.MinValue ? DateTime.MinValue : player.AccountCreatedDate;
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

        public static async Task InsertDataAsync(PlayerSummaryModel player)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText =
                "INSERT dbo.users (steamid, communityvisibilitystate, profilestate, personaname, profileurl, avatar, avatarmedium, avatarfull, personastate, realname, primaryclanid, timecreated, loccountrycode, locstatecode, loccityid, banstatus, date_added) VALUES (@steamid, @communityvisibilitystate, @profilestate, @personaname, @profileurl, @avatar, @avatarmedium, @avatarfull, @personastate, @realname, @primaryclanid, @timecreated, @loccountrycode, @locstatecode, @loccityid, @banstatus, @date_added)";

            // ints (don't need null checks)
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(player.SteamId);
            cmd.Parameters.Add("@profilestate", SqlDbType.Int).Value = Convert.ToInt32(player.ProfileState);
            cmd.Parameters.Add("@loccityid", SqlDbType.Int).Value = Convert.ToInt32(player.CityCode);
            cmd.Parameters.AddWithValue("@date_added", DateTime.Now);
            // check for valid datetime value
            cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value = player.AccountCreatedDate <= DateTime.MinValue ? DateTime.MinValue : player.AccountCreatedDate;
            cmd.Parameters.AddWithValue("@communityvisibilitystate", player.ProfileVisibility);
            cmd.Parameters.AddWithValue("@personastate", player.UserStatus); 
            cmd.Parameters.AddWithValue("@personaname", player.Nickname);
            cmd.Parameters.AddWithValue("@profileurl", player.ProfileUrl);
            cmd.Parameters.AddWithValue("@avatar", player.AvatarUrl);
            cmd.Parameters.AddWithValue("@avatarmedium", player.AvatarMediumUrl);
            cmd.Parameters.AddWithValue("@avatarfull", player.AvatarFullUrl);
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

        private static void InsertFriends(ulong steamID, ulong friendID)
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

        public static async Task<PlayerSummaryModel> getPlayerInfo(ISteamUser steamInterface, ulong steamID)
        {
            ISteamWebResponse<PlayerSummaryModel> playerSummaryResponse = await steamInterface.GetPlayerSummaryAsync(steamID);
            return playerSummaryResponse?.Data;
        }

        private static async Task<IReadOnlyCollection<FriendModel>> getFriendList(ISteamUser steamInterface, ulong steamID)
        {
            // exception handling for System.Net.Http.HttpRequestException
            try
            {
                ISteamWebResponse<IReadOnlyCollection<FriendModel>> friendListResponse = await steamInterface.GetFriendsListAsync(steamID);
                return friendListResponse.Data;
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Skipping current friend, thrown an error. Likely hidden Friends List.");
                return null;
            }
        }

        private static async Task<bool> CheckVacBan(ulong steamID)
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
        
        public static async Task<List<PlayerSummaryModel>> AvailableAccounts(ulong steamID)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT * FROM dbo.users INNER JOIN dbo.friends ON dbo.users.steamid = dbo.friends.friendswith WHERE dbo.users.banstatus = 1";
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

        // TODO refactor this
        public static async Task AddFriendData(ISteamUser steamInterface, ulong steamID)
        {
            // get friend data
            // TODO add a check here to deal with hidden friend list (check fof insert)
            IReadOnlyCollection<FriendModel> friendsList = await getFriendList(steamInterface, steamID);

            // add friend data
            if (friendsList != null)
            {
                foreach (FriendModel friend in friendsList)
                {
                    PlayerSummaryModel currPlayer = await getPlayerInfo(steamInterface, friend.SteamId);
                    if (currPlayer != null)
                    {
                        await InsertDataAsync(currPlayer);
                        InsertFriends(steamID, currPlayer.SteamId);
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

            // add friend of friend data
            if (friendsList != null)
            {
                foreach (FriendModel friend in friendsList)
                {
                    IReadOnlyCollection<FriendModel> friendOfFriendList = null;
                    PlayerSummaryModel currPlayer = await getPlayerInfo(steamInterface, friend.SteamId);

                    // null check on currPlayer
                    if (currPlayer == null)
                    {
                        Debug.WriteLine("Skipping current player, likely a bad profile");
                        continue;
                    }

                    if (currPlayer.ProfileVisibility == ProfileVisibility.Public)
                    {
                        friendOfFriendList = await getFriendList(steamInterface, friend.SteamId);
                    }

                    if (friendOfFriendList != null)
                    {
                        foreach (FriendModel friendOfFriend in friendOfFriendList)
                        {
                            PlayerSummaryModel friendOfFriendPlayer =
                                await getPlayerInfo(steamInterface, friendOfFriend.SteamId);
                            // null check on friendOfFriendPlayer
                            if (friendOfFriendPlayer != null)
                            {
                                await InsertDataAsync(friendOfFriendPlayer);
                                InsertFriends(currPlayer.SteamId, friendOfFriendPlayer.SteamId);
                                Debug.WriteLine("Added friend of friend data. steamID: " + friendOfFriendPlayer.SteamId);
                            }
                            else
                            {
                                Debug.WriteLine("Skipping current player, likely a bad profile");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Error, skipping current friend of friend list. Likely a private profile.");
                        continue;
                    }
                }
            }
        }
    }
}
