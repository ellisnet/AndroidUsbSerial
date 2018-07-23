using System;
using System.Collections.Generic;
using AndroidUsbSerial.Drivers;

namespace AndroidUsbSerial
{
    public enum DriverOption
    {
        Unknown = 0,
        CdcAcmSerialDriver,
        Ch34xSerialDriver,
        Cp21xxSerialDriver,
        FtdiSerialDriver,
        ProlificSerialDriver,
    }

    public class ProbeTable
    {
        private readonly object _dictionaryLocker = new object();
        private readonly IDictionary<string, IUsbSerialDriverFactory> _probeTable = new Dictionary<string, IUsbSerialDriverFactory>();

        private string GetKey(int vendorId, int productId) => $"{vendorId}-{productId}";

        private IUsbSerialDriverFactory GetDriverFactory(DriverOption driverOption)
        {
            IUsbSerialDriverFactory result = null;

            switch (driverOption)
            {
                case DriverOption.CdcAcmSerialDriver:
                    result = new CdcAcmSerialDriverFactory();
                    break;
                case DriverOption.Ch34xSerialDriver:
                    result = new Ch34xSerialDriverFactory();
                    break;
                case DriverOption.Cp21xxSerialDriver:
                    result = new Cp21xxSerialDriverFactory();
                    break;
                case DriverOption.FtdiSerialDriver:
                    result = new FtdiSerialDriverFactory();
                    break;
                case DriverOption.ProlificSerialDriver:
                    result = new ProlificSerialDriverFactory();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(driverOption), driverOption, null);
            }

            return result;
        }

        public virtual ProbeTable AddProduct(ProductDefinition product)
        {
            if (product == null) { throw new ArgumentNullException(nameof(product));}
            if (product.Driver == DriverOption.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(product), "Cannot add a product with unknown driver.");
            }

            IUsbSerialDriverFactory factory = GetDriverFactory(product.Driver);

            lock (_dictionaryLocker)
            {
                string key = GetKey(product.VendorId, product.ProductId);
                if (_probeTable.ContainsKey(key))
                {
                    _probeTable[key] = factory;
                }
                else
                {
                    _probeTable.Add(key, factory);
                }
            }

            return this;
        }

        internal virtual ProbeTable AddDriver(DriverOption driverOption, IDictionary<int, int[]> supportedDevices)
        {
            foreach (KeyValuePair<int, int[]> entry in supportedDevices)
            {
                foreach (int productId in entry.Value)
                {
                    AddProduct(new ProductDefinition(entry.Key, productId, driverOption));
                }
            }

            return this;
        }

        public virtual IUsbSerialDriverFactory FindDriverFactory(int vendorId, int productId)
        {
            IUsbSerialDriverFactory result = null;

            lock (_dictionaryLocker)
            {
                string key = GetKey(vendorId, productId);
                if (_probeTable.ContainsKey(key))
                {
                    result = _probeTable[key];
                }
            }

            return result;
        }
    }
}
