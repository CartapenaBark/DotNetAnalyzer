using System.Collections.ObjectModel;
using System.Windows.Input;

namespace WpfApp;

public class MainWindowViewModel
{
    public string Title { get; set; } = "Test Application";

    public string Subtitle { get; set; } = "WPF Test Assets for DotNetAnalyzer";

    public ObservableCollection<ItemModel> Items { get; set; } = [];

    public ItemModel? SelectedItem { get; set; }

    public ICommand? AddCommand { get; set; }

    public ICommand? RemoveCommand { get; set; }

    public ICommand? RefreshCommand { get; set; }
}

public class ItemModel
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
