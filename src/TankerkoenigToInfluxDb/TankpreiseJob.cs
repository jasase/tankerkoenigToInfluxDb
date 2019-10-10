using Framework.Abstraction.Extension;
using Framework.Abstraction.Services.DataAccess.InfluxDb;
using Framework.Abstraction.Services.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using Tankpreise.Tankerkoenig;

namespace Tankpreise
{
    public class TankpreiseJob : IJob
    {
        private static readonly Tuple<string, string, int>[] _postions = new[]
        {
            new Tuple<string, string, int>("52.339187", "9.352008", 3), //Bad Nenndorf
            new Tuple<string, string, int>("52.505395", "9.479457", 3), //Arbeit Neustadt
            new Tuple<string, string, int>("52.416356", "9.641335", 1), //Arbeit Garbsen
            new Tuple<string, string, int>("52.162919", "9.476763", 3), //Hachmühlen            
            new Tuple<string, string, int>("52.272593", "9.362178", 2)  //Lauenau
        };
        private readonly TankerkoenigApi _api;
        private readonly Dictionary<string, Tankstelle> _tankstellen;
        private readonly ILogger _logger;
        private readonly IInfluxDbUpload _influxDbUpload;

        private int _lastRequestDay;

        public TankpreiseJob(ILogManager logManager, ILogger logger, TankpreiseSetting setting, IInfluxDbUpload influxDbUpload)
        {
            _logger = logger;
            _influxDbUpload = influxDbUpload;
            _api = new TankerkoenigApi(setting, logManager.GetLogger(typeof(TankerkoenigApi)));
            _tankstellen = new Dictionary<string, Tankstelle>();
            _lastRequestDay = -1;
        }

        public string Name => "Tankpreise";

        public void Execute()
        {
            if (DateTime.Now.Day != _lastRequestDay)
            {
                _logger.Info("Clear current list of tankstellen, because new day reached. Last day was {0}", _lastRequestDay);
                _tankstellen.Clear();
            }

            if (!_tankstellen.Any())
            {
                _logger.Info("No tankstellen known. Requesting list of availible tankstellen");
                LoadTankstellen();
                _lastRequestDay = DateTime.Now.Day;
            }

            RequestPrices();            
        }

        private void UploadPrices(TankstellenPreis[] prices)
            => _influxDbUpload.QueueWrite(CreateInfluxDbEntries(prices).ToArray(), 5, "tankstelle");

        private IEnumerable<InfluxDbEntry> CreateInfluxDbEntries(IEnumerable<TankstellenPreis> prices)
        {
            foreach (var price in prices)
            {
                yield return new InfluxDbEntry
                {
                    Measurement = "tankstellenPreise",
                    Tags = new InfluxDbEntryField[]
                    {
                        new InfluxDbEntryField { Name = "name", Value = price.Tankstelle.Ort + " - " + price.Tankstelle.Name},
                        new InfluxDbEntryField { Name = "brand", Value = price.Tankstelle.Brand},
                        new InfluxDbEntryField { Name = "sorte", Value = price.Sorte},
                        new InfluxDbEntryField { Name = "tankstellenid", Value = price.Tankstelle.Id},
                        new InfluxDbEntryField { Name = "Ort", Value = price.Tankstelle.Ort},
                        new InfluxDbEntryField { Name = "PLZ", Value = price.Tankstelle.PLZ},
                        new InfluxDbEntryField { Name = "Strasse", Value = price.Tankstelle.Strasse},
                    },
                    Fields = new InfluxDbEntryField[]
                    {
                        new InfluxDbEntryField { Name = "value", Value = price.Preis}
                    }
                };
            }
        }

        private void RequestPrices()
        {
            var ids = _tankstellen.Select(x => x.Value.Id).GetEnumerator();
            var idsToRequest = new List<string>();

            while (ids.MoveNext())
            {

                idsToRequest.Add(ids.Current);

                if (idsToRequest.Count >= 10)
                {
                    _logger.Info("Request price for tankestelle with ids [{0}]", string.Join(", ", idsToRequest));
                    RequestPrices(idsToRequest.ToArray());
                    idsToRequest.Clear();
                }
            }

            _logger.Info("Request price for tankestelle with ids [{0}]", string.Join(", ", idsToRequest));
            RequestPrices(idsToRequest.ToArray());
        }

        private void RequestPrices(string[] ids)
        {
            var priceList = new List<TankstellenPreis>();
            var requestResult = _api.RequestPrices(ids);
            if (requestResult == null) return;

            foreach (var price in requestResult.prices)
            {
                if (price.Value.status != "open" ||
                    !_tankstellen.ContainsKey(price.Key)) continue;

                var tankstelle = _tankstellen[price.Key];

                if (price.Value.diesel is double)
                {
                    var priceTyped = new TankstellenPreis
                    {
                        Preis = (double) price.Value.diesel,
                        Sorte = "Diesel",
                        Tankstelle = tankstelle
                    };
                    priceList.Add(priceTyped);
                }
                if (price.Value.e5 is double)
                {
                    var priceTyped = new TankstellenPreis
                    {
                        Preis = (double) price.Value.e5,
                        Sorte = "Super",
                        Tankstelle = tankstelle
                    };
                    priceList.Add(priceTyped);
                }
                if (price.Value.e10 is double)
                {
                    var priceTyped = new TankstellenPreis
                    {
                        Preis = (double) price.Value.e10,
                        Sorte = "E10",
                        Tankstelle = tankstelle
                    };
                    priceList.Add(priceTyped);
                }
            }

            UploadPrices(priceList.ToArray());
        }

        private void LoadTankstellen()
        {
            foreach (var position in _postions)
            {
                _logger.Info("Requesting list of availible tankstellen for postion [{0} - {1}] with radius {2}",
                    position.Item1,
                    position.Item2,
                    position.Item3);
                var requestResult = _api.RequestList(position.Item1, position.Item2, position.Item3);

                if (requestResult != null)
                {
                    foreach (var station in requestResult.stations)
                    {

                        var newTankstelle = new Tankstelle
                        {
                            Brand = station.brand,
                            Id = station.id,
                            Name = station.name,
                            Ort = station.place,
                            PLZ = station.postCode,
                            Strasse = station.street + " " + station.houseNumber
                        };
                        _logger.DebugDump("Received new Tankstelle", newTankstelle);

                        _tankstellen.Add(newTankstelle.Id, newTankstelle);
                    }
                }
            }
        }
    }
}
