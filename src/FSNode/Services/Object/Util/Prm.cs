using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Session;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Fs.Services.Object.Util
{
    public class CommonPrm
    {
        private bool local;
        private SessionToken token;
        private BearerToken bearer;

        public bool Local
        {
            get => this.local;
            set => this.local = value;
        }

        public SessionToken Token
        {
            get => this.token;
            set => this.token = value;
        }

        public BearerToken Bearer
        {
            get => this.bearer;
            set => this.bearer = value;
        }
    }
}
