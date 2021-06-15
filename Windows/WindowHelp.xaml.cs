using System.Collections.Generic;

namespace LogAnalyzerWPF.Windows
{
    /// <summary>
    /// Interaction logic for WindowHelp.xaml
    /// </summary>
    public partial class WindowHelp : AdonisUI.Controls.AdonisWindow
    {
        public WindowHelp()
        {
            InitializeComponent();

            PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); }; //shortcut to close the window
        }
    }
}
