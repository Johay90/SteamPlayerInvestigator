using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using SteamWebAPI2.Utilities;
using SteamWebAPI2.Interfaces;
using System.Net.Http;
using Steam.Models.SteamCommunity;
using System.Numerics;

/*

Mon;
Caching
Input screen
Make UI work with data (output)

Tue--Thu
Alg

*/

// TODO Deal with various errors from calls (401s, 403s, 502 etc)..

namespace SteamPlayerInvestigator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
            else
            {
                con.Close();
                return false;
            }
        }

        private static async Task UpdateUserData(PlayerSummaryModel player)
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
            if (player.AccountCreatedDate <= DateTime.MinValue)
            {
                cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value = DateTime.MinValue;
            }
            else
            {
                cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value = player.AccountCreatedDate;
            }

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
            
            if (await checkVACBan(player.SteamId))
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

        // TODO this doens't really need to be async, considering calling vac ban elsewhere so we can remove async from here
        private static async Task InsertDataAsync(PlayerSummaryModel player)
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
            if (player.AccountCreatedDate <= DateTime.MinValue)
            {
                cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value = DateTime.MinValue;
            }
            else
            {
                cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value = player.AccountCreatedDate;
            }

            // strings
            // null checks
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
            
            if (await checkVACBan(player.SteamId))
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

        // method insert into friends table
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

        private async Task<PlayerSummaryModel> getPlayerInfo(SteamUser steamInterface, ulong steamID)
        {
            ISteamWebResponse<PlayerSummaryModel> playerSummaryResponse = await steamInterface.GetPlayerSummaryAsync(steamID);
            return playerSummaryResponse == null ? null : playerSummaryResponse.Data;
        }
        
        private static async Task<IReadOnlyCollection<FriendModel>> getFriendList(SteamUser steamInterface, ulong steamID)
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

        private static async Task<bool> checkVACBan(ulong steamID)
        {
            string steamAPIKey = "7E7C3A26841681369678AE28CDF62901";
            // factory to be used to generate various web interfaces
            SteamWebInterfaceFactory webInterfaceFactory = new SteamWebInterfaceFactory(steamAPIKey);

            // this will map to the ISteamUser endpoint
            SteamUser steamInterface = webInterfaceFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());
            
            ISteamWebResponse<IReadOnlyCollection<PlayerBansModel>> vacBanResponse = await steamInterface.GetPlayerBansAsync(steamID);
            return vacBanResponse.Data.First().NumberOfVACBans > 0;
        }

        // TODO refactor this
        private async Task AddFriendData(SteamUser steamInterface, ulong steamID)
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
                    }
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            // need to convert steamiD to ulong for steamInterface
            ulong steamID = Convert.ToUInt64(TextBoxSteamID.Text);
            string steamAPIKey = "7E7C3A26841681369678AE28CDF62901";

            // factory to be used to generate various web interfaces
            SteamWebInterfaceFactory webInterfaceFactory = new SteamWebInterfaceFactory(steamAPIKey);

            // this will map to the ISteamUser endpoint
            SteamUser steamInterface = webInterfaceFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());

            PlayerSummaryModel player = null;
            
            if (UserExists(steamID))
            {
                if (UserNeedsUpdate(steamID))
                {
                    player = await getPlayerInfo(steamInterface, steamID);
                    RemoveFriends(steamID);
                    await UpdateUserData(player);
                    Debug.WriteLine("updated steamID: " + player.SteamId);
                    await AddFriendData(steamInterface, steamID);
                }
                else
                {
                    Debug.WriteLine("User is up to date");
                }
            }
            else
            {
                player = await getPlayerInfo(steamInterface, steamID);
                await InsertDataAsync(player);
                Debug.WriteLine("User doesn't exist, inserting. SteamID: " + player.SteamId);
                await AddFriendData(steamInterface, steamID);
            }

            // get primary user with banned accounts friends
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText =
                "SELECT * FROM dbo.users INNER JOIN dbo.friends ON dbo.users.steamid = dbo.friends.friendswith WHERE dbo.users.banstatus = 1";
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);
            con.Open();
            cmd.Connection = con;
            SqlDataReader reader = cmd.ExecuteReader();
            List<string> list = new List<string>();
            while (reader.Read())
            {
                list.Add(reader["friendswith"].ToString());
            }
            con.Close();

        }
    }
}
