using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SATOMI.Pages
{
    public class DICOMServerSettings : INotifyPropertyChanged
    {
        public DICOMServerSettings()
        {
            IpAddress = GetLocalIPAddress();
            LoadSettings();
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private string? _ipaddress;
        public string? IpAddress
        {
            get => _ipaddress;
            set
            {
                if (_ipaddress != value)
                {
                    _ipaddress = value;
                    OnPropertyChanged(nameof(IpAddress));
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
        private string GetLocalIPAddress()
        {
            string ipAddress = "Unknown";
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ipAddress = unicastAddress.Address.ToString();
                            break;
                        }
                    }
                }
            }
            return ipAddress;
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