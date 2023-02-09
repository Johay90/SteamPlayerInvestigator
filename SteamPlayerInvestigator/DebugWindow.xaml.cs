using SteamPlayerInvestigator.Classes;
using System.Collections.Generic;
using System.Windows;
using System;

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
    }
}
