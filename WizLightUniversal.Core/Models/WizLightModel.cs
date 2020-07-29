﻿using System;
using System.Threading;
using OpenWiz;
using System.ComponentModel;
using System.Diagnostics;

namespace WizLightUniversal.Core.Models
{
    public class WizLightModel : INotifyPropertyChanged
    {
        // Setters are speicfically used to issue updates to lights,
        // otherwise the backing variable is changed and an update event
        // is issued.
        // TODO: Use a WizState to back these properties
        public string IP { get; }
        public string MAC { get; }
        public string Name { get; set; }
        public string Version { get; set; }
        public int MinimumTemperature { get; set; }
        public int MaximumTemperature { get; set; }
        private int _red;
        public int Red
        {
            get { return _red; }
            set { if (_red != value) { _red = value; QueueUpdate(UpdateType.Color); } }
        }
        private int _green;
        public int Green
        {
            get { return _green; }
            set { if (_green != value) { _green = value; QueueUpdate(UpdateType.Color); } }
        }
        private int _blue;
        public int Blue
        {
            get { return _blue; }
            set { if (_blue != value) { _blue = value; QueueUpdate(UpdateType.Color); } }
        }
        private int _warm;
        public int WarmWhite
        {
            get { return _warm; }
            set { if (_warm != value) { _warm = value; QueueUpdate(UpdateType.Color); } }
        }
        private int _cool;
        public int CoolWhite
        {
            get { return _cool; }
            set { if (_cool != value) { _cool = value; QueueUpdate(UpdateType.Color); } }
        }
        private int _temp;
        public int Temperature
        {
            get { return _temp; }
            set { if (_temp != value) { _temp = value; QueueUpdate(UpdateType.Temperature); } }
        }
        private int _bright;
        public int Brightness
        {
            get { return _bright; }
            set { if (_bright != value) { _bright = value; QueueUpdate(UpdateType.Control); } }
        }
        private bool _power;
        public bool Power
        {
            get { return _power; }
            set { if (_power != value) { _power = value; QueueUpdate(UpdateType.Control); } }
        }

        // The handle and socket are needed to communicate with a light
        private readonly WizHandle Handle;
        private readonly WizSocket Socket;

        // This action loop runs at a predefined period which commits an update
        // or polls a light's state
        private const int MAX_TICKS_WITHOUT_UPDATE = 10;
        private const int TICK_PERIOD_MS = 100;

        private Timer UpdateTimer;
        private int TicksWithoutUpdate;
        private volatile bool UpdatePending;
        private volatile WizState NextUpdate;

        // This event handler communicates with the controls bound to this model
        public event PropertyChangedEventHandler PropertyChanged;

        // Constructor
        public WizLightModel(WizHandle handle) : this()
        {
            // Initial values for these fields
            IP = handle.Ip.ToString();
            MAC = handle.Mac;

            // enables us to receive updates
            Handle = handle;
            Socket = new WizSocket();
            Socket.Bind();
            Socket.BeginRecieveFrom(Handle, WhenGetState, null);

            // Send config request
            Socket.SendTo(WizState.MakeGetUserConfig(), Handle);
            Socket.SendTo(WizState.MakeGetSystemConfig(), Handle);

            // Start the timer for polling/updating
            NextUpdate = null;
            UpdatePending = false;
            TicksWithoutUpdate = MAX_TICKS_WITHOUT_UPDATE;
            UpdateTimer = new Timer(UpdateTimerCallback, this, 0, TICK_PERIOD_MS);
        }

        // Action loop for the timer
        private void UpdateTimerCallback(object state)
        {
            try
            {
                if (UpdatePending) // Send an update this next call
                {
                    TicksWithoutUpdate = 0;
                    Socket.SendTo(NextUpdate, Handle);
                    Debug.WriteLine($"Model for {Handle.Ip}: Sent update");
                    UpdatePending = false;
                }
                else if (++TicksWithoutUpdate > MAX_TICKS_WITHOUT_UPDATE) // Send a poll request this next call
                {
                    TicksWithoutUpdate = 0;
                    Socket.SendTo(WizState.MakeGetPilot(), Handle);
                    Debug.WriteLine($"Model for {Handle.Ip}: Sent poll request");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Model for {Handle.Ip}: Failed to issue message -- {e.Message}");
                UpdatePending = false;
            }
        }

        // Creates a state when an update needs to be issued
        private enum UpdateType { Color, Temperature, Control };
        private void QueueUpdate(UpdateType type)
        {
            NextUpdate = new WizState() { Method = WizMethod.setPilot, Params = new WizParams() };
            switch(type)
            {
                case UpdateType.Color:
                    NextUpdate.Params.R = _red;
                    NextUpdate.Params.G = _green;
                    NextUpdate.Params.B = _blue;
                    NextUpdate.Params.W = _warm;
                    NextUpdate.Params.C = _cool;
                    break;
                case UpdateType.Temperature:
                    NextUpdate.Params.Temp = _temp;
                    break;
                case UpdateType.Control:
                    NextUpdate.Params.State = _power;
                    if (_power) NextUpdate.Params.Dimming = _bright;
                    break;
            }
            LastPilot = NextUpdate.Params;
            UpdatePending = true;
        }
        public void QueueUpdate(WizState state)
        {
            NextUpdate = state;
            LastPilot = state.Params;
            UpdatePending = true;
        }

        // Called when data is recieved
        private void WhenGetState(IAsyncResult asyncResult)
        {
            try
            {
                WizState state = Socket.EndReceiveFrom(Handle, asyncResult);
                if (state != null)
                {
                    HandleGetError(state.Error);
                    switch (state.Method)
                    {
                        case WizMethod.getPilot:
                            HandleGetPilot(state.Result);
                            break;
                        case WizMethod.syncPilot:
                            HandleGetPilot(state.Params);
                            break;
                        case WizMethod.getUserConfig:
                        case WizMethod.getSystemConfig:
                            HandleGetConfig(state.Result);
                            break;
                    }
                    Socket.BeginRecieveFrom(Handle, WhenGetState, null);
                }
            }
            catch(Exception e)
            {
                Debug.WriteLine($"Model for {Handle.Ip}: Unexpected error\n{e.StackTrace}");
            }
        }

        // Handles the user configuration
        private void HandleGetConfig(WizResult config)
        {
            if (config != null)
            {
                if (config.ExtRange != null)
                {
                    MaximumTemperature = config.ExtRange[1];
                    MinimumTemperature = config.ExtRange[0];
                    Debug.WriteLine($"Model for {Handle.Ip}: Got configuration");
                }
                if (config.ModuleName != null && config.ModuleName.Length > 0)
                {
                    Name = config.ModuleName;
                }
                if (config.FwVersion != null && config.FwVersion.Length > 0)
                {
                    Version = $"v{config.FwVersion}";
                }
            }
        }

        // Handles a pilot (state) update
        private void HandleGetPilot(WizParams pilot)
        {
            TicksWithoutUpdate = 0;
            Debug.WriteLine($"Model for {Handle.Ip}: Got pilot");
            if (pilot != null)
            {
                LastPilot = pilot;
                // update power and brightness
                if (pilot.State.HasValue)
                {
                    _power = pilot.State.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Power"));
                }
                if (pilot.Dimming.HasValue)
                {
                    _bright = pilot.Dimming.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Brightness"));
                }

                // update temp
                if (pilot.Temp.HasValue)
                {
                    _temp = pilot.Temp.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Temperature"));
                }

                // update red
                if (pilot.R.HasValue)
                {
                    _red = pilot.R.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Red"));
                }
                // update green
                if (pilot.G.HasValue)
                {
                    _green = pilot.G.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Green"));
                }
                // update blue
                if (pilot.B.HasValue)
                {
                    _blue = pilot.B.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Blue"));
                }
                // update warm white
                if (pilot.W.HasValue)
                {
                    _warm = pilot.W.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("WarmWhite"));
                }
                // update cool white
                if (pilot.C.HasValue)
                {
                    _cool = pilot.C.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CoolWhite"));
                }
            }
        }

        private void HandleGetError(WizError error)
        {
            if (error != null) Debug.WriteLine($"Model for {Handle.Ip}: Got error: {error.Message}");
        }

        private WizLightModel()
        {
            // Default values
            IP = "Getting IP...";
            MAC = "Getting MAC...";
            Name = "Wiz Light";
            Version = "unknwon version";
            MaximumTemperature = 10000;
            MinimumTemperature = 0;
            _red = _blue = _green = _warm = _cool = _temp = _bright = 0;
            _power = false;
        }

        private WizParams LastPilot;
        private WizParams SavedPilot;
        public void SavePilot()
        {
            if (LastPilot != null)
            {
                SavedPilot = new WizParams();
                SavedPilot.State = LastPilot.State;
                SavedPilot.Dimming = LastPilot.Dimming;
                SavedPilot.R = LastPilot.R;
                SavedPilot.G = LastPilot.G;
                SavedPilot.B = LastPilot.B;
                SavedPilot.W = LastPilot.W;
                SavedPilot.C = LastPilot.C;
                SavedPilot.Temp = LastPilot.Temp;
            }
        }
        public void RestorePilot()
        {
            if (SavedPilot != null)
            {
                QueueUpdate(new WizState() { Method = WizMethod.setPilot, Params = SavedPilot });
            }
        }
    }
}
