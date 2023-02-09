using Steam.Models.SteamCommunity;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace SteamPlayerInvestigator.Classes
{
    public class Database
    {
        private readonly SqlConnection _connection;
        private readonly MainWindow mainWindow;

        public Database(string connectionString, MainWindow mainWindow)
        {
            _connection = new SqlConnection(connectionString);
            this.mainWindow = mainWindow;
        }

        public async Task InsertDataAsync(PlayerSummaryModel player)
        {
            SqlCommand cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText =
                    "INSERT dbo.users (steamid, communityvisibilitystate, profilestate, personaname, profileurl, avatar, avatarmedium, avatarfull, personastate, realname, primaryclanid, timecreated, loccountrycode, locstatecode, loccityid, banstatus, date_added) VALUES (@steamid, @communityvisibilitystate, @profilestate, @personaname, @profileurl, @avatar, @avatarmedium, @avatarfull, @personastate, @realname, @primaryclanid, @timecreated, @loccountrycode, @locstatecode, @loccityid, @banstatus, @date_added)"
            };

            // ints (don't need null checks)
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

            await Task.Delay(500);
            if (await mainWindow.CheckVacBan(player.SteamId))
            {
                cmd.Parameters.AddWithValue("@banstatus", 1);
            }
            else
            {
                cmd.Parameters.AddWithValue("@banstatus", 0);
            }

            cmd.Connection = _connection;
            await _connection.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            _connection.Close();
        }
    }
}
