using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HiddenListener
{
    public class LogglyDataUploader : DataUploaderBase
    {
        private Uri _baseUri;

        public LogglyDataUploader(Uri baseUri)
            : base()
        {
            _baseUri = baseUri;
        }

        public override Task Upload(ICollection<HiddenListenerData> data)
        {
            Debug.WriteLine($"Beginning upload of {data.Count} entries");
            return Task.Run(async () =>
            {
                var streamUri = new UriBuilder(_baseUri);

                // All the matched advertisement packets in this interval
                var valuesArray = data.ToArray();

                for (int i = 0; i < valuesArray.Length; i++)
                {
                    var item = valuesArray[i];
                    HttpContent content = new StringContent(JsonConvert.SerializeObject(item));

                    HttpClient http = new System.Net.Http.HttpClient();
                    Debug.WriteLine($"Sending data to loggly stream {streamUri.ToString()}");
                    HttpResponseMessage response = await http.PostAsync(streamUri.Uri, content);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK ||
                        response.StatusCode == System.Net.HttpStatusCode.Accepted ||
                        response.StatusCode == System.Net.HttpStatusCode.Created)
                    {
                        // this element has been uploaded, it is now safe to remove
                    }
                }
                Debug.WriteLine($"Finished upload of {data.Count} entries");
            });
        }
    }
}
