using System;
using System.Collections;
using System.Linq;
using Soneta.Business;
using Soneta.Types.DynamicApi;
using YOUR_NAMESPACE.Interfaces;

[assembly: Service(typeof(YOUR_NAMESPACE.Interfaces.IYourApi), typeof(YOUR_NAMESPACE.YourApi), ServiceScope.Session)]
[assembly: DynamicApiController(typeof(YOUR_NAMESPACE.Interfaces.IYourApi), typeof(YOUR_NAMESPACE.YourApi))]

namespace YOUR_NAMESPACE
{
    public class YourApi : IYourApi
    {
        private readonly Session _session;

        public YourApi(Session session)
        {
            _session = session;
        }

        // Example method using Session and WorkSession
        public string GetSomething(string param)
        {
            using (var workSession = _session.Login.CreateSession(true, false))
            {
                // Your logic here
                return $"Result for {param}";
            }
        }
    }
}
