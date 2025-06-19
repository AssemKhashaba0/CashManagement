namespace CashManagement.Models
{
    public class FrozenAmount
    {
        public int Id { get; set; }
        public int CashLineId { get; set; }
        public CashLine CashLine { get; set; }
        public decimal Amount { get; set; }
        public decimal Fees { get; set; }
        public decimal NetAmount { get; set; }
        public TransactionType TransactionType { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? FrozenUntil { get; set; }
    }
}