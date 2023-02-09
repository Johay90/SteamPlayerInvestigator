using SteamPlayerInvestigator.Classes;
using System.Collections.Generic;
using System.Windows;
using System;
using System.Numerics;
using System.Reflection;
using System.Windows.Controls;
using Steam.Models.SteamCommunity;

namespace SteamPlayerInvestigator
{
    /// <summary>
    /// Interaction logic for DebugWindow.xaml
    /// </summary>
    public partial class DebugWindow : Window
    {

        public DebugWindow()
        {
            InitializeComponent();
            this.Loaded += DebugWindow_Loaded;
        }

        private void DebugWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.debugListBox.DataContext = this.DataContext;
        }

        /*private void debugListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            selectedItemInfo.ItemsSource = new List<WeightedPlayer> { (WeightedPlayer)debugListBox.SelectedItem };
        }
        */

        private void debugListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            WeightedPlayer selectedItem = (WeightedPlayer)debugListBox.SelectedItem;

            if (selectedItem != null)
            {
                selectedItemInfo.Items.Clear();

                PropertyInfo[] properties = selectedItem.GetType().GetProperties();

                foreach (PropertyInfo property in properties)
                {
                    StackPanel panel = new StackPanel();

                    TextBlock propertyName = new TextBlock
                    {
                        Text = property.Name + ":",
                        FontWeight = FontWeights.Bold
                    };
                    panel.Children.Add(propertyName);

                    object propertyValue = property.GetValue(selectedItem);

                    if (propertyValue != null && propertyValue.GetType() == typeof(PlayerSummaryModel))
                    {
                        PropertyInfo[] playerProperties = propertyValue.GetType().GetProperties();
                        foreach (PropertyInfo playerProperty in playerProperties)
                        {
                            TextBlock playerPropertyName = new TextBlock
                            {
                                Text = playerProperty.Name + ":",
                                FontWeight = FontWeights.Bold
                            };
                            panel.Children.Add(playerPropertyName);

                            object playerPropertyValue = playerProperty.GetValue(propertyValue);
                            TextBlock playerPropertyValueText = new TextBlock
                            {
                                Text = playerPropertyValue != null ? playerPropertyValue.ToString() : "null"
                            };
                            panel.Children.Add(playerPropertyValueText);
                        }
                    }
                    else
                    {
                        TextBlock propertyValueText = new TextBlock
                        {
                            Text = propertyValue != null ? propertyValue.ToString() : "null"
                        };
                        panel.Children.Add(propertyValueText);
                    }

                    selectedItemInfo.Items.Add(panel);
                }
            }
        }
    }
}
