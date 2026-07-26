using Serilog;
using SilverAudioPlayer.Shared;
using System.ComponentModel;
using System.Composition;

namespace SilverAudioPlayer.Any.MemoryProvider.SilverConfigMemoryProvider
{
    public record RememberedConfig(ObjectToRemember obj, object configReader, string location, Type genericType);

    [Export(typeof(IWillProvideMemory))]
    public class SilverConfigMemoryProvider : IWillProvideMemory, IWillTellYouWhereIStoreTheConfigs
    {
        private ILogger? logger;
        private IConfigPathProvider _configPathProvider;

        public SilverConfigMemoryProvider(ILogger logger, IConfigPathProvider configPathProvider)
        {
            _configPathProvider = configPathProvider;
            this.logger = logger;
            _basedir = _configPathProvider.GetPath("Pandora");
        }

        List<RememberedConfig> ConfigMemory = [];
        private string _basedir;

        public void RegisterObjectsToRemember(IEnumerable<ObjectToRemember> objectsToRemember)
        {
            if (!Directory.Exists(_basedir))
            {
                Directory.CreateDirectory(_basedir);
            }
            foreach (var obj in objectsToRemember)
            {
                try
                {
                    var loc = GetConfig(obj.Id);
                    var t = obj.Value?.GetType();
                    if (t == null)
                    {
                        logger?.Error("type is null");
                        continue;
                    }
                    var genericType = typeof(CommentXmlConfigReaderNotifyWhenChanged<>).MakeGenericType([t]);
                    if (genericType == null)
                    {
                        logger?.Error("generic type is null");
                        continue;
                    }
                    var reader = Activator.CreateInstance(genericType);
                    if (reader == null || reader.GetType() != genericType)
                    {
                        logger?.Error("reader is null or reader doesnt match generic type");
                        continue;
                    }
                    if (!File.Exists(loc))
                    {
                        genericType.GetMethod("Write")?.Invoke(reader, [obj.Value, loc]);
                    }
                    var x = genericType.GetMethod("Read")?.Invoke(reader, [loc]);
                    obj.Value = x;
                    if (obj.Value is INotifyPropertyChanged y)
                    {
                        void OnYOnPropertyChanged(object? x, PropertyChangedEventArgs _)
                        {
                            if (x != obj.Value || obj.Value is not ICanBeToldThatAPartOfMeIsChanged { AllowedToRead: true } L) return;
                            L.AllowedToRead = false;
                            genericType.GetMethod("Write")?.Invoke(reader, [obj.Value, loc]);
                            L.AllowedToRead = true;
                        }
                        y.PropertyChanged += OnYOnPropertyChanged;
                    }
                    ConfigMemory.Add(new(obj, reader, loc, genericType));
                }
                catch (Exception e)
                {
                    logger?.Error(e, "Exception in SilverConfigMemoryProvider");
                }
            }
        }

        public string GetConfig(Guid id) => Path.Combine(_basedir, id + ".xml");
    }
}