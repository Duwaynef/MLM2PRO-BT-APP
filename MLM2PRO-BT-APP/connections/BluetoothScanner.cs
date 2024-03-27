using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using MLM2PRO_BT_APP.util;
using MLM2PRO_BT_APP.devices;

namespace MLM2PRO_BT_APP.connections
{
    public class BluetoothScanner
    {
        private readonly Guid _serviceUuid = new("DAF9B2A4-E4DB-4BE4-816D-298A050F25CD");
        private readonly BluetoothLEAdvertisementWatcher _watcher;
        private readonly List<ulong> _foundDevices = [];
        private long _lastHeartbeatReceived;

        public BluetoothScanner()
        {
            Logger.Log("BluetoothScanner: initializing");
            _watcher = new BluetoothLEAdvertisementWatcher();
            var advertisementFilter = new BluetoothLEAdvertisementFilter();
            advertisementFilter.Advertisement.ServiceUuids.Add(_serviceUuid);

            

            _watcher.AdvertisementFilter = advertisementFilter;
            _watcher.Received += OnAdvertisementReceived;
            _watcher.Start();
            Logger.Log("BluetoothScanner: started");
        }
        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (_foundDevices.Contains(args.BluetoothAddress))
            {
                if (_lastHeartbeatReceived > DateTimeOffset.Now.ToUnixTimeSeconds() - 5) return;
                _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
                var device = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                if (device.Name.Contains("MLM2-") || device.Name.Contains("BlueZ "))
                {
                    if (DeviceManager.Instance != null)
                    {
                        if (App.SharedVm != null) App.SharedVm.LmRSSI = args.RawSignalStrengthInDBm.ToString();
                        Logger.Log("Device RSSI: " + args.RawSignalStrengthInDBm.ToString());
                    }
                }
            }
            else
            {
                _foundDevices.Add(args.BluetoothAddress);
                Logger.Log($"Device found: BluetoothAddress: {args.BluetoothAddress}, LocalName = {args.Advertisement.LocalName}, RSSI: {args.RawSignalStrengthInDBm}");
                var device = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                //var deviceinfo = await DeviceInformation.CreateFromIdAsync(device.DeviceId);

                //Logger.Log("Getting pairing protection level");
                //var protectionLevel = deviceinfo.Pairing.ProtectionLevel;
                //Logger.Log("Pairing protection level: " + protectionLevel.ToString());
                //Logger.Log("Pairing device");
                //await deviceinfo.Pairing.PairAsync(protectionLevel);
                //await deviceinfo.Pairing.UnpairAsync();
                
                Logger.Log($"Device found: BluetoothAddress: {device.DeviceId}, LocalName = {device.Name}, RSSI: {args.RawSignalStrengthInDBm}");
                //var result = device.DeviceId.Split('-').Last().Replace(":", "").ToUpper();
                //AddBluetoothIdToSettings(device.DeviceId);
            }

        }
        private static void AddBluetoothIdToSettings(string newBluetoothId)
        {
            if (SettingsManager.Instance?.Settings?.LaunchMonitor != null && SettingsManager.Instance.Settings.LaunchMonitor.KnownBluetoothIDs.Contains(newBluetoothId)) return;
            SettingsManager.Instance?.Settings?.LaunchMonitor?.KnownBluetoothIDs.Add(newBluetoothId);
            SettingsManager.Instance?.SaveSettings();
        }
    }
}
