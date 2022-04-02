using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverConfig
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CommentAttribute : Attribute
    {
        public string Description;
        public bool InsideOfObject;

        public CommentAttribute(string des, bool inside = false)
        {
            Description = des;
            InsideOfObject = inside;
        }
    }
}