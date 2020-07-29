﻿using AppKit;
using Foundation;
using System.Drawing;
using WizLightUniversal.Core;

namespace WizLightUniversal.macOS
{
    // The preferences provider offered by macOS
    public class MacPreferencesProvider : PreferencesProvider
    {
        // This is the backend to the user preferences API
        private NSUserDefaults defaults;

        public override int HomeID
        {
            get
            {
                return (int) defaults.IntForKey("HomeID");
            }

            set
            {
                defaults.SetInt(value, "HomeID");
            }
        }

        // Constructor
        public MacPreferencesProvider() : base()
        {
            defaults = new NSUserDefaults();
        }
    }
}