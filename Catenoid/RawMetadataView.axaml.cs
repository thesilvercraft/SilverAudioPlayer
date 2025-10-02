using System.Text.Json;
using Avalonia.Controls;
using AvaloniaEdit;

namespace Catenoid;

public partial class RawMetadataView : Window
{
    public RawMetadataView()
    {
        InitializeComponent();
    }
    JsonSerializerOptions options = new()
    {
        WriteIndented = true
    };
    public RawMetadataView(SilverAudioPlayer.Shared.Song currentSong) : this()
    {
        var textBox = this.FindControl<TextEditor>("TextBox");
        textBox.Text = System.Text.Json.JsonSerializer.Serialize(currentSong,options) ?? "No Raw Metadata";
    }
}