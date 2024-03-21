using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLM2PRO_BT_APP.connections
{
    public interface BluetoothBaseInterface
    {
        public abstract bool isBluetoothDeviceValid();
        public abstract Task ArmDevice();
        public abstract Task DisarmDevice();
        public abstract Task RestartDeviceWatcher();
        public abstract Task DisconnectAndCleanup();
        public abstract byte[] GetEncryptionKey();
        public abstract Task UnSubAndReSub();
    }
}
