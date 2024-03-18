using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

public static class Logger
{
    private static TextBox logTextBox;
    private static string logFilePath = "appLog.log"; // Specify your log file path

    public static void Initialize(TextBox textBox, string filePath = "appLog.log")
    {
        logTextBox = textBox;
        logFilePath = filePath;
        // Ensure directory exists
        var logDirectory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
    }

    public static void Log(string message)
    {
        string formattedMessage = $"{DateTime.Now}: {message}\n";

        // Update the TextBox
        Application.Current.Dispatcher.Invoke(() => logTextBox?.AppendText(formattedMessage));

        // Attempt to log to file with retries
        bool success = false;
        int retryCount = 0;
        int maxRetries = 5; // Maximum number of retries
        int delayBetweenRetries = 500; // Delay in milliseconds

        while (!success && retryCount < maxRetries)
        {
            try
            {
                File.AppendAllText(logFilePath, formattedMessage);
                success = true; // If success, exit loop
            }
            catch (IOException) // Catch exceptions related to file access
            {
                retryCount++;
                Thread.Sleep(delayBetweenRetries); // Wait before retrying
            }
        }

        if (!success)
        {
            // Handle failure after retries, could log to an alternate location or raise a notification
            Logger.Log("Failed to write to log file after retries.");
        }
    }

}
