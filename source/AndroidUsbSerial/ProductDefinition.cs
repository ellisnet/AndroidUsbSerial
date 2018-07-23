namespace AndroidUsbSerial
{
    public class ProductDefinition
    {
        public int VendorId { get; }
        public int ProductId { get; }
        public DriverOption Driver { get; }

        public ProductDefinition(int vendorId, int productId, DriverOption driverOption)
        {
            VendorId = vendorId;
            ProductId = productId;
            Driver = driverOption;
        }
    }
}
