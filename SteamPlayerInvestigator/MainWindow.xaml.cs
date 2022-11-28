using System;
using System.Data;
using System.Net;
using System.Windows;
using System.Text.Json;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Diagnostics;
using SteamWebAPI2.Utilities;
using SteamWebAPI2.Interfaces;
using System.Net.Http;
using Steam.Models.SteamCommunity;

/*

Mon;

Make friends DB
Insert friends into DB
Setup innerjoins (to check for banned accounts)

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

        private static void InsertData(PlayerSummaryModel player)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText =
                "INSERT dbo.users (steamid, communityvisibilitystate, profilestate, personaname, profileurl, avatar, avatarmedium, avatarfull, personastate, realname, primaryclanid, timecreated, loccountrycode, locstatecode, loccityid) VALUES (@steamid, @communityvisibilitystate, @profilestate, @personaname, @profileurl, @avatar, @avatarmedium, @avatarfull, @personastate, @realname, @primaryclanid, @timecreated, @loccountrycode, @locstatecode, @loccityid)";

            // ints (don't need null checks)
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(player.SteamId);
            cmd.Parameters.Add("@profilestate", SqlDbType.Int).Value = Convert.ToInt32(player.ProfileState);
            cmd.Parameters.Add("@loccityid", SqlDbType.Int).Value = Convert.ToInt32(player.CityCode);
            cmd.Parameters.AddWithValue("@timecreated", player.AccountCreatedDate);

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

            cmd.Connection = con;
            con.Open();
            cmd.ExecuteNonQuery();
            con.Close();
        }

        private async Task<Player> playerAPICall(string steamAPIKey, string steamID)
        {
            using WebClient wb = new WebClient();
            string playerUrl = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=" + steamAPIKey +
                               "&steamids=" + steamID;
            string playerResponse = await wb.DownloadStringTaskAsync(playerUrl);
            PlayerData playerDeserialized = JsonSerializer.Deserialize<PlayerData>(playerResponse);
            Player player = playerDeserialized.response.players[0];
            return player;
        }
        
        private async Task<FriendData> friendAPICall(string steamAPIKey, string steamID)
        {
            using WebClient wb = new WebClient();
            string freindsUrl = "http://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key=" + steamAPIKey +
                                "&steamid=" + steamID + "&relationship=friend";
            string friendResponse = await wb.DownloadStringTaskAsync(freindsUrl);
            FriendData friendDeserialized = JsonSerializer.Deserialize<FriendData>(friendResponse);
            return friendDeserialized;
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


            ISteamWebResponse<PlayerSummaryModel> playerSummaryResponse = await steamInterface.GetPlayerSummaryAsync(steamID);
            PlayerSummaryModel playerSummaryData = playerSummaryResponse.Data;
            InsertData(playerSummaryData);

            Debug.WriteLine("Added player data.");


            /*

            Debug.WriteLine("Starting API calls");

            
            Player player = await playerAPICall(steamAPIKey, steamID);
            InsertData(player);
            Debug.WriteLine("Primary Player data inserted into database. SteamID: " + player.steamid);
            
            FriendData friendDeserialized = await friendAPICall(steamAPIKey, steamID);

            // ReSharper disable once InvertIf
            if (friendDeserialized.friendslist.friends.Count > 0)
            {
                Debug.WriteLine("Starting friend loop");

                // get friend data
                foreach (Friend friend in friendDeserialized.friendslist.friends)
                {
                    Player friendPlayer = await playerAPICall(steamAPIKey, friend.steamid);
                    InsertData(friendPlayer);
                    Debug.WriteLine("Friend data inserted into database. SteamID: " + friendPlayer.steamid);
                }

                // get friends of friends
                Debug.WriteLine("Starting friends of friends loop");
                foreach (Friend friend in friendDeserialized.friendslist.friends)
                {
                    FriendData friendOfFriendDeserialized = await friendAPICall(steamAPIKey, friend.steamid);
                    foreach (Friend friendOfFriend in friendOfFriendDeserialized.friendslist.friends)
                    {
                        Player friendOfFriendPlayer = await playerAPICall(steamAPIKey, friendOfFriend.steamid);
                        InsertData(friendOfFriendPlayer);
                        Debug.WriteLine("Friend of friend data inserted into database. SteamID: " + friendOfFriendPlayer.steamid);
                    }
                }
            }
            Debug.WriteLine("Finished");

            */
        }
    }
}
