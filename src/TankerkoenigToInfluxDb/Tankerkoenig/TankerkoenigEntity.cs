using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tankpreise.Tankerkoenig
{
    public abstract class TankerkoenigEntity
    {
        public string status { get; set; }
        public bool ok { get; set; }
    }

    public class ListResultEntity : TankerkoenigEntity
    {
        public ListResultEntry[] stations { get; set; }
    }

    public class ListResultEntry
    {
        public string id { get; set; }
        public string name { get; set; }
        public double lat { get; set; }
        public double lng { get; set; }
        public string brand { get; set; }
        public object diesel { get; set; }
        public object e5 { get; set; }
        public object e10 { get; set; }
        public string street { get; set; }
        public string houseNumber { get; set; }
        public string postCode { get; set; }
        public string place { get; set; }
    }

    public class PricesResultEntity : TankerkoenigEntity
    {
        public Dictionary<string, PricesResultEntry> prices { get; set; }
    }

    public class PricesResultEntry
    {
        public string status { get; set; }
        public object diesel { get; set; }
        public object e5 { get; set; }
        public object e10 { get; set; }
    }
}
