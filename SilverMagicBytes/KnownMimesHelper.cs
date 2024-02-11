using System.IO.Compression;

namespace SilverMagicBytes;

public static class KnownMimesHelper
{
    const string header="SilverMagicBytes 1.0";
    public static void LoadTo(Stream file, List<MimeType> mimeTypes)
    {
        using var fs=new BrotliStream(file, CompressionMode.Decompress);
        using var sr=new StreamReader(fs);
        if(sr.ReadLine()!=header)
        {
            throw new NotSupportedException("File is not supported");
        }
        while(!sr.EndOfStream)
        {
            var common=sr.ReadLine();
            var alternatives=sr.ReadLine()?.Split('\\');
            var extensions=sr.ReadLine()?.Split('\\');
            var mindex= mimeTypes.Select((x, i) =>
            {
                return new { item=x, index= i };
            }).FirstOrDefault(x=>x.item.Common==common)?.index;
            var type=sr.ReadLine();
            if(mindex is {} m)
            {
                mimeTypes[m]=new MimeType(common, alternatives, extensions);
            }
            else
            {

                mimeTypes.Add(new MimeType(common, alternatives, extensions));
            }
        }

    }
    public static void SaveFrom(List<MimeType> mimeTypes, Stream file)
    {
        using var fs=new BrotliStream(file, CompressionLevel.Optimal);
        using var tw=new StreamWriter(fs);
        tw.WriteLine(header);
        foreach(var mimeType in mimeTypes)
        {
            tw.WriteLine(mimeType.Common);
            tw.WriteLine(string.Join('\\',mimeType.AlternativeTypes));
            tw.WriteLine(string.Join('\\',mimeType.FileExtensions));
            tw.Write(mimeType.GetType().Name + "\\"+mimeType.GetType().BaseType?.Name);
            if(mimeType is ICompression compression)
            {
                tw.Write($"\\{compression.CompressionType}");
            }
            tw.WriteLine();
        }
    }
}
