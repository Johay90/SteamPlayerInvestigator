using System;
using System.Data;
using System.Net;
using System.Windows;
using System.Text.Json;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Diagnostics;

/*

Sun:
Add to DB
Setup innerjoins

Mon;
Caching
Iutpuut screen
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

        private static void InsertData(Player player)
        {
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText =
                "INSERT dbo.users (steamid, communityvisibilitystate, profilestate, personaname, profileurl, avatar, avatarmedium, avatarfull, avatarhash, personastate, realname, primaryclanid, timecreated, personastateflags, loccountrycode, locstatecode, loccityid) VALUES (@steamid, @communityvisibilitystate, @profilestate, @personaname, @profileurl, @avatar, @avatarmedium, @avatarfull, @avatarhash, @personastate, @realname, @primaryclanid, @timecreated, @personastateflags, @loccountrycode, @locstatecode, @loccityid)";

            // ints (don't need null checks, will return 0 or 1)
            cmd.Parameters.AddWithValue("@steamid", player.steamid);
            cmd.Parameters.AddWithValue("@communityvisibilitystate", player.communityvisibilitystate);
            cmd.Parameters.AddWithValue("@profilestate", player.profilestate);
            cmd.Parameters.AddWithValue("@personastate", player.personastate);
            cmd.Parameters.AddWithValue("@timecreated", player.timecreated);
            cmd.Parameters.AddWithValue("@personastateflags", player.personastateflags);
            cmd.Parameters.AddWithValue("@loccityid", player.loccityid);

            // null checks on strings/varchars
            if (player.personaname != null)
            {
                cmd.Parameters.AddWithValue("@personaname", player.personaname);
            }
            else
            {
                cmd.Parameters.AddWithValue("@personaname", DBNull.Value);
            }

            if (player.profileurl != null)
            {
                cmd.Parameters.AddWithValue("@profileurl", player.avatar);
            }
            else
            {
                cmd.Parameters.AddWithValue("@profileurl", DBNull.Value);
            }

            if (player.avatar != null)
            {
                cmd.Parameters.AddWithValue("@avatar", player.avatar);
            }
            else
            {
                cmd.Parameters.AddWithValue("@avatar", DBNull.Value);
            }

            if (player.avatarmedium != null)
            {
                cmd.Parameters.AddWithValue("@avatarmedium", player.avatarmedium);
            }
            else
            {
                cmd.Parameters.AddWithValue("@avatarmedium", DBNull.Value);
            }

            if (player.avatarfull != null)
            {
                cmd.Parameters.AddWithValue("@avatarfull", player.avatarfull);
            }
            else
            {
                cmd.Parameters.AddWithValue("@avatarfull", DBNull.Value);
            }

            if (player.avatarhash != null)
            {
                cmd.Parameters.AddWithValue("@avatarhash", player.avatarhash);
            }
            else
            {
                cmd.Parameters.AddWithValue("@avatarhash", DBNull.Value);
            }

            if (player.realname != null)
            {
                cmd.Parameters.AddWithValue("@realname", player.realname);
            }
            else
            {
                cmd.Parameters.AddWithValue("@realname", DBNull.Value);
            }

            if (player.primaryclanid != null)
            {
                cmd.Parameters.AddWithValue("@primaryclanid", player.primaryclanid);
            }
            else
            {
                cmd.Parameters.AddWithValue("@primaryclanid", DBNull.Value);
            }

            if (player.loccountrycode != null)
            {
                cmd.Parameters.AddWithValue("@loccountrycode", player.loccountrycode);
            }
            else
            {
                cmd.Parameters.AddWithValue("@loccountrycode", DBNull.Value);
            }

            if (player.locstatecode != null)
            {
                cmd.Parameters.AddWithValue("@locstatecode", player.locstatecode);
            }
            else
            {
                cmd.Parameters.AddWithValue("@locstatecode", DBNull.Value);
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
            string steamID = TextBoxSteamID.Text;
            string steamAPIKey = "7E7C3A26841681369678AE28CDF62901";

            Debug.WriteLine("Starting API calls");

            // get player data
            Player player = await playerAPICall(steamAPIKey, steamID);

            Debug.WriteLine("Got player data");

            // get friend list
            FriendData friendDeserialized = await friendAPICall(steamAPIKey, steamID);

            Debug.WriteLine("Got friend list");

            InsertData(player);

            // ReSharper disable once InvertIf
            if (friendDeserialized.friendslist.friends.Count > 0)
            {
                Debug.WriteLine("Starting friend loop");
                
                foreach (Friend friend in friendDeserialized.friendslist.friends)
                {
                    Player friendPlayer = await playerAPICall(steamAPIKey, friend.steamid);
                    InsertData(friendPlayer);
                }
            }
            Debug.WriteLine("Finished");
        }
    }
}
