namespace MLM2PRO_BT_APP.util
{
    public class EventAggregator
    {
        public static EventAggregator Instance { get; } = new();
        public event Action<string, int>? SnackBarMessagePublished;
        public void PublishSnackBarMessage(string message, int duration = 2)
        {
            SnackBarMessagePublished?.Invoke(message, duration);
        }
    }
}
