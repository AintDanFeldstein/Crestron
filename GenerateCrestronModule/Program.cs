using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crestron;
using System.Net;

namespace GenerateCrestronModule
{
    class Foo : CrestronConnection
    {
        public Foo() : base(IPAddress.Parse("0.0.0.0"), 0)
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
                this.bool1 = value;
                OnPropertyChanged("Bool1");
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
                this.string1 = value;
                OnPropertyChanged("String1");
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
                this.uint1 = value;
                OnPropertyChanged("UInt1");
            }
        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            Foo foo = new Foo();

            foo.Bool1 = true;
            foo.String1 = "foobar";
            foo.UInt1 = 75;
        }
    }
}
