﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;

/*
Thu: 
- Make UI work with data (Input)
- Add Database (in VS)

Fri
- Insert into DB
- Get Friends data, add to db, seutp innerjoins

Sat
- Insert Friends data into DB and setup inner joins

Sun--Mon
- Caching
- Make Output Screen
- Make UI work with data (Output screen)

Tue--Thu
Alg

*/

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string steamID = TextBoxSteamID.Text;
            string url = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=7E7C3A26841681369678AE28CDF62901&steamids=" + steamID;
            using (var wb = new WebClient())
            {
                var response = wb.DownloadString(url);
                Root myDeserializedClass = JsonSerializer.Deserialize<Root>(response);
                System.Diagnostics.Debug.WriteLine("Display Name:" + myDeserializedClass.response.players[0].personaname);
            }
        }
    }
}
