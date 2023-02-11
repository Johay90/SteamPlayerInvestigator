using Steam.Models.SteamCommunity;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace SteamPlayerInvestigator.Classes
{
    public class Database
    {
        private readonly SqlConnection _connection;
        private readonly SteamDataService _steamDataService;

        public Database(string connectionString, SteamDataService steamDataService)
        {
            _connection = new SqlConnection(connectionString);
            _steamDataService = steamDataService;
        }

        public async Task InsertDataAsync(PlayerSummaryModel player)
        {
            await using SqlCommand cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText =
                    "INSERT INTO dbo.users (steamid, communityvisibilitystate, profilestate, personaname, profileurl, avatar, avatarmedium, avatarfull, personastate, realname, primaryclanid, timecreated, loccountrycode, locstatecode, loccityid, banstatus, date_added) " +
                    "VALUES (@steamid, @communityvisibilitystate, @profilestate, @personaname, @profileurl, @avatar, @avatarmedium, @avatarfull, @personastate, @realname, @primaryclanid, @timecreated, @loccountrycode, @locstatecode, @loccityid, @banstatus, @date_added)"
            };

            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(player.SteamId);
            cmd.Parameters.Add("@profilestate", SqlDbType.Int).Value = Convert.ToInt32(player.ProfileState);
            cmd.Parameters.Add("@loccityid", SqlDbType.Int).Value = Convert.ToInt32(player.CityCode);
            cmd.Parameters.Add("@date_added", SqlDbType.DateTime2).Value = DateTime.Now;
            cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value =
                player.AccountCreatedDate > DateTime.MinValue ? player.AccountCreatedDate : DateTime.MinValue;
            cmd.Parameters.Add("@communityvisibilitystate", SqlDbType.Int).Value = player.ProfileVisibility;
            cmd.Parameters.Add("@personastate", SqlDbType.Int).Value = player.UserStatus;
            cmd.Parameters.Add("@personaname", SqlDbType.NVarChar).Value = player.Nickname ?? (object)DBNull.Value;
            cmd.Parameters.Add("@profileurl", SqlDbType.NVarChar).Value = player.ProfileUrl ?? (object)DBNull.Value;
            cmd.Parameters.Add("@avatar", SqlDbType.NVarChar).Value = player.AvatarUrl ?? (object)DBNull.Value;
            cmd.Parameters.Add("@avatarmedium", SqlDbType.NVarChar).Value =
                player.AvatarMediumUrl ?? (object)DBNull.Value;
            cmd.Parameters.Add("@avatarfull", SqlDbType.NVarChar).Value = player.AvatarFullUrl ?? (object)DBNull.Value;
            cmd.Parameters.Add("@realname", SqlDbType.NVarChar).Value = player.RealName ?? (object)DBNull.Value;
            cmd.Parameters.Add("@primaryclanid", SqlDbType.BigInt).Value =
                player.PrimaryGroupId ?? (object)DBNull.Value;
            cmd.Parameters.Add("@loccountrycode", SqlDbType.NChar).Value = player.CountryCode ?? (object)DBNull.Value;
            cmd.Parameters.Add("@locstatecode", SqlDbType.NChar).Value = player.StateCode ?? (object)DBNull.Value;
            cmd.Parameters.AddWithValue("@banstatus", await _steamDataService.IsVacBannedAsync(player.SteamId) ? 1 : 0);

            cmd.Connection = _connection;

            try
            {
                if (_connection.State == ConnectionState.Closed)
                {
                    await _connection.OpenAsync();
                }

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("An error occurred in InsertDataAsync: " + ex.Message);
            }
            finally
            {
                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
            }
        }

        public async Task UpdateUserData(PlayerSummaryModel player)
        {

            try
            {
                await using SqlConnection con = new SqlConnection(_connection.ConnectionString);

                SqlCommand cmd = new SqlCommand
                {
                    CommandType = CommandType.Text,
                    CommandText = @"
            UPDATE users 
            SET 
                steamid = @steamid, 
                personaname = @personaname, 
                profileurl = @profileurl, 
                avatar = @avatar, 
                avatarmedium = @avatarmedium, 
                avatarfull = @avatarfull, 
                personastate = @personastate, 
                realname = @realname, 
                primaryclanid = @primaryclanid, 
                timecreated = @timecreated, 
                loccountrycode = @loccountrycode, 
                locstatecode = @locstatecode, 
                loccityid = @loccityid, 
                date_added = @date_added, 
                banstatus = @banstatus 
            WHERE steamid = @steamid"
                };

                cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(player.SteamId);
                cmd.Parameters.Add("@loccityid", SqlDbType.Int).Value = Convert.ToInt32(player.CityCode);
                cmd.Parameters.Add("@timecreated", SqlDbType.DateTime2).Value =
                    player.AccountCreatedDate.Date >= DateTime.MinValue
                        ? player.AccountCreatedDate
                        : DateTime.MinValue;
                cmd.Parameters.AddWithValue("@date_added", DateTime.Now);
                cmd.Parameters.AddWithValue("@personastate", player.UserStatus);
                cmd.Parameters.AddWithValue("@personaname", player.Nickname);
                cmd.Parameters.AddWithValue("@profileurl", player.ProfileUrl);
                cmd.Parameters.AddWithValue("@avatar", player.AvatarUrl);
                cmd.Parameters.AddWithValue("@avatarmedium", player.AvatarMediumUrl);
                cmd.Parameters.AddWithValue("@avatarfull", player.AvatarFullUrl);
                cmd.Parameters.AddWithValue("@realname", player.RealName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@primaryclanid", player.PrimaryGroupId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@loccountrycode", player.CountryCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@locstatecode", player.StateCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@banstatus",
                    await _steamDataService.IsVacBannedAsync(player.SteamId) ? 1 : 0);

                cmd.Connection = con;
                con.Open();
                cmd.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                Debug.WriteLine("An error occurred while executing the query (UpdateUserData): " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("An error occurred (UpdateUserData): " + ex.Message);
            }
        }

        public bool UserNeedsUpdate(ulong steamID)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText =
                "SELECT * FROM users WHERE SteamID = @SteamID AND date_added < DATEADD(day, -7, GETDATE())";
            cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);
            cmd.Connection = _connection;
            try
            {
                _connection.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    return reader.HasRows;
                }
            }
            catch (SqlException ex)
            {
                Debug.WriteLine("An error occurred while checking UserNeedsUpdate(): " + ex.Message);
                return false;
            }
            finally
            {
                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
            }
        }

        public bool UserExists(ulong steamID)
        {
            using SqlConnection con = new SqlConnection(_connection.ConnectionString);
            try
            {
                con.Open();
                using SqlCommand cmd = new SqlCommand("SELECT * FROM users WHERE steamid = @SteamID", con);
                cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);
                using SqlDataReader reader = cmd.ExecuteReader();
                return reader.HasRows;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UserExists()" + ex.Message);
                return false;
            }
        }

        public void RemoveFriends(ulong steamid)
        {
            // Get ids from friends for removing 
            List<long> friends = GetFriendIds(steamid);

            // Remove from users table
            RemoveUsers(friends);

            // Remove from friends table
            RemoveFriend(steamid);
        }
        
        public async Task<List<PlayerSummaryModel>> AvailableAccounts(ulong steamID)
        {
            try
            {
                await using SqlConnection connection = new SqlConnection(_connection.ConnectionString);
                await connection.OpenAsync();
                string sql = @"SELECT * 
                   FROM dbo.users 
                   INNER JOIN dbo.friends ON dbo.friends.friendswith = dbo.users.steamid 
                   WHERE dbo.users.banstatus = 1 AND dbo.friends.steamid = @steamid";
                await using SqlCommand command = new SqlCommand(sql, connection);
                command.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);

                return (await command.ExecuteReaderAsync())
                    .Cast<IDataRecord>()
                    .Select(MapDataRecordToPlayerSummary)
                    .Where(p => p.SteamId != steamID)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.WriteLine("An error occurred (AvailableAccounts): " + e.Message);
                throw;
            }
        }

        public async Task InsertFriendsAsync(ulong steamID, ulong friendID)
        {
            try
            {
                await using SqlConnection con = new SqlConnection(_connection.ConnectionString);
                await con.OpenAsync();
                await using SqlCommand cmd = new SqlCommand("INSERT dbo.friends (steamid, friendswith) VALUES (@steamid, @friendswith)", con);
                cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamID);
                cmd.Parameters.Add("@friendswith", SqlDbType.BigInt).Value = Convert.ToInt64(friendID);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InsertFriendsAsync method threw an exception: " + ex.Message);
                throw;
            }
        }

        private PlayerSummaryModel MapDataRecordToPlayerSummary(IDataRecord record)
        {
            return new PlayerSummaryModel
            {
                SteamId = Convert.ToUInt64(record["steamid"]),
                ProfileUrl = record["profileurl"].ToString(),
                AvatarUrl = record["avatar"].ToString(),
                AvatarMediumUrl = record["avatarmedium"].ToString(),
                AvatarFullUrl = record["avatarfull"].ToString(),
                RealName = record["realname"].ToString(),
                PrimaryGroupId = record["primaryclanid"].ToString(),
                AccountCreatedDate = Convert.ToDateTime(record["timecreated"]),
                UserStatus = (UserStatus)Convert.ToInt32(record["personastate"]),
                ProfileVisibility = (ProfileVisibility)Convert.ToInt32(record["communityvisibilitystate"]),
                Nickname = record["personaname"].ToString(),
                ProfileState = (uint)Convert.ToInt32(record["profilestate"]),
                CountryCode = record["loccountrycode"].ToString(),
                StateCode = record["locstatecode"].ToString(),
                CityCode = Convert.ToUInt32(record["loccityid"].ToString()),
            };
        }

        private List<long> GetFriendIds(ulong steamid)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(_connection.ConnectionString))
                {
                    con.Open();
                    SqlCommand cmd = new SqlCommand("SELECT friendswith FROM friends WHERE steamid = @steamid", con);
                    cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamid);
                    SqlDataReader reader = cmd.ExecuteReader();
                    List<long> friends = new List<long>();
                    while (reader.Read())
                    {
                        friends.Add((long)reader["friendswith"]);
                    }

                    return friends;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Method name: GetFriendIds");
                Debug.WriteLine("An error occured: " + ex.Message);
                return null;
            }
        }

        private void RemoveUsers(List<long> friends)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(_connection.ConnectionString))
                {
                    con.Open();
                    foreach (long friend in friends)
                    {
                        SqlCommand cmd = new SqlCommand("DELETE FROM users WHERE steamid = @friendId", con);
                        cmd.Parameters.Add("@friendId", SqlDbType.BigInt).Value = friend;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in RemoveUsers method: " + ex.Message);
                throw;
            }
        }

        private void RemoveFriend(ulong steamid)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(_connection.ConnectionString))
                {
                    con.Open();
                    SqlCommand cmd = new SqlCommand("DELETE FROM friends WHERE steamid = @steamid", con);
                    cmd.Parameters.Add("@steamid", SqlDbType.BigInt).Value = Convert.ToInt64(steamid);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RemoveFriend method encountered an error: " + ex.Message);
            }
        }
    }
}