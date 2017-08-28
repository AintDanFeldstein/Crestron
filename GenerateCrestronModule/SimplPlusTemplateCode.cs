using Crestron;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenerateCrestronModule
    {
    partial class SimplPlusTemplate
        {
        private CrestronConnection crestronConnection;

        public SimplPlusTemplate(CrestronConnection crestronConnection)
            {
            this.crestronConnection = crestronConnection;
            }
        }
    }
