using Serilog;
using SilverAudioPlayer.Shared;
using SilverConfig;
using System.ComponentModel;
using System.Composition;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;

namespace SilverAudioPlayer.Any.MemoryProvider.SilverConfigMemoryProvider
{
    [Export(typeof(IWillProvideMemory))]
    public class SilverConfigMemoryProvider : IWillProvideMemory, IWillTellYouWhereIStoreTheConfigs
    {
        public void MakeSureAllIsWell()
        {
        }
        List<ObjectClass> Classes = new();
        string basedir = Path.Combine(ConfigPath.BasePath, "Pandora");

        public void RegisterObjectsToRemember(IEnumerable<ObjectToRemember> objectsToRemember)
        {
            if(!Directory.Exists(basedir))
            {
                Directory.CreateDirectory(basedir);
            }
            foreach (var obj in objectsToRemember)
            {
                var loc = Path.Combine(basedir, obj.Id.ToString() + ".xml");
                var t = obj.Value?.GetType();
                if(t==null)
                {
                    continue;
                }
                Type genericType = typeof(CommentXmlConfigReaderNotifyWhenChanged<>).MakeGenericType(new[] { t });
                var reader = Activator.CreateInstance(genericType);
                if (reader != null && genericType!=null && reader.GetType() == genericType)
                {
                    if(!File.Exists(loc))
                    {
                        genericType?.GetMethod("Write")?.Invoke(reader, new object[] { obj.Value, loc });
                    }
                    var x = genericType?.GetMethod("Read")?.Invoke(reader, new object[] { loc });
                    obj.Value = x;
                    if(obj.Value is INotifyPropertyChanged y)
                    {
                        y.PropertyChanged += (x, y) =>
                        {
                            if(x == obj.Value && obj.Value is ICanBeToldThatAPartOfMeIsChanged L && L.AllowedToRead)
                            {
                                genericType?.GetMethod("Write")?.Invoke(reader, new object[] {obj.Value, loc });
                            }
                        };
                    }
                    Classes.Add(new(obj, reader, loc, genericType));
                }
            }
            
        }

        public string GetConfig(Guid id)
        {
            return Path.Combine(basedir, id.ToString() + ".xml");
        }
    }
    public record class ObjectClass(ObjectToRemember obj, object config, string loc, Type  genericType  );
}