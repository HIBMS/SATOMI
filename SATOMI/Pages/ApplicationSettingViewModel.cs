﻿/*
 * ApplicationSettingPage.cs
 * 
 * Overview:
 * This file defines the DICOMServerSettings class in a .NET MAUI application.
 * It manages the configuration settings for the DICOM server, including the AE Title, Port Number, and Local IP Address.
 * The class allows users to load, modify, and save settings for the DICOM storage server.
 * 
 * Features:
 * - Retrieve and display the local IP address
 * - Load and save DICOM server settings (AE Title and Port Number)
 * - Notify UI of property changes using INotifyPropertyChanged
 * - Provide default values for DICOM settings
 * 
 * Author: s.harada@HIBMS
 */
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SATOMI.Pages
{
    public class DICOMServerSettings : INotifyPropertyChanged
    {
        public DICOMServerSettings()
        {
            IpAddresses = new ObservableCollection<string>(GetLocalIPAddresses());
            LoadSettings();
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private ObservableCollection<string>? ipAddresses;
        public ObservableCollection<string>? IpAddresses
        {
            get => ipAddresses;
            set
            {
                if (ipAddresses != value)
                {
                    ipAddresses = value;
                    OnPropertyChanged(nameof(IpAddresses));
                }
            }
        }
        private string _aetitle = "STORESCP";
        public string AeTitle
        {
            get => _aetitle;
            set
            {
                if (_aetitle != value)
                {
                    _aetitle = value;
                    OnPropertyChanged(nameof(AeTitle));
                }
            }
        }
        private int _portnumber = 4649;
        public int PortNumber
        {
            get => _portnumber;
            set
            {
                if (_portnumber != value)
                {
                    _portnumber = value;
                    OnPropertyChanged(nameof(PortNumber));
                }
            }
        }
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private IEnumerable<string> GetLocalIPAddresses()
        {
            var ipList = new List<string>();

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ipList.Add($"{networkInterface.Name}: {unicastAddress.Address}");
                        }
                    }
                }
            }
            return ipList;
        }
        public void SaveSettings()
        {
            Preferences.Set("SCPAeTitle", AeTitle);
            Preferences.Set("SCPPortNumber", PortNumber.ToString());
        }
        private void LoadSettings()
        {
            AeTitle = Preferences.Get("SCPAeTitle", "");
            PortNumber = int.TryParse(Preferences.Get("SCPPortNumber", ""), out int port) ? port : 4649;
        }
    }
}