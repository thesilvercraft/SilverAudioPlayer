using Avalonia.Media.Imaging;
using Jellyfin.Sdk.Generated.Models;
using SilverAudioPlayer.Shared;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public class WrappedDto : INotifyPropertyChanged, IDisposable
{
    public BaseItemDto dto;

    public WrappedDto(BaseItemDto dto, WrappedStream? ws = null)
    {
        this.dto = dto;
        using var s = ws?.GetStream();
        if (s != null)
        {
            Cover = Bitmap.DecodeToHeight(s, 200);
        }
    }

    public string Name => dto.Name;
    public string AlbumArtist => dto.AlbumArtist;
    public bool? IsFolder => dto.IsFolder;
    public int? IndexNumber => dto.IndexNumber;

    private Bitmap? _cover;
    public Bitmap? Cover
    {
        get => _cover;
        set
        {
            if (!Equals(_cover, value))
            {
                _cover?.Dispose(); 
                
                _cover = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cover?.Dispose();
            _cover = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~WrappedDto()
    {
        Dispose(false);
    }
}