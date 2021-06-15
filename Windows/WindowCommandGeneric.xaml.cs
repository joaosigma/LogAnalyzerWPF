using System.Text;
using System.Text.Json;

namespace LogAnalyzerWPF.Windows
{
    /// <summary>
    /// Interaction logic for WindowCommandGeneric.xaml
    /// </summary>
    public partial class WindowCommandGeneric : AdonisUI.Controls.AdonisWindow
    {
        public WindowCommandGeneric(string contentJson)
        {
            InitializeComponent();

            PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); }; //shortcut to close the window

            try
            {
                var jsonDoc = JsonDocument.Parse(contentJson);

                using (var stream = new System.IO.MemoryStream())
                {
                    var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                    jsonDoc.WriteTo(writer);
                    writer.Flush();

                    mTextBlock.Text = Encoding.UTF8.GetString(stream.ToArray());
                }
            }
            catch (JsonException)
            {
                mTextBlock.Text = contentJson;
            }
        }
    }
}
