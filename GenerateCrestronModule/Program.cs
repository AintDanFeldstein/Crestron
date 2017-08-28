using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crestron;
using System.Net;

namespace GenerateCrestronModule
{
    class MyHomeCrestron : CrestronConnection
    {
        public MyHomeCrestron() : base(IPAddress.Parse("0.0.0.0"), 0)
        {
        }

        private bool bool1;
        public bool Bool1
        {
            get
            {
                return this.bool1;
            }
            set
            {
                SetProperty(ref this.bool1, value, "bool1");
            }
        }

        private String string1;
        public String String1
        {
            get
            {
                return this.string1;
            }
            set
            {
                SetProperty(ref this.string1, value, "string1");
            }
        }

        private UInt16 uint1;
        public UInt16 UInt1
        {
            get
            {
                return this.uint1;
            }
            set
            {
                SetProperty(ref this.uint1, value, "uint1");
            }
        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            MyHomeCrestron myHomeCrestron = new MyHomeCrestron();

            myHomeCrestron.Bool1 = true;
            myHomeCrestron.String1 = "foobar";
            myHomeCrestron.UInt1 = 75;
            SimplPlusTemplate template = new SimplPlusTemplate(myHomeCrestron);
            String result = template.TransformText();
            System.IO.File.WriteAllText("c:\\tmp\\foo.xxx", result);
        }
    }
}
