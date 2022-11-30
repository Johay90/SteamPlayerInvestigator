using System;
using System.Collections.Generic;
using System.Windows;
using System.Diagnostics;
using System.Linq;
using SteamWebAPI2.Utilities;
using SteamWebAPI2.Interfaces;
using System.Net.Http;
using Steam.Models.SteamCommunity;

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

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            // need to convert steamiD to ulong for steamInterface
            ulong steamID = Convert.ToUInt64(TextBoxSteamID.Text);
            string steamAPIKey = "7E7C3A26841681369678AE28CDF62901";

            // factory to be used to generate various web interfaces
            SteamWebInterfaceFactory webInterfaceFactory = new SteamWebInterfaceFactory(steamAPIKey);

            // this will map to the ISteamUser endpoint
            SteamUser steamInterface = webInterfaceFactory.CreateSteamWebInterface<SteamUser>(new HttpClient());

            PlayerSummaryModel player;

            if (App.UserExists(steamID)) 
            {
                if (App.UserNeedsUpdate(steamID))
                {
                    player = await App.getPlayerInfo(steamInterface, steamID);
                    App.RemoveFriends(steamID);
                    await App.UpdateUserData(player);
                    Debug.WriteLine("updated steamID: " + player.SteamId);
                    await App.AddFriendData(steamInterface, steamID);
                }
                else
                {
                    player = await App.getPlayerInfo(steamInterface, steamID);
                    Debug.WriteLine("User is up to date");
                }
            }
            else
            {
                player = await App.getPlayerInfo(steamInterface, steamID);
                await App.InsertDataAsync(player);
                Debug.WriteLine("User doesn't exist, inserting. SteamID: " + player.SteamId);
                await App.AddFriendData(steamInterface, steamID);
            }

            PlayerSummaryModel primaryAccount = player;
            List<PlayerSummaryModel> players = await App.AvailableAccounts(player.SteamId);
            Dictionary<PlayerSummaryModel, int> weightedPlayers = App.CalculateWeightedScores(players, primaryAccount);
            
            // get player with top score
            PlayerSummaryModel topPlayer = weightedPlayers.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;

            // output window
            output outputWindow = new output();
            outputWindow.Show();
            if (primaryAccount != null) outputWindow.labelPrimaryAccount.Content = primaryAccount.Nickname;
            outputWindow.labelSecondaryAccount.Content = topPlayer.Nickname;
            outputWindow.labelPercMsg.Content = "TODO";


        }


    }
    }
