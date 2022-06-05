using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SPADE {
    internal class AddressEqualityComparer : IEqualityComparer<IPAddress> {
        public bool Equals(IPAddress x, IPAddress y)
            => x.ToString() == y.ToString();

        public int GetHashCode(IPAddress obj)
            => obj.ToString().GetHashCode();
    }
}
