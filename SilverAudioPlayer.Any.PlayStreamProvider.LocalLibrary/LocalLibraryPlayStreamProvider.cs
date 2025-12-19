using System.ComponentModel;
using System.Composition;
using System.Xml.Serialization;
using Serilog;
using SilverAudioPlayer.Shared;
using SLibIndex;

namespace SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary;

[Export(typeof(IPlayStreamProvider))]
public class LocalLibraryPlayStreamProvider : IPlayStreamProvider, IAmOnceAgainAskingYouForYourMemory
{

    public ObjectToRemember ConfigObject =
        new(Guid.Parse("a829f767-72e9-4337-b44b-01f0d2bc7869"), new LocalLibraryPlayStreamProviderConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => [ConfigObject];
    private static bool RunningIndex = false;
    
  
    public void Use(IPlayStreamProviderListener env)
    {
        var dirs= ((LocalLibraryPlayStreamProviderConfig)ConfigObject.Value).Directories;
        Task.Run(() =>
        {
            RunningIndex = true;
            try
            {
                Indexer i = new();
                i.Index(dirs);
            }
            catch (Exception e)
            {
                Log.Error(e,"error indexing");
                try
                {
                    if (File.Exists(IndexedTrack.ExpectedFileLocation)) File.Delete(IndexedTrack.ExpectedFileLocation);
                }
                catch { }

            }
       
            RunningIndex = false;
        });
        MainWindow mw = new((LocalLibraryPlayStreamProviderConfig)ConfigObject.Value, env);
        mw.Show();
    }

    public string Name => "Local Library";
    public string Description => "Sort and play local files";
    public WrappedStream Icon => new WrappedEmbeddedResourceStream(typeof(LocalLibraryPlayStreamProvider).Assembly,
        "SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary.icon.svg");
    public Version? Version => typeof(LocalLibraryPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => "SilverAudioPlayer.Any.PlayStreamProvider.LocalLibrary\nGPL3.0";
    public List<Tuple<Uri, URLType>>? Links { get; }
}

public class LocalLibraryPlayStreamProviderConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [XmlArray("Directories")]
    [XmlArrayItem("Directory")]
    public List<string> Directories { get; set; } = [];
    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}
