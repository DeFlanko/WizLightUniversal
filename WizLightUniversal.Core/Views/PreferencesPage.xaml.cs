﻿using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace WizLightUniversal.Core.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PreferencesPage : ContentPage
    {
        // Loads the pref page 
        public PreferencesPage()
        {
            InitializeComponent();
            SaveButton.IsEnabled = false;
            if (PreferencesProvider.Default.HomeID > 0)
            {
                HomeIDEntry.Text = PreferencesProvider.Default.HomeID.ToString();
            }
        }

        // Saves the preferences when clicked
        void SaveButton_Clicked(object sender, EventArgs e)
        {
            int homeId = VerifyHomeID();
            if (homeId > 0)
            {
                PreferencesProvider.Default.HomeID = homeId;
            }
        }

        // validate text continuously 
        private void HomeIDEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = VerifyHomeID() > 0;
        }

        // verify the home is useable
        private int VerifyHomeID()
        {
            string text = HomeIDEntry.Text;
            //return text.Length == 6 && int.TryParse(text, out int homeId) ? homeId : 0;
            return int.TryParse(text, out int homeId) ? homeId : 0;
        }
    }
}