using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Shared
{
    public interface IAmOnceAgainAskingYouForYourMemory
    {
        public ObjectToRemember[] ObjectsToRememberForMe { get; }
    }
    public interface ICanBeToldThatAPartOfMeIsChanged
    {
        public void PropertyChanged(object sender, PropertyChangedEventArgs e);
        public bool AllowedToRead { get; }
    }
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
    public interface IWillTellYouWhereIStoreTheConfigs
    {
        public string GetConfig(Guid id);
    }
    public interface IWillProvideMemory
    {
        public void RegisterObjectsToRemember(IEnumerable<ObjectToRemember> objectsToRemember);
        public void MakeSureAllIsWell();
    }
}
