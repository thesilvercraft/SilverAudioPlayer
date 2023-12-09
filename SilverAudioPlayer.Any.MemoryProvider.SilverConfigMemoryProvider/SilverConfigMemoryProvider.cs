using Serilog;
using SilverAudioPlayer.Shared;
using System.ComponentModel;
using System.Composition;

namespace SilverAudioPlayer.Any.MemoryProvider.SilverConfigMemoryProvider
{
    public record class RememberedConfig(ObjectToRemember obj, object configReader, string location, Type genericType);

    [Export(typeof(IWillProvideMemory))]
    public class SilverConfigMemoryProvider : IWillProvideMemory, IWillTellYouWhereIStoreTheConfigs
    {
        private ILogger? logger;

        public SilverConfigMemoryProvider()
        {
            logger = Logger.GetLogger(typeof(SilverConfigMemoryProvider));
        }

        List<RememberedConfig> ConfigMemory = [];
        readonly string _basedir = ConfigPath.GetPath("Pandora");

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
                        continue;
                    var genericType = typeof(CommentXmlConfigReaderNotifyWhenChanged<>).MakeGenericType([t]);
                    if (genericType == null)
                        continue;
                    var reader = Activator.CreateInstance(genericType);
                    if (reader == null || reader.GetType() != genericType) continue;
                    if (!File.Exists(loc))
                    {
                        genericType.GetMethod("Write")?.Invoke(reader, [obj.Value, loc]);
                    }
                    var x = genericType.GetMethod("Read")?.Invoke(reader, [loc]);
                    obj.Value = x;
                    if (obj.Value is INotifyPropertyChanged y)
                    {
                        y.PropertyChanged += (x, y) =>
                        {
                            if (x == obj.Value && obj.Value is ICanBeToldThatAPartOfMeIsChanged L && L.AllowedToRead)
                            {
                                L.AllowedToRead = true;
                                genericType.GetMethod("Write")?.Invoke(reader, [obj.Value, loc]);
                                L.AllowedToRead = false;
                            }
                        };
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