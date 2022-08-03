using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVROSC
{
    public class OSCAddress
    {
        [JsonProperty("address")]
        public string ParameterAddress { get; set; }

        [JsonProperty("type")]
        public string ParameterType { get; set; }
    }

    public class OSCParameter
    {
        [JsonProperty("name")]
        public string ParameterName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "input")]
        public OSCAddress Input { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "output")]
        public OSCAddress Output { get; set; }
    }

    public class OSCConfig
    {
        [JsonProperty("id")]
        public string AvatarGUID { get; set; }

        [JsonProperty("name")]
        public string AvatarName { get; set; }

        [JsonProperty("parameters")]
        public List<OSCParameter> AvatarParameters { get; set; }
    }

}
