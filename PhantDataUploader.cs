using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HiddenListener
{
    public class PhantDataUploader : DataUploaderBase
    {
        private string _publicKey;
        private string _privateKey;
        private Uri _baseUri;

        public PhantDataUploader(string publicKey, string privateKey, Uri baseUri)
            : base()
        {
            _publicKey = publicKey;
            _privateKey = privateKey;
            _baseUri = baseUri;
        }

        public override Task Upload(ICollection<HiddenListenerData> data)
        {
            Debug.WriteLine($"Beginning upload of {data.Count} entries");
            return Task.Run(async () =>
            {
                var streamUri = new UriBuilder(_baseUri);
                streamUri.Path = Path.Combine("input", _publicKey);

                // All the matched advertisement packets in this interval
                var valuesArray = data.ToArray();

                for (int i = 0; i < valuesArray.Length; i++)
                {
                    var item = valuesArray[i];
                    var queryParameters = $"private_key={_privateKey}&mac={item.Address}" +
                                          $"&timestamp={item.TimeCaptured.ToUnixTimeSeconds()}" +
                                          $"&type={item.BeaconType}&rssi={item.RSSI}&misc_data=0";
                    streamUri.Query = queryParameters;

                    HttpClient http = new System.Net.Http.HttpClient();
                    Debug.WriteLine($"Sending data to phant stream {streamUri.ToString()}");
                    HttpResponseMessage response = await http.GetAsync(streamUri.Uri);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK ||
                        response.StatusCode == System.Net.HttpStatusCode.Accepted ||
                        response.StatusCode == System.Net.HttpStatusCode.Created)
                    {
                        // this element has been uploaded, it is now safe to remove
                        //btAddressToLatestAdvertisementEventMap[item.BluetoothAddress] = null;
                    }
                }
                Debug.WriteLine($"Finished upload of {data.Count} entries");
            });
        }
    }
}
