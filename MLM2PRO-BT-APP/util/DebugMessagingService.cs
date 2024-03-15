using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLM2PRO_BT_APP.util
{
    public static class DebugMessageService
    {
        public static event EventHandler<string> OnMessageReceived;

        public static void SendMessage(string message)
        {
            OnMessageReceived?.Invoke(null, message);
        }
    }
}
