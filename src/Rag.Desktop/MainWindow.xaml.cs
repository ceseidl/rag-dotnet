using System.Windows;
using Microsoft.Win32;
using Rag.Desktop.Services;
using Rag.Desktop.ViewModels;

namespace Rag.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new(RagHost.Build);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void PickFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pasta com os documentos (.md e .txt)"
        };
        if (dialog.ShowDialog(this) == true)
            _vm.Folder = dialog.FolderName;
    }
}
