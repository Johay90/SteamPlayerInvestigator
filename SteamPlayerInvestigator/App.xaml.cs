using System.Collections.Generic;
using System.Windows;

namespace SteamPlayerInvestigator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
    }

    public class Player
        {
            public string steamid { get; set; }
            public int communityvisibilitystate { get; set; }
            public int profilestate { get; set; }
            public string personaname { get; set; }
            public string profileurl { get; set; }
            public string avatar { get; set; }
            public string avatarmedium { get; set; }
            public string avatarfull { get; set; }
            public string avatarhash { get; set; }
            public int personastate { get; set; }
            public string realname { get; set; }
            public string primaryclanid { get; set; }
            public int timecreated { get; set; }
            public int personastateflags { get; set; }
            public string loccountrycode { get; set; }
            public string locstatecode { get; set; }
            public int loccityid { get; set; }
        }

        public class Response
        {
            public List<Player> players { get; set; }
        }

        public class PlayerData
        {
            public Response response { get; set; }
        }

        public class Friend
        {
            public string steamid { get; set; }
            public string relationship { get; set; }
            public int friend_since { get; set; }
        }

        public class Friendslist
        {
            public List<Friend> friends { get; set; }
        }

        public class FriendData
        {
            public Friendslist friendslist { get; set; }
        }
}
