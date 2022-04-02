using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SilverConfig
{
    public class CommentXmlConfigReader<T> : IConfigReader<T>
    {
        private readonly XmlSerializer serializer;

        public CommentXmlConfigReader()
        {
            serializer = new XmlSerializer(typeof(T));
        }

        public virtual T? Read(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException();
            }
            using var streamWriter = new StreamReader(path);
            return (T?)serializer.Deserialize(streamWriter);
        }

        public virtual bool SupportsComments()
        {
            return true;
        }

        public virtual void Write(T config, string path)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            using var streamWriter = new StreamWriter(path, false);
            MakeDocumentWithComments(XmlUtils.SerializeToXmlDocument(config)).Save(streamWriter);
        }

        private static XmlDocument MakeDocumentWithComments(XmlDocument xmlDocument)
        {
            foreach (var i in typeof(T).GetMembers())
            {
                foreach (var e in i.GetCustomAttributes(false))
                {
                    if (e is CommentAttribute a)
                    {
                        if (a.InsideOfObject)
                        {
                            xmlDocument = XmlUtils.CommentInObject(xmlDocument, $"/{typeof(T).Name}/{i.Name}", a.Description);
                        }
                        else
                        {
                            xmlDocument = XmlUtils.CommentBeforeObject(xmlDocument, $"/{typeof(T).Name}/{i.Name}", a.Description);
                        }
                    }
                }
            }
            return xmlDocument;
        }
    }
}