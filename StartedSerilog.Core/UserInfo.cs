using System.Collections.Generic;

namespace StartedSerilog.Core
{
    internal class UserInfo
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public Dictionary<string, List<string>> UserClaims { get; set; }
    }
}