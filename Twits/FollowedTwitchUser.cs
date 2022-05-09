using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twits
{
    class FollowedTwitchUser
    {
        public string id { get; set; }
        public string name { get; set; }
        public Uri picture { get; set; }

        public FollowedTwitchUser(string name, string id, Uri picture)
        {
            this.id = id;
            this.name = name;
            this.picture = picture;
        }
    }
}
