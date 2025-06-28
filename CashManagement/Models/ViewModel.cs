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
    public class SupplierTransactionViewModel
    {
        public int Id { get; set; }
        public string SupplierName { get; set; }
        public decimal Amount { get; set; }
        public DebitCreditType DebitCreditType { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; }
        public string UserName { get; set; }
    }
    public class SupplierDashboardViewModel
    {
        public int TotalSuppliers { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalDebit { get; set; }
        public int CreditSuppliersCount { get; set; }
        public int DebitSuppliersCount { get; set; }
        public int ZeroBalanceSuppliersCount { get; set; }
        public List<SupplierTransactionViewModel> LatestTransactions { get; set; }
        public SupplierType? SupplierType { get; set; }
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