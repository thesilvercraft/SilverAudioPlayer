using System.Xml.Serialization;

namespace SilverAudioPlayerModuleHandler.Build;

public class SilverAudioPlayerModulesInfo
{
    public List<Module> Modules { get; set; } = [];
}

public class Module
{
    [XmlAttribute(AttributeName = "Name")]
    public string Name { get; set; }

    public List<string> Dependencies { get; set; } = [];
    public List<File> Files { get; set; } = [];
    public List<LinuxLink> Links { get; set; } = [];

    public List<File> Debug { get; set; } = [];

}
public class LinuxLink
{
    /// <summary>
    /// The location of the link, e.g. /usr/bin/catenoid
    /// </summary>
    [XmlAttribute(AttributeName = "LocationFrom")]
    public string LocationFrom { get; set; }
    /// <summary>
    /// The location the link points to, e.g. /opt/catenoid/catenoid
    /// </summary>
    [XmlAttribute(AttributeName = "LocationTo")]
    public string LocationTo { get; set; }

}
public class File
{
    [XmlAttribute(AttributeName = "Name")]
    public string Name { get; set; }
    [XmlAttribute(AttributeName = "LocLin")]
    public string? LocationToPlaceLinux { get; set; }
}