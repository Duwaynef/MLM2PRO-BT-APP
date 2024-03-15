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
        Application.Current.Dispatcher.Invoke(() =>
        {
            logTextBox?.AppendText(formattedMessage);
        });
        // Log to file
        File.AppendAllText(logFilePath, formattedMessage);
    }
}
