using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Crestron
    {
    public abstract class CrestronConnection : INotifyPropertyChanged
        {

        public abstract class PropertyHandler
            {
            public String propertyName;
            public PropertyHandler(String propertyName)
                {
                this.propertyName = propertyName;
                }
            public abstract void ParseValue(object o, String value);
            public abstract String ValueToString(object o);
            }

        public abstract class GenericPropertyHandler<PropertyType> : PropertyHandler
            {
            protected delegate ValueType GetValue<ValueType>(Object obj);
            protected delegate void SetValue<ValueType>(Object obj, ValueType value);

            protected GetValue<PropertyType> getValue;
            protected SetValue<PropertyType> setValue;

            public GenericPropertyHandler(PropertyInfo propertyInfo) : base(propertyInfo.Name)
                {
                this.getValue = CreateGetValue<PropertyType>(propertyInfo);
                this.setValue = CreateSetValue<PropertyType>(propertyInfo);
                }

            public override String ValueToString(object o)
                {
                return this.getValue(o).ToString();
                }

            protected GetValue<ValueType> CreateGetValue<ValueType>(PropertyInfo propertyInfo)
                {
                Type[] getValueArgTypes = { typeof(Object) };
                DynamicMethod getValueMethod = new DynamicMethod("get_" + this.propertyName, propertyInfo.PropertyType, getValueArgTypes, this.GetType().Module, true);

                ILGenerator ilGen = getValueMethod.GetILGenerator(1024);

                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Castclass, propertyInfo.DeclaringType);
                ilGen.EmitCall(OpCodes.Call, propertyInfo.GetGetMethod(), null);
                ilGen.Emit(OpCodes.Ret);
                getValueMethod.DefineParameter(1, ParameterAttributes.In, "object");
                GetValue<ValueType> getValue = (GetValue<ValueType>)getValueMethod.CreateDelegate(typeof(GetValue<ValueType>));

                return getValue;
                }

            protected SetValue<ValueType> CreateSetValue<ValueType>(PropertyInfo propertyInfo)
                {
                Type[] setValueArgTypes = { typeof(Object), propertyInfo.PropertyType };
                DynamicMethod setValueMethod = new DynamicMethod("set_" + this.propertyName, propertyInfo.PropertyType, setValueArgTypes, this.GetType().Module, true);

                ILGenerator ilGen = setValueMethod.GetILGenerator(1024);

                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Castclass, propertyInfo.DeclaringType);
                ilGen.Emit(OpCodes.Ldarg_1);
                ilGen.EmitCall(OpCodes.Call, propertyInfo.GetSetMethod(), null);
                ilGen.Emit(OpCodes.Ret);
                setValueMethod.DefineParameter(1, ParameterAttributes.In, "object");
                setValueMethod.DefineParameter(2, ParameterAttributes.In, "value");
                SetValue<ValueType> setValue = (SetValue<ValueType>)setValueMethod.CreateDelegate(typeof(SetValue<ValueType>));

                return setValue;
                }
            }

        public class BoolPropertyHandler : GenericPropertyHandler<bool>
            {
            public BoolPropertyHandler(PropertyInfo propertyInfo) : base(propertyInfo)
                {
                }
            public override String ValueToString(object o)
                {
                return getValue(o) ? "1" : "0";
                }

            public override void ParseValue(object o, String value)
                {
                setValue(o, value == "0" ? false : true);
                }
            }

        public class Uint16PropertyHandler : GenericPropertyHandler<UInt16>
            {
            public Uint16PropertyHandler(PropertyInfo propertyInfo) : base(propertyInfo)
                {
                }
            public override void ParseValue(object o, String value)
                {
                setValue(o, UInt16.Parse(value));
                }
            }

        public class StringPropertyHandler : GenericPropertyHandler<String>
            {
            public StringPropertyHandler(PropertyInfo propertyInfo) : base(propertyInfo)
                {
                }

            public override String ValueToString(object o)
                {
                return getValue(o);
                }

            public override void ParseValue(object o, String value)
                {
                setValue(o, value);
                }
            }

        protected IPAddress ipAddress;
        protected int port;

        protected static Dictionary<String, PropertyHandler> propertyHandlers = null;

        protected TcpClient tcpClient;
        protected StreamReader rxStream;
        protected StreamWriter txStream;

        public event PropertyChangedEventHandler PropertyChanged;

        public CrestronConnection(IPAddress ipAddress, int port)
            {
            this.ipAddress = ipAddress;
            this.port = port;
            if (propertyHandlers == null)
                CreatePropertyHandlers();
            }

        private void CreatePropertyHandlers()
            {
            propertyHandlers = new Dictionary<string, PropertyHandler>();

            TypeInfo typeInfo = this.GetType().GetTypeInfo();

            foreach (MemberInfo memberInfo in typeInfo.DeclaredMembers)
                {
                Type memberInfoType = memberInfo.GetType();

                String name = memberInfo.Name;

                PropertyInfo propertyInfo = memberInfo as PropertyInfo;
                if (propertyInfo != null)
                    {
                    PropertyHandler propertyHandler = null;
                    Type propertyType = propertyInfo.PropertyType;

                    if (propertyType == typeof(bool))
                        {
                        propertyHandler = new BoolPropertyHandler(propertyInfo);
                        }
                    else if (propertyType == typeof(UInt16))
                        {
                        propertyHandler = new Uint16PropertyHandler(propertyInfo);
                        }
                    else if (propertyType == typeof(String))
                        {
                        propertyHandler = new StringPropertyHandler(propertyInfo);
                        }

                    if (propertyHandler != null)
                        {
                        propertyHandlers[propertyInfo.Name] = propertyHandler;
                        }
                    }
                }
            }

        async public void ConnectAsync()
            {
            this.tcpClient = new TcpClient();
            await this.tcpClient.ConnectAsync(this.ipAddress, this.port);
            Stream stream = tcpClient.GetStream();
            this.rxStream = new StreamReader(stream);
            this.txStream = new StreamWriter(stream);
            }

        protected void OnPropertyChanged(String propertyName)
            {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                {
                handler(this, new PropertyChangedEventArgs(propertyName));
                }

            PropertyHandler propertyHandler = null;
            if (propertyHandlers.TryGetValue(propertyName, out propertyHandler))
                {
                Task task = PropertyChangedAsync(propertyHandler);
                }
            }

        async protected Task PropertyChangedAsync(PropertyHandler propertyHandler)
            {
            string propertyName = propertyHandler.propertyName;

            string s = String.Format("{0} = {1}\n", propertyName, propertyHandler.ValueToString(this));

            await this.txStream.WriteAsync(s);
            await this.txStream.FlushAsync();
            }

        protected void RemotePropertyChanged(String propertyName, String propertyValue)
            {
            PropertyHandler propertyHandler;

            if (propertyHandlers != null && (propertyHandlers.TryGetValue(propertyName, out propertyHandler)))
                {
                propertyHandler.ParseValue(this, propertyValue);
                OnPropertyChanged(propertyName);
                }
            }
        }
    }
