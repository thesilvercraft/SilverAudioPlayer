using System.ComponentModel;

namespace SilverAudioPlayer.Shared
{
    /// <summary>
    /// Request an object to be serialised and deserialized in the config directory
    /// </summary>
    public interface IAmOnceAgainAskingYouForYourMemory
    {
        public IEnumerable<ObjectToRemember> ObjectsToRememberForMe { get; }
    }
    /// <summary>
    /// A config which could be told that it has been externally changed
    /// </summary>
    public interface ICanBeToldThatAPartOfMeIsChanged
    {
        public void PropertyChanged(object sender, PropertyChangedEventArgs e);
        public bool AllowedToRead { get; set;}
    }
    /// <summary>
    /// An object representing a config to serialise and deserialize
    /// </summary>
    public class ObjectToRemember
    {
        public ObjectToRemember(Guid id, object value)
        {
            Id = id;
            if(value is INotifyPropertyChanged and ICanBeToldThatAPartOfMeIsChanged && value.GetType().GetConstructor(Type.EmptyTypes) !=null)
            {
                Value = value ?? throw new ArgumentNullException(nameof(value));
            }
            else
            {
                throw new ArgumentException("Value must implement INotifyPropertyChanged and ICanBeToldThatAPartOfMeIsChanged and must have a paramaterless constructor", nameof(value));
            }
        }

        public Guid Id { get; set; }
        public object Value { get; set; }
    }
    /// <summary>
    /// A thing capable of sharing the location of serialised objects
    /// </summary>
    public interface IWillTellYouWhereIStoreTheConfigs
    {
        public string GetConfig(Guid id);
    }
    /// <summary>
    /// A thing capable of serialising and deserializing (storing) objects
    /// </summary>
    public interface IWillProvideMemory
    {
        public void RegisterObjectsToRemember(IEnumerable<ObjectToRemember> objectsToRemember);
    }
}
