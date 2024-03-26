using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig;


namespace MLM2PRO_BT_APP
{
    public partial class AboutPage
    {
        public AboutPage()
        {
            InitializeComponent();
            LoadReadmeMarkdown();
        }

        private void GitHub_Link_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Duwaynef/MLM2PRO-BT-APP",
                UseShellExecute = true
            });
        }
        
        private void LoadReadmeMarkdown()
        {
            string pathToReadme = Path.Combine(Directory.GetCurrentDirectory(), "readme.md");
            if (File.Exists(pathToReadme))
            {
                string markdown = File.ReadAllText(pathToReadme);
                string imgDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "img");
                var imgDirectoryUri = new Uri(imgDirectoryPath);
                string pattern = @"\!\[.*?\]\((https:\/\/ko-fi\.com\/img\/.*?\.svg)\)";
                string replacement = $"![ko-fi]({imgDirectoryUri.AbsoluteUri}/githubbutton_sm.png)";
                markdown = Regex.Replace(markdown, pattern, replacement);
                
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                string htmlContent = Markdown.ToHtml(markdown, pipeline);

                var backgroundColor = ((SolidColorBrush)Application.Current.Resources["MaterialDesignPaper"]).Color;
                var foregroundColor = ((SolidColorBrush)Application.Current.Resources["MaterialDesignBody"]).Color;

                string bgHex = ColorToHex(backgroundColor);
                string fgHex = ColorToHex(foregroundColor);
                
                string css = $$"""
                               
                                   body {
                                       background-color: {{bgHex}};
                                       color: {{fgHex}};
                                       font-family: 'Roboto', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                                   }
                                   a {
                                       color: {{fgHex}};
                                   }
                                       
                               """;
                string html = $"""
                               
                                   <!DOCTYPE html>
                                   <meta http-equiv="X-UA-Compatible" content="IE=edge">
                                   <html>
                                   <head>
                                       <style>{css}</style>
                                   </head>
                                   <body>
                                       {htmlContent}
                                   </body>
                                   </html>
                               """;

                MarkdownWebBrowser.NavigateToString(html);
            }
            else
            {
                MessageBox.Show("readme.md not found.");
            }
        }
        private static string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        
        private void MarkdownWebBrowser_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            if (e.Uri != null && !e.Uri.IsFile && e.Uri.AbsoluteUri != "about:blank")
            {
                e.Cancel = true;
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to open link: {ex.Message}");
                }
            }
        }


    }
}