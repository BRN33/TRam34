using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Shared.Helpers
{

    // Bu sınıf, log appsettingsten aldıgı degerleri tutacak ve ona göre log yazıp yazmamaya karar vericek
    public class LogEndpointSettings
    {

        public bool Alarm { get; set; }
        public bool Error { get; set; }
        public bool Event { get; set; }
        public bool Information { get; set; }
        public bool Warning { get; set; }
    }
}
