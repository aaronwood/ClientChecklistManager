using System.Windows;

namespace ClientChecklistManager.Views;

public partial class SaveTemplateDialog : Window
{
    public SaveTemplateDialog()
    {
        InitializeComponent();
        TemplateNameBox.Focus();
    }

    public string TemplateName => TemplateNameBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TemplateNameBox.Text))
        {
            MessageBox.Show("Please enter a template name.", "Name Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
