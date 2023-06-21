using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARTICARES
{
    class Signaling
    {
        public static string projectId;
        public static void configure_signaling_server()
        {
            projectId = "webrtc-signaling-57733";
            string filepath = "C:/repos/webrtc-signaling-57733-85acfd65782c.json";
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);
        }
    }
}
