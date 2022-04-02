using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SilverConfig
{
    public static class XmlUtils
    {
        /// <summary>
        ///     stolen from https://stackoverflow.com/questions/2548708/how-to-create-an-xml-document-from-a-net-object
        /// </summary>
        /// <param name="input">The object to serialize</param>
        /// <returns>A string containing that object as xml</returns>
        public static async Task<string> SerializeToXmlAsync(object input)
        {
            var ser = new XmlSerializer(input.GetType());
            await using var memStm = new MemoryStream();
            ser.Serialize(memStm, input);
            memStm.Position = 0;
            var result = await new StreamReader(memStm).ReadToEndAsync();
            return result;
        }

        /// <summary>
        ///     stolen from https://stackoverflow.com/questions/2548708/how-to-create-an-xml-document-from-a-net-object
        /// </summary>
        /// <param name="input">The object to serialize</param>
        /// <returns>A XmlDocument containing the object as xml</returns>
        public static XmlDocument SerializeToXmlDocument(object input)
        {
            var ser = new XmlSerializer(input.GetType());
            using var memStm = new MemoryStream();
            ser.Serialize(memStm, input);
            memStm.Position = 0;
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true
            };
            using var xtr = XmlReader.Create(memStm, settings);
            var xd = new XmlDocument();
            xd.Load(xtr);
            return xd;
        }

        /// <summary>
        ///     kinda stolen from
        ///     https://stackoverflow.com/questions/6328288/how-to-comment-a-line-of-a-xml-file-in-c-sharp-with-system-xml
        /// </summary>
        /// <param name="inputdoc">The input document</param>
        /// <param name="xpath">Xpath of the Object</param>
        /// <param name="comment">The comment</param>
        /// <returns>A XmlDocument that has the comment before the xpath thingy</returns>
        public static XmlDocument CommentInObject(XmlDocument inputdoc, string xpath, string comment)
        {
            var elementToComment = inputdoc.SelectSingleNode(xpath);
            if (elementToComment == null)
                throw new Exception("Could not find the element to comment, looked for " + xpath);
            elementToComment.InnerText = $"<!--{comment}-->\n{elementToComment.InnerText}";
            return inputdoc;
        }

        /// <summary>
        ///     kinda stolen from
        ///     https://stackoverflow.com/questions/6328288/how-to-comment-a-line-of-a-xml-file-in-c-sharp-with-system-xml
        /// </summary>
        /// <param name="inputdoc">The input document</param>
        /// <param name="xpath">Xpath of the Object</param>
        /// <param name="comment">The comment</param>
        /// <returns>A XmlDocument that has the comment before the xpath thingy</returns>
        public static XmlDocument CommentBeforeObject(XmlDocument inputdoc, string xpath, string comment)
        {
            var elementToComment = inputdoc.SelectSingleNode(xpath);
            var commentNode = inputdoc.CreateComment(comment);
            var parentNode = elementToComment?.ParentNode;
            parentNode?.InsertBefore(commentNode, elementToComment);
            return inputdoc;
        }
    }
}