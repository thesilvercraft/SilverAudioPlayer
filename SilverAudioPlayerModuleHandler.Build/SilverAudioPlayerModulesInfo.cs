using System.Xml.Serialization;

namespace SilverAudioPlayerModuleHandler.Build;

public class SilverAudioPlayerModulesInfo
{
    public List<Module> Modules { get; set; } = new();
}

public class Module
{
    [XmlAttribute(AttributeName="Name")] 
    public string Name { get; set; }

    public List<string> Dependencies { get; set; } = new();
    public List<File> Files { get; set; }= new();
    public List<File> Debug { get; set; }= new();

}

public class File
{
    [XmlAttribute(AttributeName="Name")] 
    public string Name { get; set; }
    
}