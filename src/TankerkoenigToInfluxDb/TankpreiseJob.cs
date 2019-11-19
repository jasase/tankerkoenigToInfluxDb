using Framework.Abstraction.Extension;
using Framework.Abstraction.Services.DataAccess.InfluxDb;
using Framework.Abstraction.Services.Scheduling;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tankpreise.Tankerkoenig;

namespace Tankpreise
{
    public class TankpreiseJob : IJob
    {
        private const string LOCATION_DELIMITER = "|";
        private const string LOCATION_PARAMETER_DELIMITER = ";";

        private readonly Tuple<double, double, int>[] _postions;
        private readonly TankerkoenigApi _api;
        private readonly Dictionary<string, Tankstelle> _tankstellen;
        private readonly ILogger _logger;
        private readonly TankpreiseSetting _setting;
        private readonly IInfluxDbUpload _influxDbUpload;

        private int _lastRequestDay;

        public TankpreiseJob(ILogManager logManager, ILogger logger, TankpreiseSetting setting, IInfluxDbUpload influxDbUpload)
        {
            _logger = logger;
            _setting = setting;
            _influxDbUpload = influxDbUpload;
            _tankstellen = new Dictionary<string, Tankstelle>();
            _lastRequestDay = -1;

            _postions = ParseLocationString().ToArray();
            _api = new TankerkoenigApi(_setting, logManager.GetLogger(typeof(TankerkoenigApi)));

            _logger.Info("Location list:\r\n{0}",
                         string.Join(Environment.NewLine, _postions.Select(x => x.Item1 + "\t" + x.Item2 + "\t" + x.Item3)));
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
            => _influxDbUpload.QueueWrite(CreateInfluxDbEntries(prices).ToArray(), 5, _setting.Database);

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
                        Preis = (double)price.Value.diesel,
                        Sorte = "Diesel",
                        Tankstelle = tankstelle
                    };
                    priceList.Add(priceTyped);
                }
                if (price.Value.e5 is double)
                {
                    var priceTyped = new TankstellenPreis
                    {
                        Preis = (double)price.Value.e5,
                        Sorte = "Super",
                        Tankstelle = tankstelle
                    };
                    priceList.Add(priceTyped);
                }
                if (price.Value.e10 is double)
                {
                    var priceTyped = new TankstellenPreis
                    {
                        Preis = (double)price.Value.e10,
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

        private IEnumerable<Tuple<double, double, int>> ParseLocationString()
        {
            var counter = 1;
            var locationsStrings = (_setting.Locations ?? string.Empty).Split(LOCATION_DELIMITER, StringSplitOptions.RemoveEmptyEntries);
            foreach (var cur in locationsStrings)
            {
                var curSplitted = cur.Split(LOCATION_PARAMETER_DELIMITER, StringSplitOptions.RemoveEmptyEntries);
                if (curSplitted.Length == 3 &&
                   double.TryParse(curSplitted[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                   double.TryParse(curSplitted[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng) &&
                   int.TryParse(curSplitted[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var radius))
                {
                    yield return new Tuple<double, double, int>(lat, lng, radius);
                }
                else
                {
                    _logger.Error("Part {0} of location string does not match format \"<lat>;<lng>;<radius>|\". Number format is 0.00", counter);
                }
                counter++;
            }
        }
    }
}
