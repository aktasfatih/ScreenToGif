﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using ScreenToGif.Cloud;

namespace ScreenToGif.Services
{
    public class Gfycat : ICloud
    {
        public async Task<UploadedFile> UploadFileAsync(string path, CancellationToken cancellationToken,
            IProgress<double> progressCallback = null)
        {
            using (var client = new HttpClient())
            {
                using (var res = await client.PostAsync(@"https://api.gfycat.com/v1/gfycats", null, cancellationToken))
                {
                    var result = await res.Content.ReadAsStringAsync();
                    //{"isOk":true,"gfyname":"ThreeWordCode","secret":"15alphanumerics","uploadType":"filedrop.gfycat.com"}

                    var ser = new JavaScriptSerializer();

                    if (!(ser.DeserializeObject(result) is Dictionary<string, object> thing))
                        throw new Exception("It was not possible to get the gfycat name: " + res);

                    var name = thing["gfyname"] as string;

                    using (var content = new MultipartFormDataContent())
                    {
                        content.Add(new StringContent(name), "key");
                        content.Add(new ByteArrayContent(File.ReadAllBytes(path)), "file", name);

                        using (var res2 = await client.PostAsync("https://filedrop.gfycat.com", content, cancellationToken))
                        {
                            if (!res2.IsSuccessStatusCode)
                                throw new Exception("It was not possible to get the gfycat upload result: " + res2);

                            //{"task": "complete", "gfyname": "ThreeWordCode"}
                            //{"progress": "0.03", "task": "encoding", "time": 10}

                            //If the task is not yet completed, try waiting.

                            var input2 = "";

                            while (!input2.Contains("complete"))
                            {
                                using (var res3 = await client.GetAsync("https://api.gfycat.com/v1/gfycats/fetch/status/" + name, cancellationToken))
                                {
                                    input2 = await res3.Content.ReadAsStringAsync();

                                    if (!res3.IsSuccessStatusCode)
                                        throw new UploadingException("It was not possible to get the gfycat upload status: " + res3);
                                }

                                if (!input2.Contains("complete"))
                                    Thread.Sleep(1000);
                            }

                            if (res2.IsSuccessStatusCode)
                                return new UploadedFile() {Link = "https://gfycat.com/" + name};
                        }
                    }
                }
            }

            throw new UploadingException("Unknown error");
        }
    }
}