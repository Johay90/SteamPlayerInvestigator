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
using Microsoft.ML;

namespace SteamPlayerInvestigator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window{

        private readonly StatusMessageService _statusMessageService = new StatusMessageService();
        private List<WeightedPlayer> _weightedPlayers;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _statusMessageService;
        }

        public void CreateModel()
        {
            // create a connection to the database
            SqlConnection con =
                new SqlConnection(
                    @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\JMumm\OneDrive\Documents\Uni\Y3\Final_hons\Project\SteamPlayerInvestigator\SteamPlayerInvestigator\Database1.mdf;Integrated Security=True");
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = con;
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT * FROM [dbo].[users]";
            con.Open();
            SqlDataReader reader = cmd.ExecuteReader();

            // retrieve the player information from the database and store it in a list
            List<PlayerProfile> profiles = new List<PlayerProfile>();
            while (reader.Read())
            {
                PlayerProfile profile = new PlayerProfile
                {
                    Nickname = reader["personaname"].ToString(),
                    RealName = reader["realname"].ToString(),
                    CityCode = reader["loccityid"].ToString(),
                    CountryCode = reader["loccountrycode"].ToString(),
                    StateCode = reader["locstatecode"].ToString(),
                    PrimaryGroupId = reader["primaryclanid"].ToString(),
                };
                profiles.Add(profile);
            }

            con.Close();

            // create an instance of the MLContext class
            MLContext mlContext = new MLContext();

            // use the mlContext.Data property to create a IDataView object that represents the data you want to use for training
            IDataView dataView = mlContext.Data.LoadFromEnumerable(profiles);

            // use the IDataView object to train the machine learning model
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label", "PrimaryGroupId")
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("Nickname"))
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("RealName"))
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("CityCode"))
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("CountryCode"))
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("StateCode"))
                .Append(mlContext.Transforms.Concatenate("Features",
                    new[] { "Nickname", "RealName", "CityCode", "CountryCode", "StateCode" }))
                .AppendCacheCheckpoint(mlContext)
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "Label"));

            var model = pipeline.Fit(dataView);

            // save the model to a .zip file
            mlContext.Model.Save(model, dataView.Schema, "model.zip");
        }

        /* private async void Button_Click(object sender, RoutedEventArgs e)
         {
             // TODO Move away from static/single instance variables/methods here so that we can reuse button_click
             // TODO also stop our foreach (friend and fof gathering) on button click for reuse
 
             // for buton_click reuse
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
        */

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            ulong steamID = Convert.ToUInt64(TextBoxSteamID.Text);
            string steamAPIKey = Credentials.ApiKey;

            // create an instance of the business logic class
            SteamDataService steamDataService = new SteamDataService(steamAPIKey, _statusMessageService);

            // get the player summary and calculate the scores
            PlayerSummaryModel primaryAccount = await steamDataService.GetPrimaryAccountAsync(steamID);
            _weightedPlayers = await steamDataService.CalculateWeightedScoresAsync(steamID);

            // get player with top score
            PlayerSummaryModel topPlayer = _weightedPlayers.Aggregate((l, r) => l.Score > r.Score ? l : r).Player;
            
            // get top score value
            int topScore = _weightedPlayers.Aggregate((l, r) => l.Score > r.Score ? l : r).Score;

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
            debugWindow.DataContext = _weightedPlayers;
            debugWindow.Show();
        }
    }
    }
