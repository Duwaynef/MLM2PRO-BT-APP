namespace MLM2PRO_BT_APP.connections
{
    public interface IBluetoothBaseInterface
    {
        public bool IsBluetoothDeviceValid();
        public Task ArmDevice();
        public byte[]? ConvertAuthRequest(byte[]? input);
        public Task DisarmDevice();
        public Task RestartDeviceWatcher();
        public Task DisconnectAndCleanup();
        public byte[]? GetEncryptionKey();
        public Task UnSubAndReSub();
    }
}
