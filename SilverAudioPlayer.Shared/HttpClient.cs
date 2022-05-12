using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.Shared
{
    public static class HttpClient
    {
        public static readonly System.Net.Http.HttpClient Client = new();
    }
}