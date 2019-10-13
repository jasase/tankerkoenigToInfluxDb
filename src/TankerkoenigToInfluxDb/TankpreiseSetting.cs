using Framework.Abstraction.Extension;

namespace Tankpreise
{
    public class TankpreiseSetting : ISetting
    {
        public string TankerkoeningApiKey { get; set; }
        public string Database { get; set; }

        public TankpreiseSetting()
        {
            TankerkoeningApiKey = "";
            Database = "tankstelle";
        }
    }
}
