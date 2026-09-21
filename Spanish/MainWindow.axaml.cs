using Avalonia.Controls;
using Spanish.Core;

namespace Spanish;

public partial class MainWindow : Window
{
    private readonly LearnLibrary _library = LearnLibrary.LoadFromFile("library.json");
    
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(_library);
    }
}