using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HiddenListener
{
    public abstract class DataUploaderBase
    {
        public abstract Task Upload(ICollection<HiddenListenerData> data);
    }
}
