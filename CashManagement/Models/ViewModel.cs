namespace CashManagement.Models
{
    public class SupplierViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public SupplierType Type { get; set; }
        public string PhoneNumber { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal CreditTotal { get; set; }
        public decimal DebitTotal { get; set; }
        public string NetBalance { get; set; }
    }

    // **ViewModel لعرض تفاصيل مورد/عميل**
    public class SupplierDetailsViewModel
    {
        public Supplier Supplier { get; set; }
        public List<SupplierTransaction> Transactions { get; set; }
        public decimal CreditTotal { get; set; }
        public decimal DebitTotal { get; set; }
        public string NetBalance { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DebitCreditType? DebitCreditType { get; set; }
    }
}