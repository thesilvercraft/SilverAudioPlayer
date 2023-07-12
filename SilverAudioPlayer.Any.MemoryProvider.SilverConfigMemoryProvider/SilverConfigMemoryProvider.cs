using Serilog;
using SilverAudioPlayer.Shared;
using SilverConfig;
using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;

namespace SilverAudioPlayer.Any.MemoryProvider.SilverConfigMemoryProvider
{
    [Export(typeof(IWillProvideMemory))]
    public class SilverConfigMemoryProvider : IWillProvideMemory, IWillTellYouWhereIStoreTheConfigs
    {
   
        List<ObjectClass> Classes = new();
        readonly string _basedir = ConfigPath.GetPath("Pandora");

        public void RegisterObjectsToRemember(IEnumerable<ObjectToRemember> objectsToRemember)
        {
            if(!Directory.Exists(_basedir))
            {
                Directory.CreateDirectory(_basedir);
            }
            foreach (var obj in objectsToRemember)
            {
                try
                {
                    var loc = GetConfig(obj.Id);
                    var t = obj.Value?.GetType();
                    if(t==null)
                    {
                        continue;
                    }
                    var genericType = typeof(CommentXmlConfigReaderNotifyWhenChanged<>).MakeGenericType(new[] { t });
                    var reader = Activator.CreateInstance(genericType);
                    if (reader == null || genericType == null || reader.GetType() != genericType) continue;
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
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        public string GetConfig(Guid id)
        {
            return Path.Combine(_basedir, id + ".xml");
        }
    }
    public record class ObjectClass(ObjectToRemember obj, object config, string loc, Type  genericType  );
}