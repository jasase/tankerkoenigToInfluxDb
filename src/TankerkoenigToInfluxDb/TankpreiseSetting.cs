using Framework.Abstraction.Extension;

namespace Tankpreise
{
    public class TankpreiseSetting : ISetting
    {
        public string TankerkoeningApiKey { get; set; }

        public TankpreiseSetting()
        {
            TankerkoeningApiKey = "";
        }
    }
}
