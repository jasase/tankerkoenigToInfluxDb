using Framework.Abstraction.Extension;
using Newtonsoft.Json;
using System;
using System.Net.Http;

namespace Tankpreise.Tankerkoenig
{
    public class TankerkoenigApi
    {
        private const string REQUEST_URI_BASE = @"https://creativecommons.tankerkoenig.de/json/";
        private const string REQUEST_URI_LIST = @"list.php?lat={0}&lng={1}&rad={2}&type=all&apikey={3}";
        private const string REQUEST_URI_DETAILS = @"detail.php?id={0}&apikey={1}";
        private const string REQUEST_URI_PRICES = @"prices.php?ids={0}&apikey={1}";

        private readonly TankpreiseSetting _settings;
        private readonly ILogger _logger;
        private readonly HttpClient _client;

        public TankerkoenigApi(TankpreiseSetting settings, ILogger logger)
        {
            _settings = settings;
            _logger = logger;
            _client = new HttpClient()
            {
                BaseAddress = new Uri(REQUEST_URI_BASE)
            };
        }

        public ListResultEntity RequestList(string lat, string lng, int radius)
        {
            var relativeUri = string.Format(REQUEST_URI_LIST, lat, lng, radius, _settings.TankerkoeningApiKey);
            return CallApi<ListResultEntity>(relativeUri);
        }

        public PricesResultEntity RequestPrices(string[] id)
        {
            var relativeUri = string.Format(REQUEST_URI_PRICES, string.Join(",", id), _settings.TankerkoeningApiKey);
            return CallApi<PricesResultEntity>(relativeUri);
        }

        private T CallApi<T>(string relativeUri)
        {
            var result = _client.GetAsync(relativeUri).Result;
            if (result.IsSuccessStatusCode)
            {
                var stringValue = result.Content.ReadAsStringAsync().Result;
                var value = JsonConvert.DeserializeObject<T>(stringValue);
                return value;
            }
            else
            {
                _logger.Error("Tankerkoening API request failed with: Code [{0}] {1}", result.StatusCode, result.ReasonPhrase);
            }
            return default(T);
        }
    }
}
