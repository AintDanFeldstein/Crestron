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
        public abstract class PropertyAccessor
            {
            public String propertyName;
            public PropertyAccessor(String propertyName)
                {
                this.propertyName = propertyName;
                }
            public abstract void ParseValue(object o, String value);
            public abstract String ValueToString(object o);
            public abstract String GetSimplPlusType();
            }

        public abstract class GenericPropertyAccessor<PropertyType> : PropertyAccessor
            {
            protected delegate ValueType GetValue<ValueType>(Object obj);
            protected delegate void SetValue<ValueType>(Object obj, ValueType value);

            protected GetValue<PropertyType> getValue;
            protected SetValue<PropertyType> setValue;

            public GenericPropertyAccessor(PropertyInfo propertyInfo) : base(propertyInfo.Name)
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
                DynamicMethod setValueMethod = new DynamicMethod("set_" + this.propertyName, null, setValueArgTypes, this.GetType().Module, true);

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

        public class BoolPropertyAccessor : GenericPropertyAccessor<bool>
            {
            public BoolPropertyAccessor(PropertyInfo propertyInfo) : base(propertyInfo)
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

            public override string GetSimplPlusType()
                {
                return "DIGITAL";
                }
            }

        public class Uint16PropertyAccessor : GenericPropertyAccessor<UInt16>
            {
            public Uint16PropertyAccessor(PropertyInfo propertyInfo) : base(propertyInfo)
                {
                }
            public override void ParseValue(object o, String value)
                {
                setValue(o, UInt16.Parse(value));
                }

            public override string GetSimplPlusType()
                {
                return "ANALOG";
                }
            }

        public class StringPropertyAccessor : GenericPropertyAccessor<String>
            {
            public StringPropertyAccessor(PropertyInfo propertyInfo) : base(propertyInfo)
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

            public override string GetSimplPlusType()
                {
                return "STRING";
                }
            }

        protected IPAddress ipAddress;
        protected int port;

        public static Dictionary<String, PropertyAccessor> PropertyAccessors { get; private set; } = null;

        protected TcpClient tcpClient;
        protected NetworkStream tcpStream;
        protected StreamReader rxStream;
        protected StreamWriter txStream;
        protected SocketException socketException;

        public event PropertyChangedEventHandler PropertyChanged;

        public CrestronConnection(IPAddress ipAddress, int port)
            {
            this.ipAddress = ipAddress;
            this.port = port;
            if (PropertyAccessors == null)
                CreatePropertyAccessors();
            }

        private void CreatePropertyAccessors()
            {
            PropertyAccessors = new Dictionary<string, PropertyAccessor>();

            TypeInfo typeInfo = this.GetType().GetTypeInfo();

            foreach (MemberInfo memberInfo in typeInfo.DeclaredMembers)
                {
                Type memberInfoType = memberInfo.GetType();

                String name = memberInfo.Name;

                PropertyInfo propertyInfo = memberInfo as PropertyInfo;
                if (propertyInfo != null)
                    {
                    PropertyAccessor propertyAccessor = null;
                    Type propertyType = propertyInfo.PropertyType;

                    if (propertyType == typeof(bool))
                        {
                        propertyAccessor = new BoolPropertyAccessor(propertyInfo);
                        }
                    else if (propertyType == typeof(UInt16))
                        {
                        propertyAccessor = new Uint16PropertyAccessor(propertyInfo);
                        }
                    else if (propertyType == typeof(String))
                        {
                        propertyAccessor = new StringPropertyAccessor(propertyInfo);
                        }

                    if (propertyAccessor != null)
                        {
                        PropertyAccessors[propertyInfo.Name] = propertyAccessor;
                        }
                    }
                }
            }

        async public Task<bool> ConnectAsync()
            {
            bool success = false;

            // Make sure any open connection is closed.
            CloseConnection();

            try
                {
                this.tcpClient = new TcpClient();

                await this.tcpClient.ConnectAsync(this.ipAddress, this.port);
                this.tcpStream = tcpClient.GetStream();
                this.rxStream = new StreamReader(this.tcpStream);
                this.txStream = new StreamWriter(this.tcpStream);

                success = true;
                }
            catch (SocketException socketException)
                {
                this.socketException = socketException;
                }
            finally
                {
                // If anything failed, close it all down.
                if (!success)
                    CloseConnection();
                }

            return success;
            }

        protected void CloseConnection()
            {
            if (this.rxStream != null)
                this.rxStream.Close();
            if (this.txStream != null)
                this.txStream.Close();
            if (this.tcpStream != null)
                this.tcpStream.Close();
            if (this.tcpClient != null)
                this.tcpClient.Close();

            this.rxStream = null;
            this.txStream = null;
            this.tcpStream = null;
            this.tcpClient = null;
            }

        async public void ConnectAndProcessAsync()
            {
            bool connected = false;

            while (true)
                {
                if (!connected)
                    connected = await ConnectAsync();

                while (connected)
                    {
                    String line;

                    try
                        {

                        line = await this.rxStream.ReadLineAsync();
                        String[] parts = line.Split('=');
                        if (parts.Length == 2)
                            {
                            RemotePropertyChanged(parts[0].Trim(), parts[1].Trim());
                            }
                        }
                    catch (ObjectDisposedException)
                        {
                        connected = false;
                        }
                    }

                // don't try to reconnect for 10 seconds
                await Task.Delay(10 * 1000);
                }
            }

        protected void SetProperty<T>(ref T property, T value, String propertyName)
            {
            if ((property == null) || (!property.Equals(value)))
                {
                property = value;
                OnPropertyChanged(propertyName);
                }
            }

        protected void OnPropertyChanged(String propertyName)
            {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                {
                handler(this, new PropertyChangedEventArgs(propertyName));
                }

            if (PropertyAccessors.TryGetValue(propertyName, out PropertyAccessor propertyAccessor))
                {
                Task task = PropertyChangedAsync(propertyAccessor);
                }
            }

        async protected Task PropertyChangedAsync(PropertyAccessor propertyHandler)
            {
            string propertyName = propertyHandler.propertyName;

            string s = String.Format("{0}={1}\n", propertyName, propertyHandler.ValueToString(this));

            await this.txStream.WriteAsync(s);
            await this.txStream.FlushAsync();
            }

        protected void RemotePropertyChanged(String propertyName, String propertyValue)
            {
            if (PropertyAccessors != null && (PropertyAccessors.TryGetValue(propertyName, out PropertyAccessor propertyAccessor)))
                {
                propertyAccessor.ParseValue(this, propertyValue);
                }
            }

        public IEnumerable<String> DigitalProperties
            {
            get
                {
                foreach (KeyValuePair<String,PropertyAccessor> keyValuePair in PropertyAccessors)
                    {
                    if (keyValuePair.Value is BoolPropertyAccessor)
                        yield return keyValuePair.Key;
                    }
                }
            }

        public IEnumerable<String> AnalogProperties
            {
            get
                {
                foreach (KeyValuePair<String,PropertyAccessor> keyValuePair in PropertyAccessors)
                    {
                    if (keyValuePair.Value is Uint16PropertyAccessor)
                        yield return keyValuePair.Key;
                    }
                }
            }

        public IEnumerable<String> StringProperties
            {
            get
                {
                foreach (KeyValuePair<String,PropertyAccessor> keyValuePair in PropertyAccessors)
                    {
                    if (keyValuePair.Value is StringPropertyAccessor)
                        yield return keyValuePair.Key;
                    }
                }
            }
        }
    }
