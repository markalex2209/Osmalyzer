﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Osmalyzer
{
    public static class WebsiteDownloadHelper
    {
        private static readonly List<(string url, string content)> _cachedWebsites = new List<(string url, string content)>();
        
        
        [MustUseReturnValue]
        public static string Read(string url, bool canUseCache)
        {
            if (canUseCache)
            {
                (string _, string content)? cached = _cachedWebsites.FirstOrDefault(cw => cw.url == url);

                if (cached != null)
                    return cached.Value.content;
            }
            
            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = client.GetAsync(url).Result;
            using HttpContent content = response.Content;
            string result = content.ReadAsStringAsync().Result;

            if (canUseCache)
                _cachedWebsites.Add((url, result));

            return result;
        }

        public static void Download(string url, string fileName)
        {
            using HttpClient client = new HttpClient();
            using Task<Stream> stream = client.GetStreamAsync(url);
            using FileStream fileStream = new FileStream(fileName, FileMode.Create);
            stream.Result.CopyTo(fileStream);
        }
    }
}