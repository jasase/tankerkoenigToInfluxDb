using ServiceHost.Docker;

namespace TankerkoenigToInfluxDb
{
    public class Program : Startup
    {
        static void Main(string[] args)
            => new Program().Run(args);
    }
}
